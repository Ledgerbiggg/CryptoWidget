using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using CryptoWidget.Common.Config;
using CryptoWidget.Common.Logger;
using CryptoWidget.Models;
using CryptoWidget.Services.IService;
using CryptoWidget.Shell.ViewModels;

namespace CryptoWidget.Shell.Views;

/// <summary>主卡片窗口：无边框悬浮、钉住置顶、关闭隐藏到托盘、托盘呼出/退出编排</summary>
public partial class MainWindow : Window
{
    /// <summary>单实例唤出消息（App 启动第二实例时发送，收到后显示卡片）</summary>
    private const int WmShowInstance = 0x0401;

    private readonly MainViewModel _vm;
    private readonly ITrayService _tray;
    private readonly ConfigService _config;

    private AppSettings _settings;
    private HwndSource? _hwndSource;
    private bool _closingToTray = true;

    public MainWindow(MainViewModel vm, ITrayService tray, ConfigService config)
    {
        InitializeComponent();
        _vm = vm;
        _tray = tray;
        _config = config;
        _settings = config.LoadSettings();
        DataContext = vm;

        // 窗口图标（用户提供的比特币图标），失败不阻塞
        try
        {
            Icon = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/btc.ico", UriKind.Absolute));
        }
        catch (Exception ex)
        {
            LoggerHelper.Error("设置窗口图标失败（已忽略）", ex);
        }

        // 托盘：左键单击呼出卡片；右键 Quit 退出
        _tray.OpenRequested += (_, _) => ShowCard();
        _tray.ExitRequested += (_, _) => ExitApp();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(WndProc);
        ApplySavedWindowState();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _tray.Show();
            _vm.Initialize();
        }
        catch (Exception ex)
        {
            LoggerHelper.Error("主窗口加载初始化异常", ex);
        }
    }

    /// <summary>窗口消息处理：收到单实例唤出消息时显示卡片</summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmShowInstance)
        {
            handled = true;
            ShowCard();
        }
        return IntPtr.Zero;
    }

    /// <summary>呼出卡片到前台（托盘左键单击 / 二次启动唤出）</summary>
    private void ShowCard()
    {
        Show();
        Activate();
    }

    /// <summary>卡片空白处拖动窗口；按钮区域不触发（OriginalSource 属于按钮时跳过）</summary>
    private void CardBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && FindVisualParent<ButtonBase>(d) != null)
            return;
        try { DragMove(); } catch { /* 极小概率异常忽略 */ }
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T match) return match;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    /// <summary>点 ×：拦截关闭，隐藏到托盘常驻（真正退出走托盘 Quit）</summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closingToTray)
        {
            e.Cancel = true;
            SaveWindowState();
            Hide();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _hwndSource?.RemoveHook(WndProc);
    }

    /// <summary>恢复上次窗口位置（默认屏幕右上角），显示器变化后越界则回退到默认位置</summary>
    private void ApplySavedWindowState()
    {
        var area = SystemParameters.WorkArea;
        if (_settings.WindowLeft is double l && _settings.WindowTop is double t
            && l >= area.Left - 200 && l <= area.Right - 60
            && t >= area.Top && t <= area.Bottom - 30)
        {
            Left = l;
            Top = t;
        }
        else
        {
            Left = area.Right - Math.Max(Width, 280) - 24;
            Top = area.Top + 24;
        }
    }

    /// <summary>保存窗口位置（隐藏/退出时触发；SaveSettings 广播 SettingsSaved，币种无变化不会重连）</summary>
    private void SaveWindowState()
    {
        if (WindowState != WindowState.Normal) return;
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _config.SaveSettings(_settings);
    }

    /// <summary>托盘 Quit：真正结束进程</summary>
    private void ExitApp()
    {
        _closingToTray = false;
        SaveWindowState();
        _tray.Hide();
        Application.Current.Shutdown();
    }
}
