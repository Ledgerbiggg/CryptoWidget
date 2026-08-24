using CryptoWidget.Common.Logger;
using CryptoWidget.Services.IService;
using System.Windows.Forms;

namespace CryptoWidget.Services.Service;

/// <summary>基于 NotifyIcon 的托盘服务（仅 Quit 菜单）</summary>
public class TrayService : ITrayService, IDisposable
{
    private NotifyIcon? _notify;

    public event EventHandler? OpenRequested;
    public event EventHandler? PinRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public void Show()
    {
        if (_notify != null) return;

        var icon = LoadIcon();
        _notify = new NotifyIcon
        {
            Icon = icon,
            Text = "CryptoWidget",
            Visible = true,
        };
        _notify.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                OpenRequested?.Invoke(this, EventArgs.Empty);
        };

        var menu = new ContextMenuStrip();
        var pin = new ToolStripMenuItem("置顶");
        pin.Click += (_, _) => PinRequested?.Invoke(this, EventArgs.Empty);
        var settings = new ToolStripMenuItem("设置");
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        var quit = new ToolStripMenuItem("Quit");
        quit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(pin);
        menu.Items.Add(settings);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quit);
        _notify.ContextMenuStrip = menu;
    }

    public void Hide()
    {
        if (_notify == null) return;
        _notify.Visible = false;
        _notify.Dispose();
        _notify = null;
    }

    private static Icon LoadIcon()
    {
        try
        {
            // 优先使用用户提供的 btc.ico，回退打包图标/系统图标，避免缺资源崩溃
            var asm = typeof(TrayService).Assembly;
            var names = asm.GetManifestResourceNames();
            var res = names.FirstOrDefault(n => n.EndsWith("btc.ico", StringComparison.OrdinalIgnoreCase))
                   ?? names.FirstOrDefault(n => n.EndsWith("tray.ico", StringComparison.OrdinalIgnoreCase));
            if (res != null)
            {
                using var stream = asm.GetManifestResourceStream(res)!;
                return new Icon(stream);
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Error("加载托盘图标失败，回退系统图标", ex);
        }
        return SystemIcons.Application;
    }

    public void Dispose() => Hide();
}
