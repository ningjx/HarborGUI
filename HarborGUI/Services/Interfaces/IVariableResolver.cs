namespace HarborGUI.Services.Interfaces;

/// <summary>
/// 变量解析器：将命令中的 ${...} 占位符替换为实际值
/// 支持多种来源：
///   1. 运行时变量（如 ${Zip_Path}，通过 Resolve/ResolveAll 的 runtimeVars 参数传入）
///   2. Config.json 的 EnvironmentVariables（如 ${API_KEY}）
///   3. 任务目录中的 task.toml 字段（如 ${task.environment.docker_image}）
///   4. 系统环境变量（兜底）
/// </summary>
public interface IVariableResolver
{
    /// <summary>
    /// 解析单条命令中的所有 ${...} 占位符
    /// </summary>
    /// <param name="command">包含占位符的命令字符串</param>
    /// <param name="taskDirectory">任务解压目录（用于读取 task.toml），null 时跳过 TOML 解析</param>
    /// <param name="runtimeVars">运行时变量字典（如 Zip_Path），优先级最高，null 时跳过</param>
    string Resolve(string command, string? taskDirectory = null,
        IReadOnlyDictionary<string, string>? runtimeVars = null);

    /// <summary>批量解析命令列表</summary>
    List<string> ResolveAll(List<string> commands, string? taskDirectory = null,
        IReadOnlyDictionary<string, string>? runtimeVars = null);

    /// <summary>更新环境变量字典（当用户在 UI 中修改配置后调用）</summary>
    void UpdateEnvironmentVariables(Dictionary<string, string> envVars);
}
