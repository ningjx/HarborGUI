using HarborGUI.Services.Interfaces;

namespace HarborGUI.Services;

/// <summary>
/// 运行时变量提供者：包装一个字典，用于单次 Resolve/ResolveAll 调用中传入临时变量
/// 例如在质检时传入当前任务的压缩包路径作为 ${Zip_Path}
/// </summary>
public class RuntimeVariableProvider : IVariableProvider
{
    private readonly IReadOnlyDictionary<string, string> _variables;

    public RuntimeVariableProvider(IReadOnlyDictionary<string, string> variables)
    {
        _variables = variables;
    }

    public string? TryResolve(string variableName, string? taskDirectory)
    {
        return _variables.TryGetValue(variableName, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : null;
    }
}
