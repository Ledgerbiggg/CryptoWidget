namespace CryptoWidget.Services.IService;

/// <summary>系统托盘服务（仿 AIMux TrayService 事件模型，仅保留 Quit）</summary>
public interface ITrayService
{
    /// <summary>左键单击托盘图标</summary>
    event EventHandler? OpenRequested;

    /// <summary>右键菜单：置顶切换</summary>
    event EventHandler? PinRequested;

    /// <summary>右键菜单：打开设置</summary>
    event EventHandler? SettingsRequested;

    /// <summary>右键菜单 Quit</summary>
    event EventHandler? ExitRequested;

    void Show();
    void Hide();
}
