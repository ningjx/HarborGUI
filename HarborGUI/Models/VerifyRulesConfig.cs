using System.Text.Json.Serialization;

namespace HarborGUI.Models;

/// <summary>
/// 质检规则配置文件根对象
/// </summary>
public class VerifyRulesConfig
{
    [JsonPropertyName("VerifyRules")]
    public List<VerifyRule> VerifyRules { get; set; } = [];
}
