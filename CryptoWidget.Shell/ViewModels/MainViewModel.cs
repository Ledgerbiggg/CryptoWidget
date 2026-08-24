using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CryptoWidget.Common.Config;
using CryptoWidget.Common.Logger;
using CryptoWidget.Models;
using CryptoWidget.Services.IService;
using CryptoWidget.Shell.Views;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;

namespace CryptoWidget.Shell.ViewModels;

/// <summary>主卡片 ViewModel：币种行、显示开关、钉住、行情订阅与设置窗口编排</summary>
public class MainViewModel : BindableBase
{
    /// <summary>OKX 官方币种图标 CDN（加载失败回退首字母色块）</summary>
    private const string IconCdnBase = "https://static.okx.com/cdn/oksupport/asset/currency/icon/";

    private static readonly HttpClient IconHttp = new() { Timeout = TimeSpan.FromSeconds(5) };

    private readonly ConfigService _config;
    private readonly IMarketService _market;
    private readonly IContainerProvider _container;
    private readonly Dictionary<string, CoinItem> _coinsByInstId = [];

    private AppSettings _settings;
    private List<string> _lastInstIds = [];
    private string _lastProxy = "";
    private SettingsWindow? _settingsWindow;

    public MainViewModel(ConfigService config, IMarketService market, IContainerProvider container)
    {
        _config = config;
        _market = market;
        _container = container;
        _settings = config.LoadSettings();

        OpenSettingsCommand = new DelegateCommand(OpenSettings);
        CloseCommand = new DelegateCommand(CloseToTray);

        _market.TickerUpdated += OnTickerUpdated;
        _market.ConnectionChanged += OnConnectionChanged;
        _config.SettingsSaved += OnSettingsSaved;
    }

    public ObservableCollection<CoinItem> Coins { get; } = [];

    private bool _showIcon = true;
    public bool ShowIcon
    {
        get => _showIcon;
        set { if (SetProperty(ref _showIcon, value)) SaveSettings(); }
    }

    private bool _showName = true;
    public bool ShowName
    {
        get => _showName;
        set { if (SetProperty(ref _showName, value)) SaveSettings(); }
    }

    private bool _showPrice = true;
    public bool ShowPrice
    {
        get => _showPrice;
        set { if (SetProperty(ref _showPrice, value)) SaveSettings(); }
    }

    private bool _showChange = true;
    public bool ShowChange
    {
        get => _showChange;
        set { if (SetProperty(ref _showChange, value)) SaveSettings(); }
    }

    private bool _priceColorByTick = true;
    /// <summary>价格颜色（大屏效果）：新价比上一笔高变绿、低变红</summary>
    public bool PriceColorByTick
    {
        get => _priceColorByTick;
        set { if (SetProperty(ref _priceColorByTick, value)) SaveSettings(); }
    }

    private bool _isPinned;
    /// <summary>钉住（置顶）状态：改动即保存，主窗口 Topmost 绑定此值</summary>
    public bool IsPinned
    {
        get => _isPinned;
        set { if (SetProperty(ref _isPinned, value)) SaveSettings(); }
    }

    private double _backgroundOpacity = 0.12;
    /// <summary>卡片背景不透明度（越小越透明），主窗口背景绑定 BackgroundBrush</summary>
    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        set
        {
            if (SetProperty(ref _backgroundOpacity, value))
            {
                UpdateBackgroundBrush();
                SaveSettings();
            }
        }
    }

    private SolidColorBrush _backgroundBrush = new(Color.FromArgb(31, 0x0F, 0x11, 0x15));
    /// <summary>卡片背景画刷（深色 + 按配置透明度）</summary>
    public SolidColorBrush BackgroundBrush
    {
        get => _backgroundBrush;
        private set => SetProperty(ref _backgroundBrush, value);
    }

    private SolidColorBrush _borderBrush = new(Color.FromArgb(15, 255, 255, 255));
    /// <summary>卡片边框画刷（白色，透明度随背景淡出）</summary>
    public SolidColorBrush BorderBrush
    {
        get => _borderBrush;
        private set => SetProperty(ref _borderBrush, value);
    }

    /// <summary>按当前不透明度重建背景/边框画刷（冻结便于绑定）；背景越透明，边框越淡直到消失</summary>
    private void UpdateBackgroundBrush()
    {
        var alpha = (byte)Math.Clamp((int)Math.Round(_backgroundOpacity * 255), 0, 255);
        var bg = new SolidColorBrush(Color.FromArgb(alpha, 0x0F, 0x11, 0x15));
        bg.Freeze();
        BackgroundBrush = bg;

        // 边框比背景更淡一层：全透明时边框也几乎不可见
        var borderAlpha = (byte)Math.Clamp((int)Math.Round(_backgroundOpacity * 96), 0, 255);
        var bd = new SolidColorBrush(Color.FromArgb(borderAlpha, 255, 255, 255));
        bd.Freeze();
        BorderBrush = bd;
    }

    public DelegateCommand OpenSettingsCommand { get; }

    public DelegateCommand CloseCommand { get; }

    /// <summary>开关/钉住状态变化后同步到配置并保存（SaveSettings 广播 SettingsSaved 刷新界面）</summary>
    private void SaveSettings()
    {
        _settings.ShowIcon = ShowIcon;
        _settings.ShowName = ShowName;
        _settings.ShowPrice = ShowPrice;
        _settings.ShowChange = ShowChange;
        _settings.PriceColorByTick = PriceColorByTick;
        _settings.IsPinned = IsPinned;
        _settings.BackgroundOpacity = BackgroundOpacity;
        _config.SaveSettings(_settings);
    }

    /// <summary>主窗口 Loaded 后调用：应用配置并开始订阅行情</summary>
    public void Initialize()
    {
        _settings = _config.LoadSettings();
        ApplySettings();
    }

    /// <summary>应用当前配置：显示开关、钉住、代理、币种列表与订阅</summary>
    private void ApplySettings()
    {
        ShowIcon = _settings.ShowIcon;
        ShowName = _settings.ShowName;
        ShowPrice = _settings.ShowPrice;
        ShowChange = _settings.ShowChange;
        PriceColorByTick = _settings.PriceColorByTick;
        IsPinned = _settings.IsPinned;
        BackgroundOpacity = _settings.BackgroundOpacity;
        UpdateBackgroundBrush();
        _market.SetProxy(_settings.Proxy);
        RebuildCoins();
    }

    /// <summary>按配置增量重建币种行：删除移除的、新增未订阅的，币种列表变化才重连订阅</summary>
    private void RebuildCoins()
    {
        var instIds = _settings.Coins.Select(c => c.InstId).ToList();

        foreach (var instId in _coinsByInstId.Keys.Where(k => !instIds.Contains(k)).ToList())
        {
            var removed = _coinsByInstId[instId];
            _coinsByInstId.Remove(instId);
            Coins.Remove(removed);
        }

        foreach (var cfg in _settings.Coins)
        {
            if (_coinsByInstId.ContainsKey(cfg.InstId)) continue;
            var coin = new CoinItem(cfg.Symbol, cfg.InstId, cfg.DecimalPlaces);
            _coinsByInstId[cfg.InstId] = coin;
            Coins.Add(coin);
            LoadIconAsync(coin);
        }

        // 币种或代理变化才重连；窗口位置等无关变更不打断行情
        var proxyChanged = _settings.Proxy != _lastProxy;
        if (proxyChanged || !instIds.SequenceEqual(_lastInstIds))
        {
            _lastInstIds = instIds;
            _lastProxy = _settings.Proxy;
            _market.Subscribe(instIds);
        }
    }

    /// <summary>设置保存后（含设置窗口改动）：重新加载配置并应用</summary>
    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        _settings = _config.LoadSettings();
        ApplySettings();
    }

    /// <summary>行情推送：后台线程触发，封送到 UI 线程增量更新对应币种单元</summary>
    private void OnTickerUpdated(object? sender, Ticker ticker)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_coinsByInstId.TryGetValue(ticker.InstId, out var item))
            {
                                item.ApplyPrice(ticker.RawLast, ticker.Last, PriceColorByTick);
                                item.ApplyChange(ticker.ChangePercent);
            }
        });
    }

    /// <summary>连接状态变化：同步所有币种的圆点（绿=已连，红=断线）</summary>
    private void OnConnectionChanged(object? sender, bool connected)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            foreach (var coin in Coins)
                coin.Connected = connected;
        });
    }

    /// <summary>从 OKX 图标 CDN 异步加载币种图标，失败保持首字母色块（URL 用小写币种名拼接）</summary>
    private async void LoadIconAsync(CoinItem coin)
    {
        try
        {
            var url = IconCdnBase + coin.Symbol.ToLowerInvariant() + ".png";
            using var resp = await IconHttp.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return;
            var bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            var bmp = new BitmapImage();
            using (var ms = new MemoryStream(bytes))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }
            bmp.Freeze();
            coin.Icon = bmp;
            coin.IconLoaded = true;
        }
        catch
        {
            // 图标加载失败不阻塞，首字母色块兜底
        }
    }

    /// <summary>打开设置窗口（已打开则激活到前台）；设置窗口瞬态注册，关闭后下次重建</summary>
    private void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = _container.Resolve<SettingsWindow>();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>点击 ×：关闭窗口（MainWindow 的 Closing 会拦截并隐藏到托盘）</summary>
    private void CloseToTray()
    {
        if (Application.Current.MainWindow is { } win)
            win.Close();
    }
}
