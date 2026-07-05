using System.IO;
using System.Text;
using HarborGUI.Models;
using HarborGUI.Services.Interfaces;

namespace HarborGUI.Services;

/// <summary>
/// 报告服务：生成 UTF-8 BOM 的 CSV 质检报告
/// </summary>
public class ReportService : IReportService
{
    public Task<string> GenerateReportAsync(TaskCheckReport report, string outputDirectory)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var reportPath = Path.Combine(outputDirectory, $"质检结果报告_{timestamp}.csv");

        var sb = new StringBuilder();

        // CSV 头部
        sb.AppendLine("检测项目,是否通过,详细信息,耗时");

        foreach (var result in report.CheckResults)
        {
            var elapsed = result.Elapsed ?? (result.RuleName == "任务名" ? (report.Elapsed ?? "") : "");
            var passed = result.Passed ? "✅ 通过" : "❌ 失败";
            var detail = EscapeCsvField(result.Detail);
            sb.AppendLine($"{EscapeCsvField(result.RuleName)},{passed},{detail},{elapsed}");
        }

        // 写入 UTF-8 with BOM，Excel 可直接打开
        File.WriteAllText(reportPath, sb.ToString(), new UTF8Encoding(true));

        return Task.FromResult(reportPath);
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
