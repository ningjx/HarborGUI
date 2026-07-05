using System.IO;
using System.Text.RegularExpressions;
using HarborGUI.Services.Interfaces;

namespace HarborGUI.Services;

/// <summary>
/// 变量解析器：替换命令中的 ${...} 占位符
/// 
/// 解析优先级：
///   1. ${task.XXX} → 从任务目录的 task.toml 中用正则提取
///   2. ${KEY}     → 从 AppConfig.EnvironmentVariables 字典取值
/// </summary>
public partial class VariableResolver : IVariableResolver
{
    private readonly Dictionary<string, string> _envVars;

    public VariableResolver(Dictionary<string, string> environmentVariables)
    {
        _envVars = new Dictionary<string, string>(environmentVariables, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 更新环境变量字典（当用户在 UI 中修改配置后调用）
    /// </summary>
    public void UpdateEnvironmentVariables(Dictionary<string, string> envVars)
    {
        _envVars.Clear();
        foreach (var kv in envVars)
            _envVars[kv.Key] = kv.Value;
    }

    public string Resolve(string command, string? taskDirectory = null)
    {
        if (string.IsNullOrEmpty(command))
            return command;

        return VariablePattern().Replace(command, match =>
        {
            var varName = match.Groups[1].Value;

            // 1. 尝试从 task.toml 解析
            if (varName.StartsWith("task.", StringComparison.OrdinalIgnoreCase) && taskDirectory != null)
            {
                var tomlValue = ResolveFromToml(varName, taskDirectory);
                if (tomlValue != null)
                    return tomlValue;
            }

            // 2. 尝试从环境变量字典取值
            if (_envVars.TryGetValue(varName, out var envValue) && !string.IsNullOrEmpty(envValue))
                return envValue;

            // 3. 兜底：保留原样（或尝试系统环境变量）
            var sysEnv = Environment.GetEnvironmentVariable(varName);
            if (!string.IsNullOrEmpty(sysEnv))
                return sysEnv;

            // 无法解析，保留原占位符（可能在运行时由 shell 解析）
            return match.Value;
        });
    }

    public List<string> ResolveAll(List<string> commands, string? taskDirectory = null)
    {
        return commands.Select(c => Resolve(c, taskDirectory)).ToList();
    }

    // ==================== TOML 正则解析 ====================

    /// <summary>
    /// 从 task.toml 中用正则提取变量值
    /// 路径映射规则：
    ///   ${task.name}                   → [task] 段, key=name
    ///   ${task.environment.docker_image} → [environment] 段, key=docker_image
    ///   ${task.metadata.difficulty}    → [metadata] 段, key=difficulty
    ///   ${task.verifier.env.XXX}       → [verifier.env] 段, key=XXX
    /// </summary>
    private static string? ResolveFromToml(string variablePath, string taskDirectory)
    {
        var tomlPath = Path.Combine(taskDirectory, "task.toml");
        if (!File.Exists(tomlPath))
            return null;

        var tomlContent = File.ReadAllText(tomlPath);

        // 移除 "task." 前缀
        var path = variablePath.Substring(5); // "task.".Length == 5
        var parts = path.Split('.');

        string sectionName;
        string keyName;

        if (parts.Length == 1)
        {
            // ${task.name} → [task] section, key = name
            sectionName = "task";
            keyName = parts[0];
        }
        else
        {
            // ${task.environment.docker_image} → [environment], key = docker_image
            // ${task.verifier.env.XXX}         → [verifier.env], key = XXX
            keyName = parts[^1];
            sectionName = string.Join(".", parts.Take(parts.Length - 1));
        }

        return ExtractTomlValue(tomlContent, sectionName, keyName);
    }

    /// <summary>
    /// 从 TOML 文本的指定段中提取指定键的值
    /// 支持带引号的字符串值和不带引号的简单值
    /// </summary>
    private static string? ExtractTomlValue(string tomlContent, string sectionName, string keyName)
    {
        // 构建正则：匹配 [section] 头部，然后在该段内查找 key = "value" 或 key = value
        var escapedSection = Regex.Escape(sectionName);
        var escapedKey = Regex.Escape(keyName);

        // 模式：\[section\] ... key = "value"  (字符串值)
        var quotedPattern = $@"\[{escapedSection}\]\s*[^\[]*?{escapedKey}\s*=\s*""([^""]*)""";
        var quotedMatch = Regex.Match(tomlContent, quotedPattern, RegexOptions.Singleline);
        if (quotedMatch.Success)
            return quotedMatch.Groups[1].Value;

        // 模式：\[section\] ... key = value  (非字符串值，如数字、布尔)
        var unquotedPattern = $@"\[{escapedSection}\]\s*[^\[]*?{escapedKey}\s*=\s*(\S+)";
        var unquotedMatch = Regex.Match(tomlContent, unquotedPattern, RegexOptions.Singleline);
        if (unquotedMatch.Success)
            return unquotedMatch.Groups[1].Value;

        return null;
    }

    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex VariablePattern();
}
