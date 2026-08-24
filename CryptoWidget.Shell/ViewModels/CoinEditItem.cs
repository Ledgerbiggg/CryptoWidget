using System.ComponentModel;
using CryptoWidget.Common.Config;

namespace CryptoWidget.Shell.ViewModels;

/// <summary>设置窗口里的单个币种编辑项：展示名称/交易对，可单独配置价格小数位</summary>
public class CoinEditItem : INotifyPropertyChanged
{
    public CoinEditItem(string symbol, string instId, int? decimalPlaces = null)
    {
        Symbol = symbol;
        InstId = instId;
        DecimalPlacesText = decimalPlaces?.ToString() ?? "";
    }

    public string Symbol { get; }
    public string InstId { get; }

    /// <summary>小数位输入文本（留空 = 原样显示），失焦后保存</summary>
    public string DecimalPlacesText
    {
        get => _decimalPlacesText;
        set
        {
            if (_decimalPlacesText == value) return;
            _decimalPlacesText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DecimalPlacesText)));
            DecimalPlacesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>解析小数位配置：空返回 null（原样显示），非法输入也按 null 处理</summary>
    public int? ParseDecimalPlaces()
    {
        var t = DecimalPlacesText.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return int.TryParse(t, out var p) && p >= 0 && p <= 10 ? p : null;
    }

    public event EventHandler? DecimalPlacesChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _decimalPlacesText = "";
}
