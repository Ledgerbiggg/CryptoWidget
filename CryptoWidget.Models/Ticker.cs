namespace CryptoWidget.Models;

/// <summary>行情快照（由一个币种的最新推送更新）</summary>
public class Ticker
{
    public string InstId { get; set; } = "";

    /// <summary>最新价</summary>
    public decimal Last { get; set; }

    /// <summary>最新价原始字符串（OKX 返回原样，未配置小数位时直接展示）</summary>
    public string RawLast { get; set; } = "";

    /// <summary>24h 涨跌幅（百分比，如 1.23 表示 +1.23%）</summary>
    public decimal ChangePercent { get; set; }

    /// <summary>连接状态</summary>
    public bool Connected { get; set; } = false;
}
