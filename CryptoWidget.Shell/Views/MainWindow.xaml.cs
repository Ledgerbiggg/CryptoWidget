using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using CryptoWidget.Common.Config;
using CryptoWidget.Common.Hotkey;
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
    private readonly HotkeyManager _hotkeyManager;

    /// <summary>显示/隐藏卡片热键动作 Id（与注册 id 对应）</summary>
    private const string ToggleCardAction = "ToggleCard";
    private const int ToggleCardHotkeyId = 0x1001;

    private AppSettings _settings;
    private HwndSource? _hwndSource;
    private bool _closingToTray = true;

    public MainWindow(MainViewModel vm, ITrayService tray, ConfigService config, HotkeyManager hotkeyManager)
    {
        InitializeComponent();
        _vm = vm;
        _tray = tray;
        _config = config;
        _hotkeyManager = hotkeyManager;
        _settings = config.LoadSettings();
        DataContext = vm;

        // 窗口图标（用户提供的比特币图标）。注意 .ico 必须用 IconBitmapDecoder 解码，BitmapImage 不支持
        try
        {
            var decoder = new System.Windows.Media.Imaging.IconBitmapDecoder(
                new Uri("pack://application:,,,/Assets/btc.ico", UriKind.Absolute),
                System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            Icon = decoder.Frames[0];
        }
        catch (Exception ex)
        {
            LoggerHelper.Error("设置窗口图标失败（已忽略）", ex);
        }

        // 托盘：左键单击呼出卡片；右键菜单 显示卡片/置顶/设置/Quit
        _tray.OpenRequested += (_, _) =>
        {
            ShowCard();
            _tray.SetShowChecked(true);
        };
        _tray.ShowToggleRequested += (_, _) => ToggleShowCard();
        _tray.PinRequested += (_, _) => _vm.IsPinned = !_vm.IsPinned;
        _tray.SettingsRequested += (_, _) => _vm.OpenSettingsCommand.Execute();
        _tray.ExitRequested += (_, _) => ExitApp();
        // 置顶状态变化时同步托盘菜单勾选状态
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsPinned))
                _tray.SetPinChecked(_vm.IsPinned);
        };

        // 配置保存后（含设置窗口改热键）重新注册热键，立即生效
        _config.SettingsSaved += (_, _) =>
        {
            _settings = _config.LoadSettings();
            RegisterHotkey();
        };

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
            RegisterHotkey();
            // 隐藏启动参数 --open-settings：启动后自动打开设置窗口（截图/调试用）
            if (Environment.GetCommandLineArgs().Contains("--open-settings"))
                _vm.OpenSettingsCommand.Execute();
        }
        catch (Exception ex)
        {
            LoggerHelper.Error("主窗口加载初始化异常", ex);
        }
    }

    /// <summary>窗口消息处理：WM_HOTKEY 由 HotkeyManager 分发；单实例唤出消息时显示卡片</summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_hotkeyManager.HandleMessage(msg, wParam))
        {
            handled = true;
        }
        else if (msg == WmShowInstance)
        {
            handled = true;
            ShowCard();
        }
        return IntPtr.Zero;
    }

    /// <summary>注册显示/隐藏卡片全局热键（默认 Alt+1）；失败仅记日志不弹窗，说明被其他程序占用</summary>
    private void RegisterHotkey()
    {
        try
        {
            _hotkeyManager.UnregisterAll();
            _hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

            var hwnd = new WindowInteropHelper(this).Handle;
            var binding = _settings.ToggleHotkey ?? new HotkeyBinding { Modifier = "Alt", Key = "1" };
            if (string.IsNullOrEmpty(binding.Key)) return;

            if (!_hotkeyManager.Register(hwnd, ToggleCardAction, binding.Modifier, binding.Key, ToggleCardHotkeyId))
                LoggerHelper.Warn($"热键注册失败: {binding.Modifier}+{binding.Key} — {_hotkeyManager.LastError}");
        }
        catch (Exception ex)
        {
            // 任何异常都不阻塞应用启动
            LoggerHelper.Error("注册热键异常", ex);
        }
    }

    /// <summary>热键触发：显示/隐藏卡片（与托盘「显示卡片」开关同一套状态同步）</summary>
    private void OnHotkeyPressed(object? sender, string actionId)
    {
        if (actionId != ToggleCardAction) return;
        ToggleShowCard();
    }

    /// <summary>呼出卡片到前台（托盘左键单击 / 二次启动唤出）</summary>
    private void ShowCard()
    {
        Show();
        Activate();
    }

    /// <summary>托盘「显示卡片」开关：取消勾选=临时隐藏（不退出），再勾选=呼出；与 × 隐藏共用状态同步</summary>
    private void ToggleShowCard()
    {
        if (IsVisible)
        {
            SaveWindowState();
            Hide();
            _tray.SetShowChecked(false);
        }
        else
        {
            ShowCard();
            _tray.SetShowChecked(true);
        }
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
            _tray.SetShowChecked(false); // 卡片隐藏，托盘「显示卡片」同步取消勾选
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

    /// <summary>保存窗口位置（隐藏/退出时触发）；基于最新配置只改位置，避免旧快照覆盖币种/透明度等新改动</summary>
    private void SaveWindowState()
    {
        if (WindowState != WindowState.Normal) return;
        _vm.SaveWindowPosition(Left, Top);
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
