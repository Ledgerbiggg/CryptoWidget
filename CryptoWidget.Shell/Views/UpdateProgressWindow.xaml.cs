using System.Windows;

namespace CryptoWidget.Shell.Views;

/// <summary>更新下载进度窗口：展示安装包下载进度，下载完成由调用方关闭并启动安装</summary>
public partial class UpdateProgressWindow : Window
{
    public UpdateProgressWindow(string version)
    {
        InitializeComponent();
        TitleText.Text = $"正在下载 v{version} …";
    }

    /// <summary>更新进度显示（percent 为 0~100 百分比）</summary>
    public void UpdateProgress(double percent)
    {
        Bar.Value = percent;
        StatusText.Text = $"{percent:F0}%";
    }
}
