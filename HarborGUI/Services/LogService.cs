using System.IO;
using HarborGUI.Services.Interfaces;

namespace HarborGUI.Services;

/// <summary>
/// 日志服务：写入 Config/app.log，带时间戳和级别
/// </summary>
public class LogService : ILogService, IDisposable
{
    private readonly string _logPath;
    private readonly object _lock = new();
    private const int MaxLogLines = 5000; // 最多保留 5000 行，超出自动截断

    public LogService()
    {
        var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);

        _logPath = Path.Combine(configDir, "app.log");
        InitLogFile();
    }

    private void InitLogFile()
    {
        lock (_lock)
        {
            // 截断过大的日志（保留后半部分）
            if (File.Exists(_logPath))
            {
                var lines = File.ReadAllLines(_logPath);
                if (lines.Length > MaxLogLines)
                {
                    var keep = lines.Skip(lines.Length - MaxLogLines / 2).ToArray();
                    File.WriteAllLines(_logPath, keep);
                }
            }

            File.AppendAllText(_logPath,
                $"{Environment.NewLine}══════ 会话开始 {DateTime.Now:yyyy-MM-dd HH:mm:ss} ══════{Environment.NewLine}");
        }
    }

    public void Log(LogLevel level, string message)
    {
        lock (_lock)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level.ToString().ToUpper(),-7}] {message}";
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
    }

    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message) => Log(LogLevel.Error, message);

    public void Error(Exception ex, string? context = null)
    {
        var msg = context != null ? $"{context}: {ex}" : ex.ToString();
        Error(msg);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            File.AppendAllText(_logPath,
                $"══════ 会话结束 {DateTime.Now:yyyy-MM-dd HH:mm:ss} ══════{Environment.NewLine}");
        }
    }
}
