using System.Diagnostics;
using System.IO;
using System.Text;
using HarborGUI.Services.Interfaces;

namespace HarborGUI.Services;

/// <summary>
/// 进程运行服务：通过 PowerShell 执行命令序列
/// </summary>
public class ProcessRunnerService : IProcessRunnerService
{
    private readonly ILogService _log;

    public ProcessRunnerService(ILogService log)
    {
        _log = log;
    }

    public async Task<ProcessRunResult> RunAsync(
        string command,
        string workingDirectory,
        int timeoutMs = 0,
        CancellationToken cancellationToken = default)
    {
        var result = new ProcessRunResult();

        _log.Info($"执行命令 [目录: {workingDirectory}]: {Truncate(command, 200)}");
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            // 使用 Base64 编码避免特殊字符（反斜杠、引号等）转义问题
            Arguments = $"-NoProfile -NonInteractive -EncodedCommand {EncodeCommand(command)}",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // 传递环境变量
        psi.Environment["PYTHONUTF8"] = "1";

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();

            // 异步读取输出
            var readOut = process.StandardOutput.ReadToEndAsync();
            var readErr = process.StandardError.ReadToEndAsync();

            if (timeoutMs > 0)
            {
                // 使用 Task.WhenAny 实现超时，避免 WaitForExit + ReadToEndAsync 死锁
                var processTask = process.WaitForExitAsync(cancellationToken);
                var timeoutTask = Task.Delay(timeoutMs, cancellationToken);
                var completed = await Task.WhenAny(processTask, timeoutTask) == processTask;

                if (!completed)
                {
                    process.Kill(entireProcessTree: true);
                    result.TimedOut = true;
                }
            }
            else
            {
                await process.WaitForExitAsync(cancellationToken);
            }

            // 等待输出读取完成
            await Task.WhenAll(readOut, readErr);

            result.ExitCode = process.ExitCode;
            result.StandardOutput = readOut.Result;
            result.StandardError = readErr.Result;

            // 记录执行结果
            if (result.ExitCode != 0)
                _log.Warning($"命令退出码={result.ExitCode}, stderr: {Truncate(result.StandardError, 200)}");
            else
                _log.Debug($"命令完成, 退出码=0");
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            result.TimedOut = true;
            _log.Warning("命令被取消");
        }
        catch (Exception ex)
        {
            result.StandardError = ex.Message;
            result.ExitCode = -1;
            _log.Error(ex, "命令执行异常");
        }

        return result;
    }

    public async Task<List<ProcessRunResult>> RunSequenceAsync(
        List<string> commands,
        string workingDirectory,
        int timeoutMs = 0,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProcessRunResult>();

        if (commands.Count == 0)
            return results;

        // 将所有命令用 ; 串联在同一个 PowerShell 会话中执行
        // 这样可以保留环境变量设置（如 $env:PYTHONUTF8 = "1"）
        var combinedCommand = string.Join("; ", commands);

        var result = await RunAsync(combinedCommand, workingDirectory, timeoutMs, cancellationToken);

        // 对于组合命令，我们只记录整体结果
        // 如果某条命令失败，PowerShell 的 $LASTEXITCODE 会反映最后一条命令的状态
        results.Add(result);

        return results;
    }

    /// <summary>
    /// 将命令编码为 Base64（Unicode），解决 PowerShell -Command 的转义问题
    /// </summary>
    private static string EncodeCommand(string command)
    {
        var bytes = Encoding.Unicode.GetBytes(command);
        return Convert.ToBase64String(bytes);
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "(空)";
        var clean = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return clean.Length <= maxLen ? clean : clean[..maxLen] + "…";
    }
}
