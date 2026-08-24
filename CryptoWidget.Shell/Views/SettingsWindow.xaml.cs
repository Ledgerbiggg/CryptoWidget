using System.Windows;
using CryptoWidget.Common.Logger;
using CryptoWidget.Shell.ViewModels;

namespace CryptoWidget.Shell.Views;

/// <summary>设置窗口：币种增删、显示开关、开机自启、代理；ViewModel 单例保持编辑状态</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // 点「保存设置」：弹保存成功提示并自动关闭窗口
        vm.Saved += (_, _) => OnSaved();
        // 关闭前兜底保存：防止焦点转移未触发 LostFocus 导致小数位/代理改动丢失（静默，不弹提示）
        Closing += (_, _) => vm.SaveOnClose();

        // 窗口图标与主卡片一致（用户提供的比特币图标），失败不阻塞
        try
        {
            Icon = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/btc.ico", UriKind.Absolute));
        }
        catch (Exception ex)
        {
            LoggerHelper.Error("设置窗口图标加载失败（已忽略）", ex);
        }
    }

    /// <summary>保存成功：提示后自动关闭设置窗口</summary>
    private void OnSaved()
    {
        MessageBox.Show(this, "配置已保存", "CryptoWidget", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }
}
