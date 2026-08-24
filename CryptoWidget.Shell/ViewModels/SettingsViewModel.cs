using System.Collections.ObjectModel;
using CryptoWidget.Common.AutoStart;
using CryptoWidget.Common.Config;
using CryptoWidget.Models;
using Prism.Commands;
using Prism.Mvvm;

namespace CryptoWidget.Shell.ViewModels;

/// <summary>设置窗口 ViewModel：币种增删、显示开关、开机自启、代理；改动即时保存并广播 SettingsSaved</summary>
public class SettingsViewModel : BindableBase
{
    private readonly ConfigService _config;
    private readonly AutoStartService _autoStart;

    private AppSettings _settings;
    private bool _showIcon = true;
    private bool _showName = true;
    private bool _showPrice = true;
    private bool _showChange = true;
    private bool _showConnectionStatus = true;
    private bool _isVerticalLayout;
    private bool _priceColorByTick = true;
    private double _backgroundOpacity = 0.12;
    private string _fontFamilyName = "Microsoft YaHei UI";
    private double _fontSize = 12;
    private string _fontWeightName = "SemiBold";
    private bool _autoStartEnabled;
    private string _proxy = "";
    private string _newSymbol = "";
    private string _errorText = "";

    public SettingsViewModel(ConfigService config, AutoStartService autoStart)
    {
        _config = config;
        _autoStart = autoStart;

        AddCoinCommand = new DelegateCommand(AddCoin);
        RemoveCoinCommand = new DelegateCommand<CoinEditItem>(RemoveCoin);
        MoveUpCommand = new DelegateCommand<CoinEditItem>(MoveUp);
        MoveDownCommand = new DelegateCommand<CoinEditItem>(MoveDown);
        SaveCommand = new DelegateCommand(SaveAll);

        _settings = config.LoadSettings();
        foreach (var c in _settings.Coins)
        {
            var item = new CoinEditItem(c.Symbol, c.InstId, c.DecimalPlaces);
            item.DecimalPlacesChanged += (_, _) => Save();
            Coins.Add(item);
            item.LoadIconAsync();
        }

        // 直接回填 backing field，避免构造函数里触发 Save
        _showIcon = _settings.ShowIcon;
        _showName = _settings.ShowName;
        _showPrice = _settings.ShowPrice;
        _showChange = _settings.ShowChange;
        _showConnectionStatus = _settings.ShowConnectionStatus;
        _isVerticalLayout = _settings.IsVerticalLayout;
        _priceColorByTick = _settings.PriceColorByTick;
        _backgroundOpacity = _settings.BackgroundOpacity;
        _fontFamilyName = _settings.FontFamily;
        _fontSize = _settings.FontSize;
        _fontWeightName = _settings.FontWeight;
        _autoStartEnabled = _settings.AutoStart;
        _proxy = _settings.Proxy;
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

    /// <summary>价格颜色（大屏效果）：新价比上一笔高变绿、低变红</summary>
    public bool PriceColorByTick
    {
        get => _priceColorByTick;
        set { if (SetProperty(ref _priceColorByTick, value)) Save(); }
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
        _settings.PriceColorByTick = _priceColorByTick;
        _settings.BackgroundOpacity = _backgroundOpacity;
        _settings.FontFamily = _fontFamilyName;
        _settings.FontSize = _fontSize;
        _settings.FontWeight = _fontWeightName;
        _settings.AutoStart = _autoStartEnabled;
        _settings.Proxy = _proxy;
        _settings.Coins = Coins
            .Select(c => new CoinConfig(c.Symbol, c.InstId) { DecimalPlaces = c.ParseDecimalPlaces() })
            .ToList();
        _config.SaveSettings(_settings);
    }
}
