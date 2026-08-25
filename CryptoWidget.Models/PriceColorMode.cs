namespace CryptoWidget.Models;

/// <summary>价格颜色模式：固定黑 / 固定白 / 随涨跌跳动着色</summary>
public enum PriceColorMode
{
    /// <summary>固定黑色</summary>
    Black = 0,

    /// <summary>固定白色</summary>
    White = 1,

    /// <summary>红绿随涨跌跳动（新价比上一笔高变绿、低变红）</summary>
    RedGreen = 2,
}
