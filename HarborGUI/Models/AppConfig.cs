using System.Text.Json.Serialization;

namespace HarborGUI.Models;

/// <summary>
/// 应用程序主配置（窗口状态、环境变量等）
/// 对应 Config/config.json
/// </summary>
public class AppConfig
{
    /// <summary>窗口状态</summary>
    [JsonPropertyName("WindowState")]
    public WindowStateConfig WindowState { get; set; } = new();

    /// <summary>上次使用的工作目录</summary>
    [JsonPropertyName("LastWorkingDirectory")]
    public string LastWorkingDirectory { get; set; } = string.Empty;

    /// <summary>上次使用的规则配置文件路径</summary>
    [JsonPropertyName("LastConfigPath")]
    public string LastConfigPath { get; set; } = string.Empty;

    /// <summary>
    /// 全局环境变量字典
    /// 命令中 ${KEY} 形式的占位符从此处取值
    /// </summary>
    [JsonPropertyName("EnvironmentVariables")]
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>最大并行任务数（0=不限制）</summary>
    [JsonPropertyName("MaxParallelTasks")]
    public int MaxParallelTasks { get; set; } = 0;

    /// <summary>自定义按钮配置</summary>
    [JsonPropertyName("CustomButton")]
    public CustomButtonConfig? CustomButton { get; set; }

    /// <summary>日志打开工具 (notepad/vscode)</summary>
    [JsonPropertyName("LogTool")]
    public string LogTool { get; set; } = "vscode";
}

/// <summary>
/// 窗口状态持久化
/// </summary>
public class WindowStateConfig
{
    [JsonPropertyName("Width")]
    public double Width { get; set; } = 1100;

    [JsonPropertyName("Height")]
    public double Height { get; set; } = 700;

    [JsonPropertyName("Left")]
    public double Left { get; set; } = double.NaN;

    [JsonPropertyName("Top")]
    public double Top { get; set; } = double.NaN;

    [JsonPropertyName("Maximized")]
    public bool Maximized { get; set; }
}

/// <summary>
/// 自定义按钮配置
/// </summary>
public class CustomButtonConfig
{
    [JsonPropertyName("Label")]
    public string Label { get; set; } = "自定义";

    [JsonPropertyName("Script")]
    public string Script { get; set; } = string.Empty;
}

