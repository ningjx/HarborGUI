using System.IO;
using HarborGUI.Models;
using HarborGUI.Services.Interfaces;

namespace HarborGUI.Services;

/// <summary>
/// 任务发现服务：扫描 .zip 文件
/// </summary>
public class TaskDiscoveryService : ITaskDiscoveryService
{
    public List<TaskItem> DiscoverTasks(string workingDirectory)
    {
        if (!Directory.Exists(workingDirectory))
            return [];

        var tasks = new List<TaskItem>();

        // 1. .zip
        foreach (var file in Directory.GetFiles(workingDirectory, "*.zip", SearchOption.TopDirectoryOnly))
            tasks.Add(new TaskItem { FileName = Path.GetFileName(file), FullPath = file, IsSelected = true });

        // 2. 目录（不解套，后续统一由 DirectoryHelper 识别根目录）
        foreach (var dir in Directory.GetDirectories(workingDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName.Equals("tasks", StringComparison.OrdinalIgnoreCase)) continue;

            tasks.Add(new TaskItem { FileName = dirName, FullPath = dir, ExtractedPath = dir, Status = VerifyTaskStatus.Pending, IsSelected = true });
        }

        return tasks.OrderBy(t => t.FileName).ToList();
    }
}
