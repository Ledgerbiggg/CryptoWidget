using System.Text.Json;
using CryptoWidget.Common.Logger;
using CryptoWidget.Models;

namespace CryptoWidget.Common.Config;

/// <summary>本地 JSON 配置读写：管理 %AppData%\CryptoWidget\ 下的配置（对用户透明，无需手管文件）</summary>
public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string RootDir { get; }
    public string SettingsPath { get; }

    public ConfigService() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoWidget"))
    {
    }

    public ConfigService(string rootDir)
    {
        RootDir = rootDir;
        SettingsPath = Path.Combine(RootDir, "settings.json");
        try { Directory.CreateDirectory(RootDir); } catch { }
    }

    /// <summary>读取设置；文件缺失或解析失败返回默认设置（默认仅订阅 BTC）。
    /// 旧配置/首次启动若没有方案池，自动用当前外观生成「默认」方案并落盘，保证平滑升级</summary>
    public AppSettings LoadSettings()
    {
        var s = Load(SettingsPath, () => Default());
        if (EnsureProfiles(s)) SaveSettings(s);
        return s;
    }

    /// <summary>切换到指定外观方案：把方案外观字段写回当前配置顶层并广播，主卡片即时刷新</summary>
    public void ApplyProfile(string id)
    {
        var s = LoadSettings();
        var p = s.Profiles.FirstOrDefault(x => x.Id == id);
        if (p == null) return;
        p.CopyTo(s);
        s.ActiveProfileId = id;
        SaveSettings(s);
    }

    /// <summary>保存设置（原子写）</summary>
    public void SaveSettings(AppSettings settings)
    {
        Save(SettingsPath, settings);
        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>全局设置变更事件（主窗口据此刷新界面 / 重新订阅）</summary>
    public event EventHandler? SettingsSaved;

    public static AppSettings Default() => new()
    {
        Coins = new List<CoinConfig> { new("BTC", "BTC-USDT") },
        ShowIcon = true,
        ShowName = true,
        ShowPrice = true,
        ShowChange = true,
        ShowConnectionStatus = true,
        IsVerticalLayout = false,
        PriceColorMode = PriceColorMode.RedGreen,
        ChangeMode = ChangeMode.Last24h,
        AutoStart = false,
        IsPinned = false,
        // 默认走本地代理（用户环境），可在设置中修改或清空改回系统代理
        Proxy = "http://127.0.0.1:7890",
        BackgroundOpacity = 0.12,
        FontFamily = "Microsoft YaHei UI",
        FontSize = 12,
        FontWeight = "SemiBold",
        // 默认方案：外观字段与上面顶层默认值一致，首启动即用「默认」方案
        Profiles = new List<AppearanceProfile> { new() { Id = "default", Name = "默认" } },
        ActiveProfileId = "default",
    };

    /// <summary>迁移：旧配置/新建时若没有方案池，用当前外观生成「默认」方案（固定 Id 便于识别）。
    /// 返回 true 表示发生了迁移（调用方据此落盘一次）</summary>
    private static bool EnsureProfiles(AppSettings s)
    {
        if (s.Profiles is { Count: > 0 }) return false;
        var def = AppearanceProfile.FromSettings(s, "默认");
        def.Id = "default";
        s.Profiles = new List<AppearanceProfile> { def };
        s.ActiveProfileId = def.Id;
        return true;
    }

    private static T Load<T>(string path, Func<T> fallback) where T : class
    {
        try
        {
            if (!File.Exists(path)) return fallback();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback();
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"读取配置失败: {path}", ex);
            return fallback();
        }
    }

    private static void Save<T>(string path, T value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, true);
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"保存配置失败: {path}", ex);
        }
    }
}
