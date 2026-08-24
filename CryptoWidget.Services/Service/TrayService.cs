using CryptoWidget.Common.Logger;
using CryptoWidget.Services.IService;
using System.Windows.Forms;

namespace CryptoWidget.Services.Service;

/// <summary>基于 NotifyIcon 的托盘服务（显示/置顶/设置/退出）</summary>
public class TrayService : ITrayService, IDisposable
{
    private NotifyIcon? _notify;
    private ToolStripMenuItem? _pinMenu;
    private ToolStripMenuItem? _showMenu;

    public event EventHandler? OpenRequested;
    public event EventHandler? PinRequested;
    public event EventHandler? ShowToggleRequested;
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
        // 「显示卡片」：勾选=显示，取消勾选=临时隐藏（不退出程序），点击即切换
        _showMenu = new ToolStripMenuItem("显示卡片")
        {
            Checked = true,
            CheckOnClick = false, // 手动切换勾选，避免与窗口状态脱节
        };
        _showMenu.Click += (_, _) =>
        {
            _showMenu.Checked = !_showMenu.Checked;
            ShowToggleRequested?.Invoke(this, EventArgs.Empty);
        };
        _pinMenu = new ToolStripMenuItem("置顶");
        _pinMenu.Click += (_, _) => PinRequested?.Invoke(this, EventArgs.Empty);
        var settings = new ToolStripMenuItem("设置");
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        var quit = new ToolStripMenuItem("退出");
        quit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(_showMenu);
        menu.Items.Add(_pinMenu);
        menu.Items.Add(settings);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quit);
        _notify.ContextMenuStrip = menu;
    }

    /// <summary>更新「置顶」菜单勾选状态：已置顶时打勾，直观反馈当前状态</summary>
    public void SetPinChecked(bool pinned)
    {
        if (_pinMenu != null)
            _pinMenu.Checked = pinned;
    }

    /// <summary>同步「显示卡片」菜单勾选状态：卡片可见时打勾（× 隐藏/呼出后由主窗口同步）</summary>
    public void SetShowChecked(bool visible)
    {
        if (_showMenu != null)
            _showMenu.Checked = visible;
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
