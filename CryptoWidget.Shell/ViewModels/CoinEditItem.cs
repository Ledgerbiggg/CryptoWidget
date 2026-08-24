using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CryptoWidget.Common.Config;

namespace CryptoWidget.Shell.ViewModels;

/// <summary>设置窗口里的单个币种编辑项：图标、名称/交易对、上下排序、可单独配置价格小数位</summary>
public class CoinEditItem : INotifyPropertyChanged
{
    /// <summary>OKX 官方币种图标 CDN（URL 用小写币种名拼接，加载失败回退首字母色块）</summary>
    private const string IconCdnBase = "https://static.okx.com/cdn/oksupport/asset/currency/icon/";

    private static readonly HttpClient IconHttp = new() { Timeout = TimeSpan.FromSeconds(5) };

    private static readonly Color[] BadgeColors =
    [
        Color.FromRgb(0xF7, 0x93, 0x1A), Color.FromRgb(0x62, 0x77, 0xE8),
        Color.FromRgb(0x2E, 0xB6, 0xA6), Color.FromRgb(0xE8, 0x5D, 0x75),
        Color.FromRgb(0x8A, 0x5C, 0xF0), Color.FromRgb(0x3B, 0xA7, 0xEA),
        Color.FromRgb(0xF2, 0xB7, 0x05), Color.FromRgb(0x53, 0x6D, 0xF1),
    ];

    public CoinEditItem(string symbol, string instId, int? decimalPlaces = null)
    {
        Symbol = symbol;
        InstId = instId;
        DecimalPlacesText = decimalPlaces?.ToString() ?? "";

        // 首字母色块颜色按币种名稳定哈希固定（不能用 GetHashCode，.NET 每次运行随机化）
        var idx = Math.Abs(StableHash(symbol)) % BadgeColors.Length;
        var brush = new SolidColorBrush(BadgeColors[idx]);
        brush.Freeze();
        InitialBrush = brush;
    }

    public string Symbol { get; }
    public string InstId { get; }

    /// <summary>图标加载失败时显示的首字母</summary>
    public string Initial => Symbol.Length > 0 ? Symbol[..1].ToUpperInvariant() : "?";

    public SolidColorBrush InitialBrush { get; }

    private ImageSource? _icon;
    public ImageSource? Icon { get => _icon; set => Set(ref _icon, value); }

    /// <summary>CDN 图标是否加载成功（成功显示图片，失败显示首字母色块）</summary>
    private bool _iconLoaded;
    public bool IconLoaded { get => _iconLoaded; set => Set(ref _iconLoaded, value); }

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

    /// <summary>从 OKX 图标 CDN 异步加载币种图标，失败保持首字母色块</summary>
    public async void LoadIconAsync()
    {
        try
        {
            var url = IconCdnBase + Symbol.ToLowerInvariant() + ".png";
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
            Icon = bmp;
            IconLoaded = true;
        }
        catch
        {
            // 网络失败保持首字母色块，不阻塞设置窗口
        }
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

    public event EventHandler? DecimalPlacesChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _decimalPlacesText = "";

    protected void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
