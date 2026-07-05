namespace HarborGUI.Services.Interfaces;

/// <summary>
/// 进程运行结果
/// </summary>
public class ProcessRunResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public bool TimedOut { get; set; }
}

/// <summary>
/// 进程运行服务：在后台启动终端执行命令
/// </summary>
public interface IProcessRunnerService
{
    /// <summary>
    /// 在指定工作目录中运行一条命令
    /// </summary>
    /// <param name="command">要执行的命令</param>
    /// <param name="workingDirectory">工作目录</param>
    /// <param name="timeoutMs">超时时间（毫秒），0 表示不限</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<ProcessRunResult> RunAsync(
        string command,
        string workingDirectory,
        int timeoutMs = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按顺序运行多条命令（同一个工作目录，环境变量会在命令间保留）
    /// </summary>
    Task<List<ProcessRunResult>> RunSequenceAsync(
        List<string> commands,
        string workingDirectory,
        int timeoutMs = 0,
        CancellationToken cancellationToken = default);
}
