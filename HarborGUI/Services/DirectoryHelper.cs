using System.IO;

namespace HarborGUI.Services;

/// <summary>目录工具：递归剥离单文件夹嵌套，找到真正的数据根目录</summary>
public static class DirectoryHelper
{
    /// <summary>从指定路径开始递归下钻，直到找到有多个条目（文件或文件夹）的目录层级</summary>
    public static string UnwrapNestedFolder(string startDir, int maxDepth = 10)
    {
        var current = startDir;
        for (int depth = 0; depth < maxDepth; depth++)
        {
            var entries = Directory.GetFileSystemEntries(current);
            if (entries.Length == 1 && Directory.Exists(entries[0]))
                current = entries[0];
            else
                break;
        }
        return current;
    }
}
