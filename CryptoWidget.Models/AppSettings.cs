namespace CryptoWidget.Models;

/// <summary>全局设置（持久化）</summary>
public class AppSettings
{
    /// <summary>订阅币种列表</summary>
    public List<CoinConfig> Coins { get; set; } = new() { new CoinConfig("BTC", "BTC-USDT") };

    public bool ShowIcon { get; set; } = true;
    public bool ShowName { get; set; } = true;
    public bool ShowPrice { get; set; } = true;
    public bool ShowChange { get; set; } = true;

    /// <summary>是否显示全局连接状态圆点</summary>
    public bool ShowConnectionStatus { get; set; } = true;

    /// <summary>币种布局方向：false=横向（默认），true=竖向</summary>
    public bool IsVerticalLayout { get; set; } = false;

    /// <summary>价格颜色模式（固定黑/白/红绿跳动），默认红绿</summary>
    public PriceColorMode PriceColorMode { get; set; } = PriceColorMode.RedGreen;

    /// <summary>涨跌幅基准（无/当日+8/当日UTC/24h），默认 24h</summary>
    public ChangeMode ChangeMode { get; set; } = ChangeMode.Last24h;

    /// <summary>开机自启</summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>是否已钉住（始终置顶）。默认未钉住</summary>
    public bool IsPinned { get; set; } = false;

    /// <summary>显示/隐藏卡片全局热键（默认 Alt+1）</summary>
    public HotkeyBinding ToggleHotkey { get; set; } = new() { Modifier = "Alt", Key = "1" };

    /// <summary>窗口位置记忆（退出时保存，启动时恢复）</summary>
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    /// <summary>OKX WS 代理地址（可选，如 http://127.0.0.1:7890；空则走系统代理/环境变量）</summary>
    public string Proxy { get; set; } = "http://127.0.0.1:7890";

    /// <summary>卡片背景不透明度（0~1，越小越透明；默认接近全透明）</summary>
    public double BackgroundOpacity { get; set; } = 0.12;

    /// <summary>字体族名（如 Microsoft YaHei UI）</summary>
    public string FontFamily { get; set; } = "Microsoft YaHei UI";

    /// <summary>主字号（价格/名称等）</summary>
    public double FontSize { get; set; } = 12;

    /// <summary>字重（Normal / SemiBold / Bold）</summary>
    public string FontWeight { get; set; } = "SemiBold";
}
