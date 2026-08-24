using CryptoWidget.Common.Logger;
using Microsoft.Win32;
using System.Diagnostics;

namespace CryptoWidget.Common.AutoStart;

/// <summary>开机自启：通过注册表 HKCU\...\Run 实现</summary>
public class AutoStartService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "CryptoWidget";

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);
                var val = key?.GetValue(AppName) as string;
                return !string.IsNullOrEmpty(val);
            }
            catch (Exception ex)
            {
                LoggerHelper.Error("读取开机自启状态失败", ex);
                return false;
            }
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true);
            if (key == null) return;
            if (enabled)
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(exe))
                    key.SetValue(AppName, exe);
            }
            else
            {
                if (key.GetValue(AppName) != null)
                    key.DeleteValue(AppName, false);
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Error("设置开机自启失败", ex);
        }
    }
}
