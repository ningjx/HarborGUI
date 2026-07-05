using HarborGUI.Models;

namespace HarborGUI.Services.Interfaces;

/// <summary>
/// 任务发现服务：扫描工作目录下的 .zip 任务文件
/// </summary>
public interface ITaskDiscoveryService
{
    /// <summary>扫描指定目录下的所有 .zip 文件，返回任务列表</summary>
    List<TaskItem> DiscoverTasks(string workingDirectory);
}
