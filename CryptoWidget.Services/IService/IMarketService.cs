using CryptoWidget.Models;

namespace CryptoWidget.Services.IService;

/// <summary>行情服务：订阅 OKX 公共 WS，推送每个币种的最新价与涨跌幅</summary>
public interface IMarketService
{
    /// <summary>某币种行情更新</summary>
    event EventHandler<Ticker>? TickerUpdated;

    /// <summary>整体连接状态变化（true=已连，false=断线）</summary>
    event EventHandler<bool>? ConnectionChanged;

    /// <summary>按给定交易对列表（instId，如 BTC-USDT）重新订阅</summary>
    void Subscribe(IReadOnlyList<string> instIds);

    /// <summary>设置代理（下次连接生效）；空串表示走系统代理/环境变量</summary>
    void SetProxy(string proxy);

    void Stop();
}
