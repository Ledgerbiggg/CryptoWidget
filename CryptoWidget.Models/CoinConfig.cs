namespace CryptoWidget.Models;

/// <summary>单个订阅币种配置</summary>
public class CoinConfig
{
    public CoinConfig() { }

    public CoinConfig(string symbol, string instId)
    {
        Symbol = symbol;
        InstId = instId;
    }

    /// <summary>展示名称，如 BTC</summary>
    public string Symbol { get; set; } = "BTC";

    /// <summary>OKX 交易对，如 BTC-USDT</summary>
    public string InstId { get; set; } = "BTC-USDT";

    /// <summary>价格显示小数位数（可选，null 则原样显示 OKX 返回的价格）</summary>
    public int? DecimalPlaces { get; set; }
}
