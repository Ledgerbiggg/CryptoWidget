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

    /// <summary>读取设置；文件缺失或解析失败返回默认设置（默认仅订阅 BTC）</summary>
    public AppSettings LoadSettings() => Load(SettingsPath, () => Default());

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
        PriceColorByTick = true,
        AutoStart = false,
        IsPinned = false,
        // 默认走本地代理（用户环境），可在设置中修改或清空改回系统代理
        Proxy = "http://127.0.0.1:7890",
    };

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
