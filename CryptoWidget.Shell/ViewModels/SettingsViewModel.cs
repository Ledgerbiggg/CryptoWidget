using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using CryptoWidget.Common.AutoStart;
using CryptoWidget.Common.Config;
using CryptoWidget.Common.Logger;
using CryptoWidget.Models;
using CryptoWidget.Services.IService;
using CryptoWidget.Services.Service;
using Prism.Commands;
using Prism.Mvvm;

namespace CryptoWidget.Shell.ViewModels;

/// <summary>设置窗口 ViewModel：币种增删、显示开关、全局热键录制、开机自启、代理、版本更新检查；改动即时保存并广播 SettingsSaved</summary>
public class SettingsViewModel : BindableBase
{
    private readonly ConfigService _config;
    private readonly AutoStartService _autoStart;
    private readonly IUpdateService _update;

    private AppSettings _settings = null!; // 构造/Reload 时加载
    private bool _showIcon = true;
    private bool _showName = true;
    private bool _showPrice = true;
    private bool _showChange = true;
    private bool _showConnectionStatus = true;
    private bool _isVerticalLayout;
    /// <summary>价格颜色模式（0=黑 1=白 2=红绿）</summary>
    private PriceColorMode _priceColorMode = PriceColorMode.RedGreen;

    /// <summary>涨跌幅基准（0=无 1=当日+8 2=当日UTC 3=24h）</summary>
    private ChangeMode _changeMode = ChangeMode.Last24h;

    private double _backgroundOpacity = 0.12;
    private string _fontFamilyName = "Microsoft YaHei UI";
    private double _fontSize = 12;
    private string _fontWeightName = "SemiBold";
    private bool _autoStartEnabled;
    private string _proxy = "";
    private string _newSymbol = "";
    private string _errorText = "";
    /// <summary>显示/隐藏卡片热键（默认 Alt+1）</summary>
    private string _hotkeyModifier = "Alt";
    private string _hotkeyKey = "1";
    private bool _isRecording;
    private bool _isChecking;
    private bool _isUpdating;
    private string _updateStatus = "";
    private UpdateInfo? _latestInfo;
    private bool _hasUpdate;
    /// <summary>当前选中的外观方案 Id（下拉切换即应用）</summary>
    private string _activeProfileId = "";
    /// <summary>另存为时输入的新方案名</summary>
    private string _newProfileName = "";

    public SettingsViewModel(ConfigService config, AutoStartService autoStart, IUpdateService update)
    {
        _config = config;
        _autoStart = autoStart;
        _update = update;

        AddCoinCommand = new DelegateCommand(AddCoin);
        RemoveCoinCommand = new DelegateCommand<CoinEditItem>(RemoveCoin);
        MoveUpCommand = new DelegateCommand<CoinEditItem>(MoveUp);
        MoveDownCommand = new DelegateCommand<CoinEditItem>(MoveDown);
        SaveCommand = new DelegateCommand(SaveAll);
        SaveAsProfileCommand = new DelegateCommand(SaveAsProfile);
        DeleteProfileCommand = new DelegateCommand(DeleteProfile, CanDeleteProfile);
        RecordHotkeyCommand = new DelegateCommand(ToggleRecording);
        CheckUpdateCommand = new DelegateCommand(async () => await CheckUpdateAsync());
        Version = GetLocalVersion();

        Reload(); // 首次加载配置（单例 VM，之后每次打开设置窗口由窗口调用 Reload 同步最新配置）
    }

    /// <summary>重新从文件加载配置并重建编辑状态：单例 VM 必须与最新文件同步，
    /// 否则再次打开设置会显示旧值，保存时还会用旧快照覆盖主卡片/设置窗口刚保存的新改动</summary>
    public void Reload()
    {
        _settings = _config.LoadSettings();
        Profiles.Clear();
        foreach (var p in _settings.Profiles ?? new List<AppearanceProfile>())
            Profiles.Add(p);
        _activeProfileId = _settings.ActiveProfileId;
        RaisePropertyChanged(nameof(ActiveProfileId));
        DeleteProfileCommand.RaiseCanExecuteChanged();
        Coins.Clear();
        foreach (var c in _settings.Coins)
        {
            var item = new CoinEditItem(c.Symbol, c.InstId, c.DecimalPlaces);
            item.DecimalPlacesChanged += (_, _) => Save();
            Coins.Add(item);
            item.LoadIconAsync();
        }

        // 直接回填 backing field（不触发 Save），新窗口绑定会读取这些最新值
        _showIcon = _settings.ShowIcon;
        _showName = _settings.ShowName;
        _showPrice = _settings.ShowPrice;
        _showChange = _settings.ShowChange;
        _showConnectionStatus = _settings.ShowConnectionStatus;
        _isVerticalLayout = _settings.IsVerticalLayout;
        _priceColorMode = _settings.PriceColorMode;
        _changeMode = _settings.ChangeMode;
        _backgroundOpacity = _settings.BackgroundOpacity;
        _fontFamilyName = _settings.FontFamily;
        _fontSize = _settings.FontSize;
        _fontWeightName = _settings.FontWeight;
        _autoStartEnabled = _settings.AutoStart;
        _proxy = _settings.Proxy;
        var hotkey = _settings.ToggleHotkey ?? new HotkeyBinding { Modifier = "Alt", Key = "1" };
        _hotkeyModifier = hotkey.Modifier ?? "Alt";
        _hotkeyKey = hotkey.Key ?? "1";
        ErrorText = "";
    }

    /// <summary>编辑中的币种列表</summary>
    public ObservableCollection<CoinEditItem> Coins { get; } = [];

    public DelegateCommand AddCoinCommand { get; }
    public DelegateCommand<CoinEditItem> RemoveCoinCommand { get; }

    /// <summary>上下箭头调整币种顺序（首个不可上移、末个不可下移）</summary>
    public DelegateCommand<CoinEditItem> MoveUpCommand { get; }
    public DelegateCommand<CoinEditItem> MoveDownCommand { get; }

    /// <summary>保存按钮：显式落盘全部配置（含小数位/代理等失焦才提交的输入）</summary>
    public DelegateCommand SaveCommand { get; }

    /// <summary>外观方案列表（设置窗口下拉 / 托盘菜单共用）</summary>
    public ObservableCollection<AppearanceProfile> Profiles { get; } = [];

    /// <summary>当前选中的方案 Id（下拉切换即应用）</summary>
    public string ActiveProfileId
    {
        get => _activeProfileId;
        set { if (SetProperty(ref _activeProfileId, value)) SwitchProfile(value); }
    }

    /// <summary>另存为时输入的新方案名</summary>
    public string NewProfileName
    {
        get => _newProfileName;
        set => SetProperty(ref _newProfileName, value);
    }

    /// <summary>当前方案名（改名即写回当前方案）</summary>
    public string CurrentProfileName
    {
        get => _settings.Profiles?.FirstOrDefault(p => p.Id == _settings.ActiveProfileId)?.Name ?? "";
        set
        {
            var p = _settings.Profiles?.FirstOrDefault(x => x.Id == _settings.ActiveProfileId);
            if (p == null) return;
            var name = value.Trim();
            if (string.IsNullOrEmpty(name) || p.Name == name) return;
            p.Name = name;
            _config.SaveSettings(_settings);
            Reload(); // 重建 Profiles 集合，让下拉即时显示新名称
        }
    }

    /// <summary>另存为当前外观为新方案</summary>
    public DelegateCommand SaveAsProfileCommand { get; }

    /// <summary>删除当前方案（至少保留一个）</summary>
    public DelegateCommand DeleteProfileCommand { get; }

    /// <summary>录制热键开关（点击「录制」后等待捕获组合键）</summary>
    public DelegateCommand RecordHotkeyCommand { get; }

    /// <summary>检查更新 / 立即更新命令（自动检查发现新版后点击直接下载安装）</summary>
    public DelegateCommand CheckUpdateCommand { get; }

    /// <summary>本地程序版本号（三段，如 0.0.5）</summary>
    public string Version { get; }

    /// <summary>是否有可更新版本（自动检查发现新版后置 true）</summary>
    public bool HasUpdate
    {
        get => _hasUpdate;
        private set
        {
            if (SetProperty(ref _hasUpdate, value)) RaisePropertyChanged(nameof(CheckButtonText));
        }
    }

    /// <summary>按钮文字：有新版时「立即更新」，否则「检查更新」</summary>
    public string CheckButtonText => _hasUpdate ? "立即更新" : "检查更新";

    /// <summary>是否正在下载安装（更新中按钮禁用）</summary>
    public bool IsUpdating
    {
        get => _isUpdating;
        private set => SetProperty(ref _isUpdating, value);
    }

    /// <summary>检查/下载状态文字（含下载百分比）</summary>
    public string UpdateStatus
    {
        get => _updateStatus;
        private set => SetProperty(ref _updateStatus, value);
    }

    /// <summary>正在检查或下载（按钮禁用/状态展示）</summary>
    public bool IsChecking
    {
        get => _isChecking;
        private set => SetProperty(ref _isChecking, value);
    }

    /// <summary>保存成功后触发（窗口据此弹提示并自动关闭）</summary>
    public event EventHandler? Saved;

    public string NewSymbol
    {
        get => _newSymbol;
        set => SetProperty(ref _newSymbol, value);
    }

    /// <summary>操作提示（如重复添加、至少保留一个币种）</summary>
    public string ErrorText
    {
        get => _errorText;
        set => SetProperty(ref _errorText, value);
    }

    public bool ShowIcon
    {
        get => _showIcon;
        set { if (SetProperty(ref _showIcon, value)) Save(); }
    }

    public bool ShowName
    {
        get => _showName;
        set { if (SetProperty(ref _showName, value)) Save(); }
    }

    public bool ShowPrice
    {
        get => _showPrice;
        set { if (SetProperty(ref _showPrice, value)) Save(); }
    }

    public bool ShowChange
    {
        get => _showChange;
        set { if (SetProperty(ref _showChange, value)) Save(); }
    }

    /// <summary>是否显示全局连接状态圆点</summary>
    public bool ShowConnectionStatus
    {
        get => _showConnectionStatus;
        set { if (SetProperty(ref _showConnectionStatus, value)) Save(); }
    }

    /// <summary>币种布局：false=横向，true=竖向（0=横向，1=竖向）</summary>
    public int LayoutIndex
    {
        get => _isVerticalLayout ? 1 : 0;
        set
        {
            var vertical = value == 1;
            if (SetProperty(ref _isVerticalLayout, vertical))
                Save();
        }
    }

    /// <summary>价格颜色下拉（0=黑色 1=白色 2=红绿）</summary>
    public int PriceColorModeIndex
    {
        get => (int)_priceColorMode;
        set
        {
            var mode = (PriceColorMode)value;
            if (SetProperty(ref _priceColorMode, mode))
                Save();
        }
    }

    /// <summary>涨跌幅下拉（0=无 1=当日+8时区 2=当日UTC 3=24小时）</summary>
    public int ChangeModeIndex
    {
        get => (int)_changeMode;
        set
        {
            var mode = (ChangeMode)value;
            if (SetProperty(ref _changeMode, mode))
                Save();
        }
    }

    /// <summary>卡片背景不透明度（越小越透明）</summary>
    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        set { if (SetProperty(ref _backgroundOpacity, value)) Save(); }
    }

    /// <summary>可选字体列表</summary>
    public string[] FontFamilyOptions { get; } =
        ["Microsoft YaHei", "Microsoft YaHei UI", "SimSun", "DengXian", "Consolas", "Segoe UI", "Arial"];

    /// <summary>可选字重列表</summary>
    public string[] FontWeightOptions { get; } = ["Normal", "SemiBold", "Bold"];

    /// <summary>当前字体（下拉框选择）</summary>
    public string FontFamilyName
    {
        get => _fontFamilyName;
        set { if (SetProperty(ref _fontFamilyName, value)) Save(); }
    }

    /// <summary>字号</summary>
    public double FontSize
    {
        get => _fontSize;
        set { if (SetProperty(ref _fontSize, value)) Save(); }
    }

    /// <summary>当前字重（下拉框选择）</summary>
    public string FontWeightName
    {
        get => _fontWeightName;
        set { if (SetProperty(ref _fontWeightName, value)) Save(); }
    }

    /// <summary>开机自启：写注册表并保存配置</summary>
    public bool AutoStart
    {
        get => _autoStartEnabled;
        set
        {
            if (SetProperty(ref _autoStartEnabled, value))
            {
                _autoStart.SetEnabled(value);
                _settings.AutoStart = value;
                Save();
            }
        }
    }

    /// <summary>代理地址（失焦保存；留空走系统代理/环境变量）</summary>
    public string Proxy
    {
        get => _proxy;
        set { if (SetProperty(ref _proxy, value)) Save(); }
    }

    /// <summary>热键修饰键（如 Alt / Ctrl+Shift）</summary>
    public string HotkeyModifier
    {
        get => _hotkeyModifier;
        set { if (SetProperty(ref _hotkeyModifier, value)) { RaisePropertyChanged(nameof(HotkeyDisplay)); Save(); } }
    }

    /// <summary>热键触发按键（如 1 / Space / F1）</summary>
    public string HotkeyKey
    {
        get => _hotkeyKey;
        set { if (SetProperty(ref _hotkeyKey, value)) { RaisePropertyChanged(nameof(HotkeyDisplay)); Save(); } }
    }

    /// <summary>是否正在录制热键（录制态下界面提示“请按键…”）</summary>
    public bool IsRecording
    {
        get => _isRecording;
        set => SetProperty(ref _isRecording, value);
    }

    /// <summary>热键展示文本，如 “Alt + 1”</summary>
    public string HotkeyDisplay
    {
        get
        {
            var mod = _hotkeyModifier.Trim();
            var key = _hotkeyKey.Trim();
            if (string.IsNullOrEmpty(mod) && string.IsNullOrEmpty(key)) return "未设置";
            if (string.IsNullOrEmpty(mod)) return key;
            return $"{mod} + {key}";
        }
    }

    /// <summary>切换录制状态（点击「录制」/「停止」）</summary>
    private void ToggleRecording()
    {
        IsRecording = !IsRecording;
    }

    /// <summary>打开设置时自动检查版本：不弹窗，结果展示在状态栏；发现新版时按钮变为「立即更新」</summary>
    public async Task AutoCheckAtOpenAsync()
    {
        if (IsChecking) return;
        IsChecking = true;
        UpdateStatus = "正在检查更新…";
        try
        {
            await CheckAndShowAsync();
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>检查更新按钮入口：已发现新版则直接下载安装，否则手动检查一次</summary>
    private async Task CheckUpdateAsync()
    {
        if (IsChecking) return;
        if (_hasUpdate && _latestInfo is not null)
        {
            await UpdateAsync(_latestInfo);
            return;
        }
        await AutoCheckAtOpenAsync();
    }

    /// <summary>拉取最新版本并更新状态栏：区分「发现新版 / 已是最新 / 检查失败」</summary>
    private async Task CheckAndShowAsync()
    {
        var info = await _update.CheckForUpdateAsync();
        if (info == null)
        {
            UpdateStatus = "检查失败：无法访问 GitHub（请检查网络/代理）";
            return;
        }

        if (!UpdateService.IsNewer(info.Version, Version))
        {
            UpdateStatus = $"已是最新版本（v{Version}）";
            return;
        }

        _latestInfo = info;
        HasUpdate = true;
        UpdateStatus = $"发现新版本 v{info.Version}，点击「立即更新」";
    }

    /// <summary>下载安装包（状态栏显示进度）→ 启动安装程序 → 结束进程完成升级</summary>
    private async Task UpdateAsync(UpdateInfo info)
    {
        IsUpdating = true;
        UpdateStatus = "正在下载安装包… 0%";
        try
        {
            var progress = new Progress<double>(p => UpdateStatus = $"正在下载安装包… {p * 100:F0}%");
            var path = await _update.DownloadAsync(info, progress);
            UpdateStatus = "下载完成，即将启动安装…";
            _update.LaunchInstaller(path);
            // 安装程序需覆盖正在运行的 exe：直接结束进程（托盘图标由系统回收），安装向导接管后续
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            LoggerHelper.Error("更新流程异常", ex);
            UpdateStatus = "更新失败";
            _ = MessageBox.Show($"下载更新失败：{ex.Message}", "更新失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsUpdating = false;
        }
    }

    /// <summary>本地版本取入口程序集（Shell）版本号，三段</summary>
    private static string GetLocalVersion()
    {
        var v = Assembly.GetEntryAssembly()?.GetName().Version;
        return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>由窗口 PreviewKeyDown 调用：捕获组合键（必须含修饰键，Esc 取消）</summary>
    public void CaptureHotkey(ModifierKeys modifiers, Key key)
    {
        if (key == Key.Escape)
        {
            IsRecording = false;
            return;
        }

        var nonModifier = GetNonModifierKey(key);
        if (nonModifier == null) return; // 仅修饰键，继续等待
        if (modifiers == ModifierKeys.None) return; // 必须有修饰键，避免单键误触发

        IsRecording = false;
        HotkeyModifier = ModifiersToString(modifiers);
        HotkeyKey = nonModifier;
    }

    /// <summary>过滤纯修饰键按键，其余按键转统一名称（如 Key.D1 → “1”）</summary>
    private static string? GetNonModifierKey(Key key)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.System)
            return null;

        return key switch
        {
            Key.Space => "Space",
            Key.OemTilde => "`",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.Tab => "Tab",
            Key.Enter => "Enter",
            Key.Back => "Back",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            _ when key >= Key.D0 && key <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
            _ when key >= Key.NumPad0 && key <= Key.NumPad9 => ((int)key - (int)Key.NumPad0).ToString(),
            _ when key >= Key.A && key <= Key.Z => key.ToString(),
            _ when key >= Key.F1 && key <= Key.F24 => key.ToString(),
            _ => key.ToString(),
        };
    }

    /// <summary>WPF 修饰键枚举转字符串（如 Ctrl+Alt）</summary>
    private static string ModifiersToString(ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
        return string.Join("+", parts);
    }

    /// <summary>新增币种：输入代码（ETH）自动拼 ETH-USDT，也支持完整交易对（PEPE-USDT）</summary>
    private void AddCoin()
    {
        ErrorText = "";
        var input = NewSymbol.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(input)) return;

        string symbol, instId;
        if (input.Contains('-'))
        {
            instId = input;
            symbol = input.Split('-')[0];
        }
        else
        {
            symbol = input;
            instId = input + "-USDT";
        }

        if (Coins.Any(c => c.InstId == instId))
        {
            ErrorText = $"「{instId}」已在列表中";
            return;
        }

        var item = new CoinEditItem(symbol, instId);
        item.DecimalPlacesChanged += (_, _) => Save();
        Coins.Add(item);
        item.LoadIconAsync();
        NewSymbol = "";
        Save();
    }

    /// <summary>删除币种：至少保留一个，避免空订阅</summary>
    private void RemoveCoin(CoinEditItem item)
    {
        if (item == null) return;
        if (Coins.Count <= 1)
        {
            ErrorText = "至少保留一个币种";
            return;
        }
        Coins.Remove(item);
        Save();
    }

    /// <summary>上移一个位置（列表首位不再上移），顺序即主卡片展示顺序</summary>
    private void MoveUp(CoinEditItem item)
    {
        if (item == null) return;
        var i = Coins.IndexOf(item);
        if (i <= 0) return;
        Coins.Move(i, i - 1);
        Save();
    }

    /// <summary>下移一个位置（列表末尾不再下移），顺序即主卡片展示顺序</summary>
    private void MoveDown(CoinEditItem item)
    {
        if (item == null) return;
        var i = Coins.IndexOf(item);
        if (i < 0 || i >= Coins.Count - 1) return;
        Coins.Move(i, i + 1);
        Save();
    }

    /// <summary>保存按钮入口：落盘并通知窗口弹提示/关闭</summary>
    public void SaveAll()
    {
        Save();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>关闭窗口兜底：静默保存，不弹提示（避免关闭时重复打扰）</summary>
    public void SaveOnClose() => Save();

    private void Save()
    {
        _settings.ShowIcon = _showIcon;
        _settings.ShowName = _showName;
        _settings.ShowPrice = _showPrice;
        _settings.ShowChange = _showChange;
        _settings.ShowConnectionStatus = _showConnectionStatus;
        _settings.IsVerticalLayout = _isVerticalLayout;
        _settings.PriceColorMode = _priceColorMode;
        _settings.ChangeMode = _changeMode;
        _settings.BackgroundOpacity = _backgroundOpacity;
        _settings.FontFamily = _fontFamilyName;
        _settings.FontSize = _fontSize;
        _settings.FontWeight = _fontWeightName;
        _settings.AutoStart = _autoStartEnabled;
        _settings.Proxy = _proxy;
        _settings.ToggleHotkey = new HotkeyBinding { Modifier = _hotkeyModifier, Key = _hotkeyKey };
        _settings.Coins = Coins
            .Select(c => new CoinConfig(c.Symbol, c.InstId) { DecimalPlaces = c.ParseDecimalPlaces() })
            .ToList();
        SyncActiveProfile();
        _config.SaveSettings(_settings);
    }

    /// <summary>下拉切换方案：把方案外观写回文件并广播（主卡片即时刷新），再 Reload 同步编辑态</summary>
    private void SwitchProfile(string id)
    {
        if (Profiles.FirstOrDefault(x => x.Id == id) == null) return;
        _config.ApplyProfile(id);
        Reload();
    }

    /// <summary>把当前 VM 外观字段同步写回激活的方案（改动即覆盖当前方案）</summary>
    private void SyncActiveProfile()
    {
        var active = _settings.Profiles?.FirstOrDefault(p => p.Id == _settings.ActiveProfileId);
        if (active == null) return;
        active.ShowIcon = _showIcon;
        active.ShowName = _showName;
        active.ShowPrice = _showPrice;
        active.ShowChange = _showChange;
        active.ShowConnectionStatus = _showConnectionStatus;
        active.IsVerticalLayout = _isVerticalLayout;
        active.PriceColorMode = _priceColorMode;
        active.ChangeMode = _changeMode;
        active.BackgroundOpacity = _backgroundOpacity;
        active.FontFamily = _fontFamilyName;
        active.FontSize = _fontSize;
        active.FontWeight = _fontWeightName;
    }

    /// <summary>另存为：以当前外观复制出一个新方案并激活（外观不变，仅切换存档目标）</summary>
    private void SaveAsProfile()
    {
        var name = (_newProfileName ?? "").Trim();
        if (string.IsNullOrEmpty(name)) name = $"方案{Profiles.Count + 1}";
        if (Profiles.Any(p => p.Name == name))
        {
            ErrorText = $"已存在方案「{name}」";
            return;
        }
        var p = AppearanceProfile.FromSettings(_settings, name);
        Profiles.Add(p);
        _settings.Profiles.Add(p);
        _settings.ActiveProfileId = p.Id;
        _activeProfileId = p.Id;
        RaisePropertyChanged(nameof(ActiveProfileId));
        _config.SaveSettings(_settings);
        NewProfileName = "";
        ErrorText = "";
    }

    /// <summary>删除当前方案，自动切到首个余下方案（至少保留一个）</summary>
    private void DeleteProfile()
    {
        if (Profiles.Count <= 1)
        {
            ErrorText = "至少保留一个方案";
            return;
        }
        var cur = Profiles.FirstOrDefault(p => p.Id == _activeProfileId);
        if (cur == null) return;
        Profiles.Remove(cur);
        _settings.Profiles.Remove(cur);
        _config.SaveSettings(_settings); // 先落盘移除
        var next = Profiles[0];
        SwitchProfile(next.Id);          // 再应用下一个并 Reload
    }

    /// <summary>删除命令可用性：仅当存在多个方案时可删</summary>
    private bool CanDeleteProfile() => Profiles.Count > 1;
}
