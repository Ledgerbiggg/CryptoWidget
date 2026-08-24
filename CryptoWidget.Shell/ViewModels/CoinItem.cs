using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace CryptoWidget.Shell.ViewModels;

/// <summary>单个币种单元的展示模型：状态圆点、图标、名称、价格、涨跌幅</summary>
public class CoinItem : INotifyPropertyChanged
{
    private static readonly Color[] BadgeColors =
    [
        Color.FromRgb(0xF7, 0x93, 0x1A), Color.FromRgb(0x62, 0x77, 0xE8),
        Color.FromRgb(0x2E, 0xB6, 0xA6), Color.FromRgb(0xE8, 0x5D, 0x75),
        Color.FromRgb(0x8A, 0x5C, 0xF0), Color.FromRgb(0x3B, 0xA7, 0xEA),
        Color.FromRgb(0xF2, 0xB7, 0x05), Color.FromRgb(0x53, 0x6D, 0xF1),
    ];

    private static readonly SolidColorBrush UpBrush = new(Color.FromRgb(0x16, 0xC7, 0x84));
    private static readonly SolidColorBrush DownBrush = new(Color.FromRgb(0xEF, 0x53, 0x50));
    private static readonly SolidColorBrush DefaultPriceBrush = Brushes.White;

    static CoinItem()
    {
        UpBrush.Freeze();
        DownBrush.Freeze();
    }

    public CoinItem(string symbol, string instId, int? decimalPlaces)
    {
        Symbol = symbol;
        InstId = instId;
        DecimalPlaces = decimalPlaces;
        // 首字母色块颜色按币种名稳定哈希固定，同币种始终同色（不能用 GetHashCode，.NET 每次运行随机化）
        var idx = Math.Abs(StableHash(symbol)) % BadgeColors.Length;
        InitialBrush = new SolidColorBrush(BadgeColors[idx]);
        InitialBrush.Freeze();
    }

    public string Symbol { get; }
    public string InstId { get; }

    /// <summary>价格显示小数位数（null 则原样显示 OKX 返回价格）</summary>
    public int? DecimalPlaces { get; }

    /// <summary>图标加载失败时显示的首字母</summary>
    public string Initial => Symbol.Length > 0 ? Symbol[..1].ToUpperInvariant() : "?";

    public SolidColorBrush InitialBrush { get; }

    private bool _connected;
    public bool Connected { get => _connected; set => Set(ref _connected, value); }

    private string _lastText = "--";
    public string LastText { get => _lastText; set => Set(ref _lastText, value); }

    private string _changeText = "--";
    public string ChangeText { get => _changeText; set => Set(ref _changeText, value); }

    private decimal _changePercent;
    public decimal ChangePercent { get => _changePercent; set => Set(ref _changePercent, value); }

    private Brush _lastBrush = DefaultPriceBrush;
    /// <summary>价格前景色：开启价格着色时随涨跌变绿/红，否则白色</summary>
    public Brush LastBrush { get => _lastBrush; set => Set(ref _lastBrush, value); }

    private ImageSource? _icon;
    public ImageSource? Icon { get => _icon; set => Set(ref _icon, value); }

    /// <summary>CDN 图标是否加载成功（成功显示图片，失败显示首字母色块）</summary>
    private bool _iconLoaded;
    public bool IconLoaded { get => _iconLoaded; set => Set(ref _iconLoaded, value); }

    /// <summary>上一笔价格（用于跳动方向着色：新价高变绿、低变红）</summary>
    private decimal _lastPrice;

    /// <summary>最近价格小数位窗口：取最大值稳定显示，避免 OKX 偶发省略尾部零（79030 vs 79030.1）导致宽度抖动</summary>
    private readonly Queue<int> _recentDecimals = new();
    private const int RecentPriceCount = 5;
    private int _displayDecimals = -1;

    /// <summary>按配置格式化价格：配置了小数位则四舍五入到该位数；否则用最近 5 个价格的最大小数位补零显示；并按跳动方向着色</summary>
    public void ApplyPrice(string rawLast, decimal last, bool priceColorByTick)
    {
        LastText = FormatPrice(rawLast, last);
        UpdateTickBrush(last, priceColorByTick);
        _lastPrice = last;
    }

    /// <summary>价格文本：配置小数位优先；否则按最近 5 个价格的最大小数位格式化（补零，宽度稳定）</summary>
    private string FormatPrice(string rawLast, decimal last)
    {
        if (DecimalPlaces is int p)
            return last.ToString("F" + p, CultureInfo.InvariantCulture);

        _recentDecimals.Enqueue(CountDecimals(rawLast));
        while (_recentDecimals.Count > RecentPriceCount)
            _recentDecimals.Dequeue();
        _displayDecimals = _recentDecimals.Max();

        return _displayDecimals >= 0
            ? last.ToString("F" + _displayDecimals, CultureInfo.InvariantCulture)
            : string.IsNullOrEmpty(rawLast) ? last.ToString(CultureInfo.InvariantCulture) : rawLast;
    }

    /// <summary>统计原始价格字符串的小数位数（如 79030 → 0，79030.1 → 1）</summary>
    private static int CountDecimals(string s)
    {
        var idx = s.IndexOf('.');
        return idx < 0 ? 0 : s.Length - idx - 1;
    }

    /// <summary>应用涨跌幅文本（+1.23% / -0.45%），颜色固定绿涨红跌（由转换器处理）</summary>
    public void ApplyChange(decimal changePercent)
    {
        ChangePercent = changePercent;
        ChangeText = (changePercent >= 0 ? "+" : "") + changePercent.ToString("F2", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>价格颜色：新价比上一笔高变绿、低变红、相同保持；关闭或首笔（无上一笔）为白色</summary>
    private void UpdateTickBrush(decimal newPrice, bool enabled)
    {
        if (!enabled || _lastPrice == 0m)
        {
            LastBrush = DefaultPriceBrush;
            return;
        }
        LastBrush = newPrice > _lastPrice ? UpBrush : newPrice < _lastPrice ? DownBrush : LastBrush;
    }

    /// <summary>按币种名生成稳定哈希（不随进程随机化）</summary>
    private static int StableHash(string s)
    {
        unchecked
        {
            var h = 17;
            foreach (var c in s) h = h * 31 + c;
            return h;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
