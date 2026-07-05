namespace HarborGUI.Services.Interfaces;

/// <summary>
/// 日志级别
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// 日志服务接口
/// </summary>
public interface ILogService
{
    void Log(LogLevel level, string message);
    void Debug(string message);
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    void Error(Exception ex, string? context = null);
}
