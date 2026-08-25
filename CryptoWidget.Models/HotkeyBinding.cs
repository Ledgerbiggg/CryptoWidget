namespace CryptoWidget.Models;

/// <summary>全局热键绑定（修饰键 + 按键）</summary>
public class HotkeyBinding
{
    /// <summary>修饰键组合，如 "Alt"、"Ctrl+Shift"</summary>
    public string Modifier { get; set; } = "Alt";

    /// <summary>触发按键，如 "1"、"Space"、"F1"</summary>
    public string Key { get; set; } = "1";
}
