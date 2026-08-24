using CryptoWidget.Common.Logger;
using CryptoWidget.Services.IService;
using System.Windows.Forms;

namespace CryptoWidget.Services.Service;

/// <summary>基于 NotifyIcon 的托盘服务（仅 Quit 菜单）</summary>
public class TrayService : ITrayService, IDisposable
{
    private NotifyIcon? _notify;

    public event EventHandler? OpenRequested;
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
        var quit = new ToolStripMenuItem("Quit");
        quit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
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
            // 优先使用打包图标，失败回退到系统图标，避免安装后缺资源导致崩溃
            var asm = typeof(TrayService).Assembly;
            var names = asm.GetManifestResourceNames();
            var res = names.FirstOrDefault(n => n.EndsWith("tray.ico", StringComparison.OrdinalIgnoreCase));
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
