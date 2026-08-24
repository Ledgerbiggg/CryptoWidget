using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CryptoWidget.Shell.Converter;

/// <summary>涨跌颜色：非负绿、负红（国外习惯绿涨红跌）</summary>
public class ChangePercentToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush UpBrush = new(Color.FromRgb(0x16, 0xC7, 0x84));
    private static readonly SolidColorBrush DownBrush = new(Color.FromRgb(0xEF, 0x53, 0x50));

    static ChangePercentToBrushConverter()
    {
        UpBrush.Freeze();
        DownBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is decimal d && d < 0 ? DownBrush : UpBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>连接状态颜色：绿=已连，红=断线</summary>
public class ConnectedToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ConnectedBrush = new(Color.FromRgb(0x16, 0xC7, 0x84));
    private static readonly SolidColorBrush DisconnectedBrush = new(Color.FromRgb(0xEF, 0x53, 0x50));

    static ConnectedToBrushConverter()
    {
        ConnectedBrush.Freeze();
        DisconnectedBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? ConnectedBrush : DisconnectedBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>布尔取反转 Visibility：true 隐藏、false 显示（图标加载失败时回退首字母色块）</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>币种名固定宽度（竖向布局对齐用）：按字号估算 5 个半角字符宽，币种名最多 5 个字母</summary>
public class SymbolWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var fontSize = value is double f && f > 0 ? f : 12;
        return fontSize * 3.8; // 5 个字母 ≈ 3.8 倍字号（字母平均宽约 0.65~0.75 字号，留少量余量）
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
