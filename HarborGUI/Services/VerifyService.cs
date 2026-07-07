using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HarborGUI.Models;
using HarborGUI.Services.Interfaces;

namespace HarborGUI.Services;

/// <summary>
/// 质检编排服务：核心业务逻辑
/// 按规则集顺序对任务执行完整质检流程
/// </summary>
public partial class VerifyService : IVerifyService
{
    private readonly IProcessRunnerService _processRunner;
    private readonly IReportService _reportService;
    private readonly IVariableResolver _variableResolver;
    private readonly ILogService _log;

    public VerifyService(IProcessRunnerService processRunner, IReportService reportService, IVariableResolver variableResolver, ILogService log)
    {
        _processRunner = processRunner;
        _reportService = reportService;
        _variableResolver = variableResolver;
        _log = log;
    }

    public async Task<TaskCheckReport> VerifyTaskAsync(
        TaskItem task,
        List<VerifyRule> rules,
        IProgress<string> progress, CancellationToken cancellationToken = default, TimeSpan? elapsed = null, IProgress<CheckResult>? ruleProgress = null)
    {
        var report = new TaskCheckReport { TaskName = task.TaskName };
        var taskSw = System.Diagnostics.Stopwatch.StartNew();
        var taskDir = DirectoryHelper.UnwrapNestedFolder(task.ExtractedPath ?? "");
        if (!string.IsNullOrEmpty(taskDir) && !Directory.Exists(taskDir)) Directory.CreateDirectory(taskDir);
        var logPath = Path.Combine(taskDir, $"质检执行日志_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        task.LogPath = logPath;
        using var taskLog = new StreamWriter(logPath, false, Encoding.UTF8) { AutoFlush = true };
        taskLog.WriteLine($"══════ 质检开始: {task.TaskName}  {DateTime.Now:yyyy-MM-dd HH:mm:ss} ══════");
        taskLog.WriteLine($"工作目录: {taskDir}");
        taskLog.WriteLine($"规则数量: {rules.Count}");
        taskLog.WriteLine();

        _log.Info($"开始质检: {task.TaskName}, 规则数={rules.Count}");

        if (string.IsNullOrEmpty(taskDir) || !Directory.Exists(taskDir))
        {
            report.CheckResults.Add(new CheckResult
            {
                RuleName = "解压",
                Passed = false,
                Detail = "任务目录不存在，无法执行质检"
            });
            return report;
        }

        for (int i = 0; i < rules.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rule = rules[i];
            progress.Report($"执行规则 [{i + 1}/{rules.Count}]: {rule.RuleName}");
            taskLog.WriteLine($"── 规则 [{i + 1}/{rules.Count}]: {rule.RuleName} ──");

            var checkResult = new CheckResult { RuleName = rule.RuleName }; var ruleSw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // 构造运行时变量（在整个规则验证过程中可用）
                var runtimeVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Zip_Path"] = task.FullPath
                };

                // 阶段1：解析变量 + 运行命令
                if (rule.Commands.Count > 0)
                {
                    var resolvedCommands = _variableResolver.ResolveAll(rule.Commands, taskDir, runtimeVars);
                    _log.Debug($"规则 [{rule.RuleName}] 解析后命令: {string.Join(" | ", resolvedCommands)}");
                    taskLog.WriteLine($"执行命令: {string.Join("; ", resolvedCommands)}");
                    var cmdResults = await _processRunner.RunSequenceAsync(
                        resolvedCommands, taskDir, cancellationToken: cancellationToken);

                    // 使用最后一条命令的退出码进行判断
                    var lastResult = cmdResults.Last();
                    checkResult.ActualExitCode = lastResult.ExitCode;
                    // 报告用短文本，日志保留完整输出
                    var rawDetail = lastResult.TimedOut ? "命令超时" :
                        string.IsNullOrEmpty(lastResult.StandardError)
                            ? lastResult.StandardOutput.Trim()
                            : lastResult.StandardError.Trim();
                    checkResult.Detail = TruncateDetail(rawDetail);

                    int expectedExitCode = int.TryParse(rule.ExitCode, out var ec) ? ec : 0;

                    taskLog.WriteLine($"退出码: {lastResult.ExitCode} (期望: {expectedExitCode})");
                    if (!string.IsNullOrWhiteSpace(lastResult.StandardOutput))
                        taskLog.WriteLine($"标准输出:\n{lastResult.StandardOutput.Trim()}");
                    if (!string.IsNullOrWhiteSpace(lastResult.StandardError))
                        taskLog.WriteLine($"标准错误:\n{lastResult.StandardError.Trim()}");

                    if (lastResult.ExitCode != expectedExitCode)
                    {
                        checkResult.Passed = false;
                        checkResult.Detail = $"退出码不匹配: 期望 {expectedExitCode}, 实际 {lastResult.ExitCode}. {checkResult.Detail}";
                        _log.Warning($"规则 [{rule.RuleName}] 失败: {checkResult.Detail}");
                        checkResult.Elapsed = ruleSw.Elapsed.TotalSeconds < 1 ? "<1s" : ruleSw.Elapsed.TotalMinutes >= 1 ? $"{(int)ruleSw.Elapsed.TotalMinutes}m{ruleSw.Elapsed.Seconds}s" : $"{ruleSw.Elapsed.Seconds}s"; report.CheckResults.Add(checkResult); ruleProgress?.Report(checkResult);
                        taskLog.WriteLine($"结果: ❌ 失败 - {checkResult.Detail}");
                        taskLog.WriteLine();
                        if (!rule.FailContinue) { taskLog.WriteLine("⚠ 遇错即停 (FailContinue=false)"); _log.Warning($"规则 [{rule.RuleName}] FailContinue=false，停止"); break; }
                        continue;
                    }
                }

                // 第二阶段：运行验证脚本（如果配置了）
                if (!string.IsNullOrWhiteSpace(rule.VerifyScript))
                {
                    // 脚本路径相对于 Config 目录解析（支持 ${Zip_Path} 等变量和参数）
                    var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                    var resolvedScript = _variableResolver.Resolve(rule.VerifyScript, taskDir, runtimeVars);
                    var spaceIdx = resolvedScript.IndexOf(' ');
                    string scriptPart, argsPart;
                    if (spaceIdx >= 0)
                    {
                        scriptPart = resolvedScript[..spaceIdx];
                        argsPart = resolvedScript[(spaceIdx + 1)..].TrimStart();
                    }
                    else
                    {
                        scriptPart = resolvedScript;
                        argsPart = "";
                    }
                    var relativePath = scriptPart.TrimStart('.', '/', '\\');
                    var scriptPath = Path.Combine(configDir, relativePath);
                    if (!File.Exists(scriptPath))
                    {
                        checkResult.Passed = false;
                        checkResult.Detail = $"验证脚本不存在";
                        _log.Warning($"验证脚本不存在: {scriptPath}");
                        checkResult.Elapsed = ruleSw.Elapsed.TotalSeconds < 1 ? "<1s" : ruleSw.Elapsed.TotalMinutes >= 1 ? $"{(int)ruleSw.Elapsed.TotalMinutes}m{ruleSw.Elapsed.Seconds}s" : $"{ruleSw.Elapsed.Seconds}s"; report.CheckResults.Add(checkResult); ruleProgress?.Report(checkResult);
                        taskLog.WriteLine($"结果: ❌ 失败 - 脚本不存在: {scriptPath}");
                        taskLog.WriteLine();
                        if (!rule.FailContinue) { taskLog.WriteLine("⚠ 遇错即停 (FailContinue=false)"); break; }
                        continue;
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = string.IsNullOrEmpty(argsPart) ? $"\"{scriptPath}\"" : $"\"{scriptPath}\" {argsPart}",
                        WorkingDirectory = taskDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };
                    psi.Environment["PYTHONUTF8"] = "1";

                    var logCmd = string.IsNullOrEmpty(argsPart) ? scriptPath : $"{scriptPath} {argsPart}";
                    _log.Debug($"运行验证脚本: {logCmd}, 工作目录: {taskDir}");
                    taskLog.WriteLine($"验证脚本: {logCmd}");

                    using var process = Process.Start(psi);
                    if (process == null)
                    {
                        checkResult.Passed = false;
                        checkResult.Detail = "无法启动验证脚本";
                        _log.Error("无法启动 Python 验证脚本");
                        checkResult.Elapsed = ruleSw.Elapsed.TotalSeconds < 1 ? "<1s" : ruleSw.Elapsed.TotalMinutes >= 1 ? $"{(int)ruleSw.Elapsed.TotalMinutes}m{ruleSw.Elapsed.Seconds}s" : $"{ruleSw.Elapsed.Seconds}s"; report.CheckResults.Add(checkResult); ruleProgress?.Report(checkResult);
                        taskLog.WriteLine($"脚本执行异常 - {checkResult.Detail}");
                        taskLog.WriteLine();
                        if (!rule.FailContinue) { taskLog.WriteLine("⚠ 遇错即停 (FailContinue=false)"); break; }
                        continue;
                    }

                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync(cancellationToken);

                    var trimmedOutput = output.Trim();
                    _log.Debug($"脚本输出: '{trimmedOutput}', stderr: '{error.Trim()}', 退出码: {process.ExitCode}");
                    taskLog.WriteLine($"脚本退出码: {process.ExitCode}");
                    taskLog.WriteLine($"脚本输出: {trimmedOutput}");
                    if (!string.IsNullOrWhiteSpace(error))
                        taskLog.WriteLine($"脚本错误: {error.Trim()}");

                    // 脚本退出码非 0 视为脚本内部出错
                    if (process.ExitCode != 0)
                    {
                        checkResult.Passed = false;
                        checkResult.VerifyOutput = trimmedOutput;
                        checkResult.Detail = $"验证脚本异常退出(退出码={process.ExitCode}): {trimmedOutput}";
                        _log.Warning($"验证脚本退出码={process.ExitCode}, output='{trimmedOutput}'");
                        checkResult.Elapsed = ruleSw.Elapsed.TotalSeconds < 1 ? "<1s" : ruleSw.Elapsed.TotalMinutes >= 1 ? $"{(int)ruleSw.Elapsed.TotalMinutes}m{ruleSw.Elapsed.Seconds}s" : $"{ruleSw.Elapsed.Seconds}s"; report.CheckResults.Add(checkResult); ruleProgress?.Report(checkResult);
                        taskLog.WriteLine($"脚本执行异常 - {checkResult.Detail}");
                        taskLog.WriteLine();
                        if (!rule.FailContinue) { taskLog.WriteLine("⚠ 遇错即停 (FailContinue=false)"); break; }
                        continue;
                    }

                    checkResult.VerifyOutput = trimmedOutput;
                    checkResult.Passed = MatchVerifyResult(trimmedOutput, rule.MatchResult);
                    _log.Debug($"匹配结果: output='{trimmedOutput}' vs expected='{rule.MatchResult}' → {(checkResult.Passed ? "通过" : "失败")}");
                    checkResult.Detail = checkResult.Passed
                        ? $"验证通过: {trimmedOutput} (期望: {rule.MatchResult})"
                        : $"验证失败: {trimmedOutput} (期望: {rule.MatchResult})";
                }
                else
                {
                    // 没有验证脚本，命令退出码匹配即视为通过
                    checkResult.Passed = true;
                    if (string.IsNullOrEmpty(checkResult.Detail))
                        checkResult.Detail = "通过";
                }
            }
            catch (OperationCanceledException)
            {
                checkResult.Passed = false;
                checkResult.Detail = "已取消";
                _log.Warning($"任务 [{task.TaskName}] 规则 [{rule.RuleName}] 被取消");
            }
            catch (Exception ex)
            {
                checkResult.Passed = false;
                checkResult.Detail = $"执行异常: {ex.Message}";
                _log.Error(ex, $"任务 [{task.TaskName}] 规则 [{rule.RuleName}] 异常");
            }

            // 自定义输出格式覆写
            if (!string.IsNullOrEmpty(rule.ResultFormat))
            {
                var validResult = checkResult.Passed ? "通过" : "失败";
                var scriptOutput = checkResult.VerifyOutput ?? checkResult.Detail;
                checkResult.Detail = rule.ResultFormat
                    .Replace("${Valid_Result}", validResult)
                    .Replace("${Vaild_Result}", validResult)
                    .Replace("${Script_Output}", scriptOutput);
            }

            checkResult.Elapsed = ruleSw.Elapsed.TotalSeconds < 1 ? "<1s" : ruleSw.Elapsed.TotalMinutes >= 1 ? $"{(int)ruleSw.Elapsed.TotalMinutes}m{ruleSw.Elapsed.Seconds}s" : $"{ruleSw.Elapsed.Seconds}s"; report.CheckResults.Add(checkResult); ruleProgress?.Report(checkResult);
            if (!string.IsNullOrEmpty(rule.ResultFormat))
                taskLog.WriteLine($"结果: {checkResult.Detail}");
            else
                taskLog.WriteLine($"项目：{rule.RuleName} - 结果: {(checkResult.Passed ? "✅" : "❌")} - {checkResult.Detail}");
            taskLog.WriteLine();
            _log.Debug($"规则 [{rule.RuleName}] 完成, 通过={checkResult.Passed}");

            if (!checkResult.Passed && !rule.FailContinue)
            {
                taskLog.WriteLine("⚠ 遇错即停 (FailContinue=false)，跳过后续规则");
                _log.Warning($"规则 [{rule.RuleName}] 失败且 FailContinue=false，停止后续质检");
                break;
            }
        }

        taskLog.WriteLine($"══════ 质检完成: {task.TaskName}  {DateTime.Now:yyyy-MM-dd HH:mm:ss} ══════");
        taskLog.WriteLine($"全部通过: {(report.AllPassed ? "是" : "否")}");

        _log.Debug($"任务日志已写入: {logPath}");
        _log.Info($"质检完成: {task.TaskName}, 全部通过={report.AllPassed}, 共{report.CheckResults.Count}条结果");

        // 生成 CSV 报告
        try
        {
            report.CheckResults.Insert(0, new CheckResult
            {
                RuleName = "任务名",
                Passed = true,
                Detail = task.TaskName
            });

            if (elapsed.HasValue) report.Elapsed = elapsed.Value.TotalMinutes >= 1 ? $"{(int)elapsed.Value.TotalMinutes}m{elapsed.Value.Seconds}s" : $"{elapsed.Value.Seconds}s"; else { var ts = taskSw.Elapsed; report.Elapsed = ts.TotalMinutes >= 1 ? $"{(int)ts.TotalMinutes}m{ts.Seconds}s" : $"{ts.Seconds}s"; } var reportPath = await _reportService.GenerateReportAsync(report, taskDir);
            task.ReportPath = reportPath;
            task.HasReport = true;
        }
        catch (Exception ex)
        {
            progress.Report($"生成报告失败: {ex.Message}");
        }

        return report;
    }

    /// <summary>
    /// 匹配验证脚本输出与期望值
    /// </summary>
    private bool MatchVerifyResult(string output, string matchResult)
    {
        _log.Debug($"MatchVerifyResult: input='{output}', expected='{matchResult}'");

        if (!double.TryParse(output, CultureInfo.InvariantCulture, out var actualValue))
        {
            _log.Debug($"非数值输出，字符串比较: '{output}' vs '{matchResult}'");
            return string.Equals(output, matchResult, StringComparison.OrdinalIgnoreCase);
        }

        var rangeMatch = RangePattern().Match(matchResult);
        if (rangeMatch.Success)
        {
            var leftBracket = rangeMatch.Groups[1].Value;
            var lowerStr = rangeMatch.Groups[2].Value;
            var upperStr = rangeMatch.Groups[3].Value;
            var rightBracket = rangeMatch.Groups[4].Value;

            double lower = double.Parse(lowerStr, CultureInfo.InvariantCulture);
            double upper = double.Parse(upperStr, CultureInfo.InvariantCulture);

            bool lowerOk = leftBracket == "[" ? actualValue >= lower : actualValue > lower;
            bool upperOk = rightBracket == "]" ? actualValue <= upper : actualValue < upper;

            _log.Debug($"范围匹配: actual={actualValue}, range={matchResult}, lowerOk={lowerOk}, upperOk={upperOk}");
            return lowerOk && upperOk;
        }

        if (double.TryParse(matchResult, CultureInfo.InvariantCulture, out var expectedValue))
        {
            var passed = Math.Abs(actualValue - expectedValue) < 1e-10;
            _log.Debug($"数值匹配: actual={actualValue}, expected={expectedValue}, diff={Math.Abs(actualValue - expectedValue):E}, passed={passed}");
            return passed;
        }

        _log.Debug($"兜底字符串匹配: '{output}' vs '{matchResult}'");
        return string.Equals(output, matchResult, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^([\[\(])([0-9.]+),([0-9.]+)([\]\)])$")]
    private static partial Regex RangePattern();

    /// <summary>截断过长的详情文本（用于 CSV 报告，日志保留完整版）</summary>
    private static string TruncateDetail(string text, int maxLen = 80)
    {
        if (string.IsNullOrWhiteSpace(text)) return "(空)";
        var clean = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return clean.Length <= maxLen ? clean : clean[..maxLen] + "…";
    }
}
