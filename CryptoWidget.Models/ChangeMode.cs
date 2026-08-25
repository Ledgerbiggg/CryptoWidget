namespace CryptoWidget.Models;

/// <summary>涨跌幅计算基准</summary>
public enum ChangeMode
{
    /// <summary>不显示涨跌幅</summary>
    None = 0,

    /// <summary>当日开盘（UTC+8 时区零点）至今</summary>
    DayUtc8 = 1,

    /// <summary>当日开盘（UTC 零点）至今</summary>
    DayUtc0 = 2,

    /// <summary>过去 24 小时</summary>
    Last24h = 3,
}
