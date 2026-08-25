namespace CryptoWidget.Services.IService;

/// <summary>远程最新版本信息（GitHub Releases）</summary>
public class UpdateInfo
{
    /// <summary>最新版本号，如 "0.0.5"</summary>
    public string Version { get; set; } = "";

    /// <summary>更新说明（Release body）</summary>
    public string Notes { get; set; } = "";

    /// <summary>安装包下载地址</summary>
    public string DownloadUrl { get; set; } = "";
}

/// <summary>版本更新检测服务：查 GitHub 最新 Release、下载安装包、启动安装</summary>
public interface IUpdateService
{
    /// <summary>拉取 GitHub 最新 Release 信息（是否比本地新由调用方用 UpdateService.IsNewer 判断）；网络/解析失败返回 null</summary>
    Task<UpdateInfo?> CheckForUpdateAsync();

    /// <summary>下载安装包到本地（progress 报告 0~1），返回本地文件路径</summary>
    Task<string> DownloadAsync(UpdateInfo info, IProgress<double> progress);

    /// <summary>启动安装程序（调用方随后退出应用，避免安装时 exe 被占用）</summary>
    void LaunchInstaller(string path);
}
