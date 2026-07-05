namespace HarborGUI.Services.Interfaces;

/// <summary>
/// 变量解析器：将命令中的 ${...} 占位符替换为实际值
/// 支持两种来源：
///   1. Config.json 的 EnvironmentVariables（如 ${API_KEY}）
///   2. 任务目录中的 task.toml 字段（如 ${task.environment.docker_image}）
/// </summary>
public interface IVariableResolver
{
    /// <summary>解析单条命令中的所有 ${...} 占位符</summary>
    /// <param name="command">包含占位符的命令字符串</param>
    /// <param name="taskDirectory">任务解压目录（用于读取 task.toml），null 时跳过 TOML 解析</param>
    string Resolve(string command, string? taskDirectory = null);

    /// <summary>批量解析命令列表</summary>
    List<string> ResolveAll(List<string> commands, string? taskDirectory = null);
}
