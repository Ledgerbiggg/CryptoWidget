using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using CryptoWidget.Common.AutoStart;
using CryptoWidget.Common.Config;
using CryptoWidget.Common.Hotkey;
using CryptoWidget.Common.Logger;
using CryptoWidget.Services.IService;
using CryptoWidget.Services.Service;
using CryptoWidget.Shell.ViewModels;
using CryptoWidget.Shell.Views;
using Prism.Ioc;
using Prism.Unity;

namespace CryptoWidget.Shell;

/// <summary>应用入口：Prism 依赖注入、单实例保护（二次启动唤醒主卡片）、全局异常兜底</summary>
public partial class App : PrismApplication
{
    /// <summary>单实例唤出消息（与主窗口 WndProc 约定一致）</summary>
    internal const int WmShowInstance = 0x0401;

    /// <summary>安装程序请求退出消息：安装包在 PrepareToInstall 里以 --exit-for-update
    /// 二次启动本程序，由已运行实例接收此消息后真正退出（供安装包替换被占用的 exe）</summary>
    internal const int WmExitForUpdate = 0x0402;

    /// <summary>命令行开关：请求正在运行的实例退出，供安装包替换文件前使用</summary>
    private const string ExitForUpdateArg = "--exit-for-update";

    private Mutex? _mutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 全局异常兜底：记录日志 + dump 完整堆栈到桌面文件，绝不静默闪退
        DispatcherUnhandledException += (_, args) =>
        {
            DumpCrash(args.Exception, "UI 线程未处理异常");
            LoggerHelper.Error("UI 线程未处理异常", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            DumpCrash(args.ExceptionObject as Exception, "AppDomain 未处理异常");
            LoggerHelper.Error("AppDomain 未处理异常", args.ExceptionObject as Exception);
        };

        // 安装包以 --exit-for-update 二次启动本程序时，本进程只当信使：
        // 把"退出"消息转给已运行实例后立刻退出。托盘常驻应用会拦截 WM_CLOSE，
        // 靠 Restart Manager 是关不掉它的（安装时会弹"无法自动关闭应用程序"）
        var exitForUpdate = e.Args.Any(
            a => string.Equals(a, ExitForUpdateArg, StringComparison.OrdinalIgnoreCase));

        // 单实例：二次启动通知已有实例呼出窗口，自身退出
        _mutex = new Mutex(true, "CryptoWidget_SingleInstance", out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            NotifyMainWindow(exitForUpdate ? WmExitForUpdate : WmShowInstance);
            Shutdown();
            return;
        }
        // 本进程就是唯一实例：没有可通知的对象，直接退出（安装程序正等它消失）
        if (exitForUpdate)
        {
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { }
            _mutex?.Dispose();
        }
        base.OnExit(e);
    }

    /// <summary>依赖注入注册：配置/服务/窗口与 ViewModel</summary>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        var config = new ConfigService();
        LoggerHelper.Info($"应用启动，配置目录: {config.RootDir}");
        containerRegistry.RegisterInstance(config);

        containerRegistry.RegisterSingleton<IMarketService, MarketService>();
        containerRegistry.RegisterSingleton<ITrayService, TrayService>();
        containerRegistry.RegisterSingleton<IUpdateService, UpdateService>();
        containerRegistry.RegisterSingleton<AutoStartService>();
        containerRegistry.RegisterSingleton<HotkeyManager>();

        containerRegistry.RegisterSingleton<MainViewModel>();
        containerRegistry.RegisterSingleton<SettingsViewModel>();
        containerRegistry.RegisterSingleton<MainWindow>();
        // SettingsWindow 用瞬态注册：WPF Window 关闭后不能再次 Show，每次打开需新实例
        containerRegistry.Register<SettingsWindow>();
    }

    /// <summary>创建主窗口前确保默认配置落盘（首次启动生成 settings.json）</summary>
    protected override Window? CreateShell()
    {
        var config = Container.Resolve<ConfigService>();
        var settings = config.LoadSettings();
        if (!File.Exists(config.SettingsPath))
            config.SaveSettings(settings);
        return Container.Resolve<MainWindow>();
    }

    /// <summary>把未处理异常完整堆栈写到桌面 CryptoWidget-crash.txt，方便定位闪退原因</summary>
    private static void DumpCrash(Exception? ex, string tag)
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var path = Path.Combine(desktop, "CryptoWidget-crash.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {tag}");
            sb.AppendLine(ex?.ToString() ?? "（无异常对象）");
            sb.AppendLine(new string('-', 60));
            File.AppendAllText(path, sb.ToString());
        }
        catch
        {
            // 写桌面失败就放弃，绝不在异常处理里再抛异常
        }
    }

    /// <summary>向已运行实例发送消息（唤出或请求退出，按窗口标题查找主卡片）</summary>
    private static void NotifyMainWindow(int message)
    {
        var hwnd = FindWindow(null, "CryptoWidget");
        if (hwnd != IntPtr.Zero)
            PostMessage(hwnd, message, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string windowName);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
