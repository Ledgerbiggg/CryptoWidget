using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using CryptoWidget.Common.Config;
using CryptoWidget.Common.Logger;
using CryptoWidget.Services.IService;

namespace CryptoWidget.Services.Service;

/// <summary>版本更新检测：GitHub Releases API 查最新版与安装包下载地址，
/// 流式下载（带进度）后启动安装程序。代理复用设置里的显式代理，否则系统代理</summary>
public class UpdateService : IUpdateService
{
    /// <summary>GitHub 最新 Release 接口（tag_name 即版本号，assets 含安装包）</summary>
    private const string LatestApiUrl = "https://api.github.com/repos/Ledgerbiggg/CryptoWidget/releases/latest";

    private readonly HttpClient _client;
    private readonly ConfigService _config;
    private readonly string _localVersion;

    public UpdateService(ConfigService config)
    {
        _config = config;
        _localVersion = GetLocalVersion();
        _client = CreateClient(config.LoadSettings().Proxy);
    }

    /// <summary>检查 GitHub 最新 Release 是否比本地新；无更新/网络异常/无安装包返回 null</summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var resp = await _client.GetAsync(LatestApiUrl);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var version = root.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "").TrimStart('v', 'V') : "";
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

            // assets 里找 CryptoWidget-Setup-x.y.z.exe 安装包（Release 页只放这一个安装包）
            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!name.StartsWith("CryptoWidget-Setup", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    downloadUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    break;
                }
            }

            if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(_localVersion))
                return null;
            if (!IsNewer(version, _localVersion)) return null;

            return new UpdateInfo { Version = version, Notes = notes, DownloadUrl = downloadUrl };
        }
        catch (Exception ex)
        {
            // 网络/解析失败静默：不打扰用户，下次启动再查
            LoggerHelper.Warn($"检查更新失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>流式下载安装包到 %AppData%\CryptoWidget\updates\（progress 报告 0~1）</summary>
    public async Task<string> DownloadAsync(UpdateInfo info, IProgress<double> progress)
    {
        var dir = Path.Combine(_config.RootDir, "updates");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"CryptoWidget-Setup-{info.Version}.exe");

        using var resp = await _client.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? 0;

        await using var src = await resp.Content.ReadAsStreamAsync();
        await using var dst = File.Create(path);
        var buf = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buf)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n));
            read += n;
            if (total > 0) progress?.Report((double)read / total);
        }
        LoggerHelper.Info($"更新安装包下载完成: {path} ({read} bytes)");
        return path;
    }

    /// <summary>启动安装程序（Inno Setup 安装包，用户交互式安装）</summary>
    public void LaunchInstaller(string path)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    /// <summary>本地版本取入口程序集（Shell）版本号；取不到返回空（跳过更新检查）</summary>
    private static string GetLocalVersion()
    {
        var v = Assembly.GetEntryAssembly()?.GetName().Version;
        return v == null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>构造 HttpClient：显式代理优先，否则系统代理；GitHub API 要求 User-Agent</summary>
    private static HttpClient CreateClient(string proxy)
    {
        var handler = new HttpClientHandler();
        if (!string.IsNullOrEmpty(proxy))
        {
            try
            {
                handler.Proxy = new WebProxy(proxy);
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"更新代理解析失败，改用系统代理: {proxy}", ex);
            }
        }
        else
        {
            handler.Proxy = WebRequest.GetSystemWebProxy();
        }

        // 下载安装包可达数十 MB，超时须放宽到 5 分钟（10 秒只够 API 检查，下载必超时）
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoWidget-UpdateChecker");
        return client;
    }

    /// <summary>判断远程版本是否比本地新（SemVer 三元组比较；支持 -beta 预发布后缀，正式版优先于预览版）</summary>
    public static bool IsNewer(string remoteVersion, string localVersion)
    {
        var r = Parse(remoteVersion);
        var l = Parse(localVersion);

        if (r.Major != l.Major) return r.Major > l.Major;
        if (r.Minor != l.Minor) return r.Minor > l.Minor;
        if (r.Patch != l.Patch) return r.Patch > l.Patch;

        // 三元组相等：正式版(无 pre) 优先于 预览版(有 pre)
        if (string.IsNullOrEmpty(l.Pre) && !string.IsNullOrEmpty(r.Pre)) return false; // 本地正式，远程预览
        if (!string.IsNullOrEmpty(l.Pre) && string.IsNullOrEmpty(r.Pre)) return true;  // 本地预览，远程正式
        return false;
    }

    private static (int Major, int Minor, int Patch, string Pre) Parse(string v)
    {
        if (string.IsNullOrWhiteSpace(v))
            return (0, 0, 0, "");

        var s = v.Trim().TrimStart('v', 'V');
        var pre = "";
        var dash = s.IndexOf('-');
        if (dash >= 0)
        {
            pre = s[(dash + 1)..];
            s = s[..dash];
        }

        var parts = s.Split('.');
        int major = 0, minor = 0, patch = 0;
        if (parts.Length > 0) int.TryParse(parts[0], out major);
        if (parts.Length > 1) int.TryParse(parts[1], out minor);
        if (parts.Length > 2) int.TryParse(parts[2], out patch);
        return (major, minor, patch, pre);
    }
}
