using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CryptoWidget.Common.Logger;
using CryptoWidget.Shell.ViewModels;

namespace CryptoWidget.Shell.Views;

/// <summary>设置窗口：币种增删/上下排序、显示开关、开机自启、代理、字体样式；ViewModel 单例保持编辑状态</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        // 先同步最新配置（单例 VM），再初始化界面绑定，确保显示的是当前文件里的值
        vm.Reload();
        InitializeComponent();
        DataContext = vm;

        // 点「保存设置」：弹保存成功提示并自动关闭窗口
        // 注意：SettingsViewModel 是单例，必须用命名方法并在窗口关闭时退订，否则每次打开窗口都累积一次订阅，
        // 导致保存时弹出多个「配置已保存」弹窗
        vm.Saved += OnVmSaved;
        Closed += (_, _) => vm.Saved -= OnVmSaved;
        // 关闭前兜底保存：防止焦点转移未触发 LostFocus 导致小数位/代理改动丢失（静默，不弹提示）
        Closing += (_, _) => vm.SaveOnClose();

        // 打开设置即自动检查版本：结果展示在状态栏，发现新版时按钮变为「立即更新」
        _ = vm.AutoCheckAtOpenAsync();

        // 窗口图标与主卡片一致（用户提供的比特币图标）。注意 .ico 必须用 IconBitmapDecoder 解码，BitmapImage 不支持
        try
        {
            var decoder = new IconBitmapDecoder(
                new Uri("pack://application:,,,/Assets/btc.ico", UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Icon = decoder.Frames[0];
        }
        catch (Exception ex)
        {
            LoggerHelper.Error("设置窗口图标加载失败（已忽略）", ex);
        }
    }

    /// <summary>保存成功事件处理：提示后自动关闭设置窗口</summary>
    private void OnVmSaved(object? sender, EventArgs e) => OnSaved();

    /// <summary>保存成功：提示后自动关闭设置窗口</summary>
    private void OnSaved()
    {
        MessageBox.Show(this, "配置已保存", "CryptoWidget", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    /// <summary>新增币种输入框：按回车直接添加（等价点击「添加」按钮）</summary>
    private void NewSymbolBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is SettingsViewModel vm)
            vm.AddCoinCommand.Execute();
        e.Handled = true;
    }

    /// <summary>录制态下拦截所有按键：分发到 ViewModel 捕获组合键（Alt 组合时 WPF 上报 Key.System，取 SystemKey）</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || !vm.IsRecording) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        vm.CaptureHotkey(Keyboard.Modifiers, key);
    }

    /// <summary>ComboBox 悬停时滚轮不应改变选中项，而是滚动页面：拦截并把滚动量转交给外层 ScrollViewer</summary>
    private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        e.Handled = true;
        var scroll = FindAncestor<ScrollViewer>(cb);
        if (scroll == null) return;
        scroll.ScrollToVerticalOffset(scroll.VerticalOffset - e.Delta);
    }

    /// <summary>沿可视树上溯查找指定类型的祖先元素</summary>
    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null && d is not T) d = VisualTreeHelper.GetParent(d);
        return d as T;
    }
}
