using HarborGUI.Models;

namespace HarborGUI.Services.Interfaces;

/// <summary>
/// 报告服务：生成 CSV 格式的质检结果报告
/// </summary>
public interface IReportService
{
    /// <summary>生成 CSV 报告并保存到指定路径</summary>
    Task<string> GenerateReportAsync(TaskCheckReport report, string outputDirectory);
}
