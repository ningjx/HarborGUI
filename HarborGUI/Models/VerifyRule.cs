using System.Text.Json.Serialization;

namespace HarborGUI.Models;

/// <summary>
/// 单条质检规则定义，与配置文件 JSON 结构对应
/// </summary>
public class VerifyRule
{
    /// <summary>规则名称（用于显示和报告）</summary>
    [JsonPropertyName("RuleName")]
    public string RuleName { get; set; } = string.Empty;

    /// <summary>要执行的命令列表，按顺序逐个运行</summary>
    [JsonPropertyName("Commands")]
    public List<string> Commands { get; set; } = [];

    /// <summary>期望的命令退出码</summary>
    [JsonPropertyName("ExitCode")]
    public string ExitCode { get; set; } = "0";

    /// <summary>可选的验证脚本路径（Python），命令成功后再运行</summary>
    [JsonPropertyName("VerifyScript")]
    public string? VerifyScript { get; set; }

    /// <summary>
    /// 期望的验证结果
    /// </summary>
    [JsonPropertyName("MatchResult")]
    public string MatchResult { get; set; } = "0";

    /// <summary>失败后是否继续执行后续规则（默认 true 兼容旧配置）</summary>
    [JsonPropertyName("FailContinue")]
    public bool FailContinue { get; set; } = true;

    /// <summary>自定义质检输出格式，支持 ${Valid_Result}、${Script_Output} 变量</summary>
    [JsonPropertyName("ResultFormat")]
    public string? ResultFormat { get; set; }

    /// <summary>UI 中是否被选中</summary>
    [JsonIgnore]
    public bool IsSelected { get; set; } = true;
}
