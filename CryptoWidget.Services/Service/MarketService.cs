using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CryptoWidget.Common.Logger;
using CryptoWidget.Models;
using CryptoWidget.Services.IService;
using Websocket.Client;

namespace CryptoWidget.Services.Service;

/// <summary>
/// OKX 公共 WebSocket 行情服务：订阅 tickers 频道推送最新价与 24h 涨跌幅。
/// 逻辑对齐 ledger-service 的 t_okx_ticker.go：
/// 主/备节点切换、代理（显式优先，否则系统代理）、断线自动重连、ping 心跳。
/// </summary>
public class MarketService : IMarketService, IDisposable
{
    /// <summary>主节点与 AWS 备用节点</summary>
    private static readonly string[] WsUrls =
    [
        "wss://ws.okx.com:8443/ws/v5/public",
        "wss://wsaws.okx.com:8443/ws/v5/public",
    ];

    /// <summary>OKX 要求 30s 内必须 ping，这里每 20s 发一次保活</summary>
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(20);

    /// <summary>断线重连指数退避封顶：2s→4s→8s→16s→30s，机场短暂抖动快速恢复，持续故障不狂刷</summary>
    private const int MaxReconnectDelaySeconds = 30;

    private readonly object _lock = new();
    private readonly System.Threading.Timer _pingTimer;
    private WebsocketClient? _client;
    private IDisposable? _subMessage;
    private IDisposable? _subReconnection;
    private IDisposable? _subDisconnection;
    private List<string> _instIds = [];
    private int _urlIndex;
    private string _proxy = "";
    /// <summary>收到的 ticker 数据条数（用于降频日志确认数据流）</summary>
    private int _tickerCount;
    /// <summary>指数退避基数：1<<attempt 得 2/4/8/16/32，封顶 30s；连接成功后重置为 1</summary>
    private int _reconnectAttempt = 1;
    /// <summary>是否已有排队的重连任务（防止断线事件风暴重复调度）</summary>
    private bool _reconnecting;
    /// <summary>是否已主动停止（Stop/Dispose/退出），停止后不再自动重连</summary>
    private bool _stopped;

    public event EventHandler<Ticker>? TickerUpdated;
    public event EventHandler<bool>? ConnectionChanged;

    public MarketService()
    {
        // 心跳定时器常驻，连接存在时发送 ping，断线期间发送失败忽略即可
        _pingTimer = new System.Threading.Timer(_ => SendPing(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>按给定交易对列表（instId，如 BTC-USDT）重新订阅；断开旧连接并以主节点重建
    /// 重连在后台线程执行：Dispose/Stop 可能阻塞等待连接关闭，不能在 UI 线程同步调用</summary>
    public void Subscribe(IReadOnlyList<string> instIds)
    {
        var list = instIds.ToList();
        _ = Task.Run(() =>
        {
            lock (_lock)
            {
                _instIds = list;
                _stopped = false;        // 恢复自动重连状态
                _reconnectAttempt = 1;   // 重新订阅重置退避基数
                _reconnecting = false;
                StopClientLocked();
                _urlIndex = 0; // 重新订阅回到主节点
                ConnectCoreLocked();
            }
        });
    }

    /// <summary>设置代理（下次连接生效）；空串表示走系统代理/环境变量</summary>
    public void SetProxy(string proxy)
    {
        lock (_lock)
        {
            _proxy = proxy ?? "";
        }
    }

    public void Stop()
    {
        _pingTimer.Change(Timeout.Infinite, Timeout.Infinite);
        lock (_lock)
        {
            _stopped = true;
            StopClientLocked();
        }
    }

    /// <summary>建立 WebSocket 连接（含代理配置）；库的固定间隔重连关闭，断线重连由自控指数退避接管</summary>
    private void ConnectCoreLocked()
    {
        try
        {
            var client = new WebsocketClient(new Uri(WsUrls[_urlIndex]), CreateClientWebSocket)
            {
                // 库自动重连（固定间隔）关闭，改为自控指数退避：机场断一小会可快速重连，持续故障不会疯狂重试
                ReconnectTimeout = null,
                ErrorReconnectTimeout = null,
                IsReconnectionEnabled = false,
            };

            _subMessage = client.MessageReceived.Subscribe(OnMessageReceived);
            _subReconnection = client.ReconnectionHappened.Subscribe(OnReconnectionHappened);
            _subDisconnection = client.DisconnectionHappened.Subscribe(OnDisconnectionHappened);

            _client = client;
            // 观察 Start 任务异常，避免未观察异常导致进程异常（库内部异常通常已自行处理）
            _ = client.Start().ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                    LoggerHelper.Error("OKX WS 启动异常", t.Exception);
            }, TaskScheduler.Default);
            _pingTimer.Change(PingInterval, PingInterval);
        }
        catch (Exception ex)
        {
            // 建连异常不抛给上层，按指数退避静默重试（机场代理暂时不可用场景）
            LoggerHelper.Error("OKX WS 启动连接失败，将在后台重试", ex);
            ScheduleReconnect();
        }
    }

    /// <summary>构造底层 WebSocket：显式代理优先，否则走系统代理（含环境变量）</summary>
    private ClientWebSocket CreateClientWebSocket()
    {
        var ws = new ClientWebSocket();
        ws.Options.Proxy = ResolveProxy();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        return ws;
    }

    private IWebProxy ResolveProxy()
    {
        if (!string.IsNullOrEmpty(_proxy))
        {
            try
            {
                return new WebProxy(_proxy);
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"代理地址解析失败，改用系统代理: {_proxy}", ex);
            }
        }
        return WebRequest.GetSystemWebProxy();
    }

    private void OnMessageReceived(ResponseMessage e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;
        var text = e.Text.Trim();
        // OKX 心跳回复 "pong" 是纯文本非 JSON，直接忽略
        if (text == "pong") return;
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            // event 类消息：subscribe 回执 / ping / pong / error，全部记日志便于排查订阅失败
            if (root.TryGetProperty("event", out var ev))
            {
                var code = root.TryGetProperty("code", out var c) ? c.GetString() : "";
                var msg = root.TryGetProperty("msg", out var m) ? m.GetString() : "";
                var arg = root.TryGetProperty("arg", out var a) ? a.GetRawText() : "";
                LoggerHelper.Info($"OKX event={ev.GetString()} code={code} msg={msg} arg={arg}");
                return;
            }

            // ticker 行情数据
            if (root.TryGetProperty("arg", out var argEl)
                && root.TryGetProperty("data", out var dataEl)
                && dataEl.GetArrayLength() > 0
                && argEl.TryGetProperty("channel", out var ch)
                && ch.GetString() == "tickers")
            {
                var d = dataEl[0];
                var instId = d.TryGetProperty("instId", out var ii) ? ii.GetString() ?? "" : "";
                var lastStr = d.TryGetProperty("last", out var l) ? l.GetString() ?? "" : "";
                var open24h = d.TryGetProperty("open24h", out var o) ? o.GetString() ?? "" : "";

                // 降频日志：前 3 条全打，之后每 100 条打一条，确认数据流在走
                _tickerCount++;
                if (_tickerCount <= 3 || _tickerCount % 100 == 0)
                    LoggerHelper.Info($"OKX ticker #{_tickerCount} instId={instId} last={lastStr} open24h={open24h}");

                if (!decimal.TryParse(lastStr, out var last))
                {
                    LoggerHelper.Warn($"OKX last 解析失败 instId={instId} last={lastStr}");
                    return;
                }

                // 涨跌幅 = (last - open) / open * 100；三种基准：24h 开盘 / UTC 零点开盘 / UTC+8 零点开盘
                decimal change = 0;
                if (decimal.TryParse(open24h, out var open) && open != 0)
                    change = (last - open) / open * 100m;

                var sodUtc0 = d.TryGetProperty("sodUtc0", out var s0) ? s0.GetString() ?? "" : "";
                var sodUtc8 = d.TryGetProperty("sodUtc8", out var s8) ? s8.GetString() ?? "" : "";

                TickerUpdated?.Invoke(this, new Ticker
                {
                    InstId = instId,
                    Last = last,
                    RawLast = lastStr,
                    ChangePercent = change,
                    ChangeUtc0 = ChangeOf(sodUtc0, last),
                    ChangeUtc8 = ChangeOf(sodUtc8, last),
                    Connected = true,
                });
                return;
            }

            // 其余未知消息（理论上不该出现）
            LoggerHelper.Info($"OKX 未知消息: {Truncate(e.Text)}");
        }
        catch (Exception ex)
        {
            LoggerHelper.Warn($"OKX WS 消息解析失败 raw={Truncate(e.Text)}: {ex.Message}");
        }
    }

    /// <summary>连接建立/重连成功：重置退避、恢复订阅并上报在线（幂等，重连后服务端订阅状态已重置）</summary>
    private void OnReconnectionHappened(ReconnectionInfo info)
    {
        lock (_lock)
        {
            _reconnectAttempt = 1;  // 连接成功，退避基数归零
            _reconnecting = false;
        }
        SendSubscribe();
        LoggerHelper.Info($"OKX WS 已连接（{info.Type}），订阅 {_instIds.Count} 个交易对");
        ConnectionChanged?.Invoke(this, true);
    }

    /// <summary>断开：主动停止不重连；网络类断开切换备用节点并进入指数退避静默重连</summary>
    private void OnDisconnectionHappened(DisconnectionInfo info)
    {
        if (info.Type == DisconnectionType.ByUser)
        {
            // 主动 Stop/Dispose/替换订阅时的断开：不自动重连，避免误重建
            LoggerHelper.Info($"OKX WS 主动断开（{info.Type}），不自动重连");
            return;
        }
        lock (_lock)
        {
            _urlIndex = (_urlIndex + 1) % WsUrls.Length;
            if (_client != null)
                _client.Url = new Uri(WsUrls[_urlIndex]);
            ConnectionChanged?.Invoke(this, false);
            ScheduleReconnect();
        }
        LoggerHelper.Warn($"OKX WS 断开（{info.Type}），将自动重连（切换备用节点）");
    }

    /// <summary>指数退避调度重连：2s→4s→8s→16s→30s 封顶，静默执行（仅日志），连接成功由 OnReconnectionHappened 重置</summary>
    private void ScheduleReconnect()
    {
        lock (_lock)
        {
            if (_stopped || _reconnecting || _client is null) return;
            _reconnecting = true;
            var delaySec = Math.Min(MaxReconnectDelaySeconds, 1 << _reconnectAttempt);
            LoggerHelper.Warn($"OKX WS 将在 {delaySec}s 后自动重连（指数退避，第 {_reconnectAttempt} 次）");
            _reconnectAttempt++;
            _ = Task.Delay(TimeSpan.FromSeconds(delaySec))
                .ContinueWith(_ => TryReconnect(), TaskScheduler.Default);
        }
    }

    /// <summary>执行重连：销毁旧客户端并重建连接（失败会再次进入退避循环）</summary>
    private void TryReconnect()
    {
        lock (_lock)
        {
            _reconnecting = false;
            if (_stopped || _client is null) return;
            if (_client.IsRunning)
            {
                // 断线事件后连接意外已恢复（误报场景），重置退避即可
                _reconnectAttempt = 1;
                return;
            }
            try { _client.Dispose(); } catch { }
            _client = null;
            ConnectCoreLocked();
        }
    }

    /// <summary>按开盘价计算涨跌幅百分比（开盘价缺失/为零返回 0）</summary>
    private static decimal ChangeOf(string openStr, decimal last)
    {
        if (!decimal.TryParse(openStr, out var open) || open == 0) return 0;
        return (last - open) / open * 100m;
    }

    /// <summary>发送订阅消息（tickers 频道，一次订阅全部交易对）</summary>
    private void SendSubscribe()
    {
        lock (_lock)
        {
            if (_client is null || _instIds.Count == 0) return;
            var args = string.Join(",",
                _instIds.Select(id => $"{{\"channel\":\"tickers\",\"instId\":\"{id}\"}}"));
            var json = $"{{\"op\":\"subscribe\",\"args\":[{args}]}}";
            LoggerHelper.Info($"OKX 发送订阅: {json}");
            try
            {
                // 必须用文本帧发送，Send() 默认二进制帧会被 OKX 拒绝（60012 Illegal request）
                _client.SendAsText(Encoding.UTF8.GetBytes(json));
            }
            catch (Exception ex)
            {
                LoggerHelper.Warn($"OKX WS 订阅发送失败: {ex.Message}");
            }
        }
    }

    private void SendPing()
    {
        lock (_lock)
        {
            if (_client is null) return;
            try
            {
                // 心跳：OKX 接受纯文本 "ping"（{"op":"ping"} 会返回 60012 Illegal request）
                _client.SendAsText(Encoding.UTF8.GetBytes("ping"));
            }
            catch
            {
                // 断线期间 ping 失败忽略，重连由库负责
            }
        }
    }

    private void StopClientLocked()
    {
        _subMessage?.Dispose();
        _subReconnection?.Dispose();
        _subDisconnection?.Dispose();
        _subMessage = null;
        _subReconnection = null;
        _subDisconnection = null;

        try { _client?.Dispose(); } catch { }
        _client = null;
    }

    public void Dispose()
    {
        Stop();
        _pingTimer.Dispose();
    }

    /// <summary>截断过长的原始消息用于日志，避免刷屏</summary>
    private static string Truncate(string s, int max = 300)
        => s.Length <= max ? s : s[..max] + "...";
}
