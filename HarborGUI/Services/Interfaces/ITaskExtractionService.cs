using HarborGUI.Models;

namespace HarborGUI.Services.Interfaces;

/// <summary>
/// 任务解压服务：将 .zip 压缩包解压到 tasks 目录
/// </summary>
public interface ITaskExtractionService
{
    /// <summary>
    /// 解压指定任务到 tasks 目录
    /// 自动处理：无根目录时用压缩包名创建根目录
    /// </summary>
    /// <returns>解压后的根目录路径</returns>
    Task<string> ExtractAsync(TaskItem task, string tasksDirectory, IProgress<string>? progress = null);
}
