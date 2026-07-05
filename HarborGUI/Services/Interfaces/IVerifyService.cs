using HarborGUI.Models;

namespace HarborGUI.Services.Interfaces;

/// <summary>
/// 质检编排服务：按规则集对任务执行完整质检流程
/// </summary>
public interface IVerifyService
{
    /// <summary>
    /// 对单个任务执行质检
    /// </summary>
    /// <param name="task">目标任务</param>
    /// <param name="rules">要执行的规则列表</param>
    /// <param name="progress">进度报告</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>质检报告</returns>
    Task<TaskCheckReport> VerifyTaskAsync(
        TaskItem task,
        List<VerifyRule> rules,
        IProgress<string> progress, CancellationToken cancellationToken = default, TimeSpan? elapsed = null, IProgress<CheckResult>? ruleProgress = null);
}
