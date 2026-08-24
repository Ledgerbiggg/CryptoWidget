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

    /// <summary>价格颜色（大屏效果）：新价比上一笔高变绿、低变红；关闭则固定白色</summary>
    public bool PriceColorByTick { get; set; } = true;

    /// <summary>开机自启</summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>是否已钉住（始终置顶）。默认未钉住</summary>
    public bool IsPinned { get; set; } = false;

    /// <summary>窗口位置记忆（退出时保存，启动时恢复）</summary>
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    /// <summary>OKX WS 代理地址（可选，如 http://127.0.0.1:7890；空则走系统代理/环境变量）</summary>
    public string Proxy { get; set; } = "http://127.0.0.1:7890";

    /// <summary>卡片背景不透明度（0~1，越小越透明；默认接近全透明）</summary>
    public double BackgroundOpacity { get; set; } = 0.12;
}
