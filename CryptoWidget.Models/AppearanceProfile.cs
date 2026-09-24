namespace CryptoWidget.Models;

/// <summary>外观配置方案：覆盖币种列表、外观字段与窗口位置，可命名存档、随时切换。
/// 代理/热键/开机自启等全局偏好不在方案范围内，切换时保持不变</summary>
public class AppearanceProfile
{
    /// <summary>默认方案的固定 Id</summary>
    public const string DefaultId = "default";

    /// <summary>方案唯一 Id（自动生成，默认方案固定为 DefaultId）</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>方案显示名（如 迷你 / 标准 / 大屏）</summary>
    public string Name { get; set; } = "方案";

    /// <summary>本方案订阅的币种列表（切换方案时覆盖顶层生效列表）；
    /// null 表示旧配置尚未迁移，由 ConfigService 用当时的全局列表填充</summary>
    public List<CoinConfig>? Coins { get; set; }

    public bool ShowIcon { get; set; } = true;
    public bool ShowName { get; set; } = true;
    public bool ShowPrice { get; set; } = true;
    public bool ShowChange { get; set; } = true;
    public bool ShowConnectionStatus { get; set; } = true;
    public bool IsVerticalLayout { get; set; } = false;
    public PriceColorMode PriceColorMode { get; set; } = PriceColorMode.RedGreen;
    public ChangeMode ChangeMode { get; set; } = ChangeMode.Last24h;
    public double BackgroundOpacity { get; set; } = 0.12;
    public string FontFamily { get; set; } = "Microsoft YaHei UI";
    public double FontSize { get; set; } = 12;
    public string FontWeight { get; set; } = "SemiBold";

    /// <summary>窗口位置记忆（随方案保存；null=尚未记录，切换到此方案时保持窗口原位）</summary>
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    /// <summary>从当前 AppSettings 顶层外观字段构建一个方案副本（用于「另存为」），币种列表一并拷入</summary>
    public static AppearanceProfile FromSettings(AppSettings s, string? name = null) => new()
    {
        Name = name ?? "方案",
        Coins = s.Coins.Select(c => c.Clone()).ToList(),
        ShowIcon = s.ShowIcon,
        ShowName = s.ShowName,
        ShowPrice = s.ShowPrice,
        ShowChange = s.ShowChange,
        ShowConnectionStatus = s.ShowConnectionStatus,
        IsVerticalLayout = s.IsVerticalLayout,
        PriceColorMode = s.PriceColorMode,
        ChangeMode = s.ChangeMode,
        BackgroundOpacity = s.BackgroundOpacity,
        FontFamily = s.FontFamily,
        FontSize = s.FontSize,
        FontWeight = s.FontWeight,
        WindowLeft = s.WindowLeft,
        WindowTop = s.WindowTop,
    };

    /// <summary>把本方案字段写回 AppSettings 顶层（用于切换生效）</summary>
    public void CopyTo(AppSettings s)
    {
        // 币种跟随方案切换：深拷贝覆盖顶层生效列表（空列表视为配置异常，保持顶层不动避免切到全空）
        if (Coins is { Count: > 0 })
            s.Coins = Coins.Select(c => c.Clone()).ToList();

        s.ShowIcon = ShowIcon;
        s.ShowName = ShowName;
        s.ShowPrice = ShowPrice;
        s.ShowChange = ShowChange;
        s.ShowConnectionStatus = ShowConnectionStatus;
        s.IsVerticalLayout = IsVerticalLayout;
        s.PriceColorMode = PriceColorMode;
        s.ChangeMode = ChangeMode;
        s.BackgroundOpacity = BackgroundOpacity;
        s.FontFamily = FontFamily;
        s.FontSize = FontSize;
        s.FontWeight = FontWeight;

        // 位置：仅当方案记录过位置时才覆盖顶层，避免切换到未定位的方案时窗口跳回默认位置
        if (WindowLeft is double l && WindowTop is double t)
        {
            s.WindowLeft = l;
            s.WindowTop = t;
        }
    }
}
