namespace HarborGUI.Services.Interfaces;

/// <summary>
/// 变量提供者：为 VariableResolver 提供特定来源的变量值解析
/// 按注册顺序决定优先级（先注册的先匹配）
/// </summary>
public interface IVariableProvider
{
    /// <summary>
    /// 尝试解析变量
    /// </summary>
    /// <param name="variableName">变量名（不含 ${}）</param>
    /// <param name="taskDirectory">当前任务解压目录，提供者可据此读取 task.toml 等文件</param>
    /// <returns>解析结果，无法处理时返回 null</returns>
    string? TryResolve(string variableName, string? taskDirectory);
}
