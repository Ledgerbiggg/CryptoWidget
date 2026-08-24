using System.IO;
using System.Text;

namespace CryptoWidget.Common.Logger;

/// <summary>轻量文件日志（写入 %AppData%\CryptoWidget\logs，按日滚动）</summary>
public static class LoggerHelper
{
    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoWidget", "logs");

    private static readonly object Lock = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        var sb = new StringBuilder();
        sb.Append(message);
        if (ex != null)
        {
            sb.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
#if DEBUG
            sb.Append("\n").Append(ex.StackTrace);
#endif
        }
        Write("ERROR", sb.ToString());
    }

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var file = Path.Combine(LogDir, $"log-{DateTime.Now:yyyyMMdd}.txt");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            lock (Lock)
            {
                File.AppendAllText(file, line + Environment.NewLine);
            }
        }
        catch
        {
            // 日志失败不应影响主程序
        }
    }
}
