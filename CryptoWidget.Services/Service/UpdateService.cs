using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using CryptoWidget.Common.Config;
using CryptoWidget.Common.Logger;
using CryptoWidget.Services.IService;

namespace CryptoWidget.Services.Service;

/// <summary>版本更新检测：从 raw.githubusercontent.com 读取仓库根 version.json 获取最新版与说明，
/// 再按版本号拼出安装包下载地址；流式下载（带进度）后启动安装。
/// 代理优先级：显式设置 &gt; 环境变量(HTTPS_PROXY/HTTP_PROXY) &gt; 系统 WinINET 代理。</summary>
public class UpdateService : IUpdateService
{
    /// <summary>最新版本信息源：raw 文件独立于 GitHub API 的 60 次/小时匿名限流，配额高得多，
    /// 且 version.json 随每次发版提交到 main，release 工作流也会同步 notes</summary>
    private const string RawVersionUrl = "https://raw.githubusercontent.com/Ledgerbiggg/CryptoWidget/main/version.json";

    /// <summary>安装包下载地址模板：Release tag 为 v{version}，资产名为 CryptoWidget-Setup-{version}.exe</summary>
    private const string DownloadUrlTemplate = "https://github.com/Ledgerbiggg/CryptoWidget/releases/download/v{0}/CryptoWidget-Setup-{0}.exe";

    private readonly ConfigService _config;
    private HttpClient _client;
    private string _proxy;

    /// <summary>内存缓存：避免频繁打开设置狂打 GitHub（限流/抖动），缓存期内直接返回</summary>
    private (DateTime Time, UpdateInfo? Info)? _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public UpdateService(ConfigService config)
    {
        _config = config;
        _proxy = config.LoadSettings().Proxy;
        _client = CreateClient(_proxy);
    }

    /// <summary>设置/更改代理后调用，重建 HttpClient 使新代理立即生效（应用启动后开启代理也生效）</summary>
    public void SetProxy(string proxy)
    {
        var next = proxy ?? "";
        if (next == _proxy) return;
        _proxy = next;
        var old = _client;
        _client = CreateClient(_proxy);
        try { old.Dispose(); } catch { }
        _cache = null; // 代理变了，旧缓存失效
    }

    /// <summary>拉取最新版本信息（是否比本地新由调用方用 IsNewer 判断）；网络/解析失败返回 null。
    /// 含 3 次重试（GitHub 国内偶发抖动）与 5 分钟缓存</summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        // 缓存命中：缓存期内直接返回，避免重复请求触发限流/抖动
        if (_cache.HasValue && DateTime.Now - _cache.Value.Time < CacheTtl)
            return _cache.Value.Info;

        UpdateInfo? result = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                result = await FetchAsync();
                break;
            }
            catch (Exception ex)
            {
                // 网络/解析失败静默：不打扰用户，调用方依据 null 展示「检查失败」状态
                LoggerHelper.Warn($"检查更新第 {attempt} 次失败: {ex.Message}");
                if (attempt < 3) await Task.Delay(800 * attempt);
            }
        }
        _cache = (DateTime.Now, result);
        return result;
    }

    /// <summary>从 raw version.json 解析版本与说明，并按版本号拼出下载地址</summary>
    private async Task<UpdateInfo?> FetchAsync()
    {
        using var resp = await _client.GetAsync(RawVersionUrl);
        if (!resp.IsSuccessStatusCode)
        {
            LoggerHelper.Warn($"version.json 拉取失败 HTTP {(int)resp.StatusCode}");
            return null;
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var version = root.TryGetProperty("version", out var v) ? (v.GetString() ?? "").Trim() : "";
        var notes = root.TryGetProperty("notes", out var b) ? b.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(version))
            return null;

        var clean = version.TrimStart('v', 'V');
        var downloadUrl = string.Format(DownloadUrlTemplate, clean);
        return new UpdateInfo { Version = clean, Notes = notes, DownloadUrl = downloadUrl };
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

    /// <summary>构造 HttpClient：显式代理优先，否则环境变量，再否则系统代理；GitHub API 要求 User-Agent</summary>
    private static HttpClient CreateClient(string proxy)
    {
        var handler = new HttpClientHandler();
        var resolved = ResolveProxy(proxy);
        if (resolved != null)
        {
            // 显式设置代理；不设则 HttpClientHandler 走默认策略（环境变量 + 系统代理）
            handler.Proxy = resolved;
            handler.UseProxy = true;
        }

        // 下载安装包可达数十 MB，超时须放宽到 5 分钟
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoWidget-UpdateChecker");
        return client;
    }

    /// <summary>解析代理：显式 &gt; 环境变量(HTTPS_PROXY/HTTP_PROXY，Clash/WSL/终端代理常用，GetSystemWebProxy 读不到) &gt; 系统 WinINET 代理；都没有返回 null（直连）</summary>
    private static IWebProxy? ResolveProxy(string proxy)
    {
        if (!string.IsNullOrEmpty(proxy))
        {
            try { return new WebProxy(proxy); }
            catch (Exception ex) { LoggerHelper.Error($"更新代理解析失败，改走默认代理: {proxy}", ex); }
        }

        var env = Environment.GetEnvironmentVariable("HTTPS_PROXY")
               ?? Environment.GetEnvironmentVariable("HTTP_PROXY");
        if (!string.IsNullOrEmpty(env))
        {
            try { return new WebProxy(env.Trim()); }
            catch (Exception ex) { LoggerHelper.Error($"环境变量代理解析失败，改走系统代理: {env}", ex); }
        }

        // 与 OKX 行情一致的系统 WinINET 代理
        return WebRequest.GetSystemWebProxy();
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
