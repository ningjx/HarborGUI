using System.IO;
using System.IO.Compression;
using HarborGUI.Models;
using HarborGUI.Services.Interfaces;

namespace HarborGUI.Services;

/// <summary>
/// 任务解压服务：仅解压，不处理目录嵌套（嵌套由 DirectoryHelper 统一处理）
/// </summary>
public class TaskExtractionService : ITaskExtractionService
{
    public Task<string> ExtractAsync(TaskItem task, string tasksDirectory, IProgress<string>? progress = null)
    {
        return Task.Run(() =>
        {
            progress?.Report($"正在解压: {task.FileName}");

            var taskDirName = task.TaskName;
            var extractRoot = Path.Combine(tasksDirectory, taskDirName);

            // 解压到临时目录，保证解压过程中断不会污染目标目录
            var tempDir = Path.Combine(tasksDirectory, $"_temp_{Guid.NewGuid():N}");
            try
            {
                ZipFile.ExtractToDirectory(task.FullPath, tempDir);

                // 覆盖旧目录
                if (Directory.Exists(extractRoot))
                    Directory.Delete(extractRoot, true);
                Directory.Move(tempDir, extractRoot);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }

            progress?.Report($"解压完成: {task.FileName}");
            return extractRoot;
        });
    }
}
