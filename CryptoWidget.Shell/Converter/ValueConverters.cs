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
