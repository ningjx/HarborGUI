using System.IO;
using System.Text.RegularExpressions;
using HarborGUI.Services.Interfaces;

namespace HarborGUI.Services;

/// <summary>
/// 变量解析器：将命令中的 ${...} 占位符替换为实际值
///
/// 解析优先级（由 Provider 注册顺序决定）：
///   1. 运行时变量（如 ${Zip_Path}，通过 runtimeVars 参数传入）
///   2. ${task.XXX} → 从任务目录的 task.toml 中用正则提取
///   3. ${KEY}     → 从 AppConfig.EnvironmentVariables 字典取值
///   4. 系统环境变量 → Environment.GetEnvironmentVariable
///   5. 无法解析    → 保留原占位符
/// </summary>
public partial class VariableResolver : IVariableResolver
{
    private readonly List<IVariableProvider> _fixedProviders;

    public VariableResolver(Dictionary<string, string> environmentVariables)
    {
        _fixedProviders =
        [
            new TomlVariableProvider(),
            new ConfigVariableProvider(environmentVariables),
        ];
    }

    public void UpdateEnvironmentVariables(Dictionary<string, string> envVars)
    {
        foreach (var provider in _fixedProviders)
        {
            if (provider is ConfigVariableProvider configProvider)
            {
                configProvider.Update(envVars);
                return;
            }
        }
    }

    public string Resolve(string command, string? taskDirectory = null,
        IReadOnlyDictionary<string, string>? runtimeVars = null)
    {
        if (string.IsNullOrEmpty(command))
            return command;

        var chain = BuildChain(runtimeVars);
        return ResolveInternal(command, taskDirectory, chain);
    }

    public List<string> ResolveAll(List<string> commands, string? taskDirectory = null,
        IReadOnlyDictionary<string, string>? runtimeVars = null)
    {
        var chain = BuildChain(runtimeVars);
        return commands.Select(c => ResolveInternal(c, taskDirectory, chain)).ToList();
    }

    private string ResolveInternal(string command, string? taskDirectory, IEnumerable<IVariableProvider> providers)
    {
        return VariablePattern().Replace(command, match =>
        {
            var varName = match.Groups[1].Value;

            foreach (var provider in providers)
            {
                var result = provider.TryResolve(varName, taskDirectory);
                if (result != null)
                    return result;
            }

            var sysEnv = Environment.GetEnvironmentVariable(varName);
            if (!string.IsNullOrEmpty(sysEnv))
                return sysEnv;

            return match.Value;
        });
    }

    private IEnumerable<IVariableProvider> BuildChain(IReadOnlyDictionary<string, string>? runtimeVars)
    {
        if (runtimeVars is null)
            return _fixedProviders;

        return new[] { new RuntimeVariableProvider(runtimeVars) }.Concat(_fixedProviders);
    }

    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex VariablePattern();

    // ==================== 内置 Provider 实现 ====================

    private class TomlVariableProvider : IVariableProvider
    {
        public string? TryResolve(string variableName, string? taskDirectory)
        {
            if (!variableName.StartsWith("task.", StringComparison.OrdinalIgnoreCase) || taskDirectory == null)
                return null;

            var tomlPath = Path.Combine(taskDirectory, "task.toml");
            if (!File.Exists(tomlPath))
                return null;

            var tomlContent = File.ReadAllText(tomlPath);
            var path = variableName[5..]; // remove "task."
            var parts = path.Split('.');

            string sectionName, keyName;
            if (parts.Length == 1)
            {
                sectionName = "task";
                keyName = parts[0];
            }
            else
            {
                keyName = parts[^1];
                sectionName = string.Join(".", parts.Take(parts.Length - 1));
            }

            return ExtractTomlValue(tomlContent, sectionName, keyName);
        }

        private static string? ExtractTomlValue(string tomlContent, string sectionName, string keyName)
        {
            var escapedSection = Regex.Escape(sectionName);
            var escapedKey = Regex.Escape(keyName);

            var quotedPattern = $@"\[{escapedSection}\]\s*[^\[]*?{escapedKey}\s*=\s*""([^""]*)""";
            var quotedMatch = Regex.Match(tomlContent, quotedPattern, RegexOptions.Singleline);
            if (quotedMatch.Success)
                return quotedMatch.Groups[1].Value;

            var unquotedPattern = $@"\[{escapedSection}\]\s*[^\[]*?{escapedKey}\s*=\s*(\S+)";
            var unquotedMatch = Regex.Match(tomlContent, unquotedPattern, RegexOptions.Singleline);
            if (unquotedMatch.Success)
                return unquotedMatch.Groups[1].Value;

            return null;
        }
    }

    private class ConfigVariableProvider : IVariableProvider
    {
        private readonly Dictionary<string, string> _envVars;

        public ConfigVariableProvider(Dictionary<string, string> envVars)
        {
            _envVars = new Dictionary<string, string>(envVars, StringComparer.OrdinalIgnoreCase);
        }

        public void Update(Dictionary<string, string> envVars)
        {
            _envVars.Clear();
            foreach (var kv in envVars)
                _envVars[kv.Key] = kv.Value;
        }

        public string? TryResolve(string variableName, string? taskDirectory)
        {
            return _envVars.TryGetValue(variableName, out var value) && !string.IsNullOrEmpty(value)
                ? value
                : null;
        }
    }
}
