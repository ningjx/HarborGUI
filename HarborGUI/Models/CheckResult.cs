namespace HarborGUI.Models;

/// <summary>
/// 单条规则的质检结果
/// </summary>
public class CheckResult
{
    /// <summary>规则名称</summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>是否通过</summary>
    public bool Passed { get; set; }

    /// <summary>详细信息（如退出码或脚本输出）</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>实际退出码</summary>
    public int ActualExitCode { get; set; }

    /// <summary>验证脚本输出（如有）</summary>
    public string? VerifyOutput { get; set; } public string? Elapsed { get; set; }
}
