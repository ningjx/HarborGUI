using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HarborGUI.Converters;
using HarborGUI.Models;
using HarborGUI.ViewModels;

namespace HarborGUI;

/// <summary>
/// MainWindow - 通过 DI 注入 MainViewModel，管理窗口状态和动态列
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly RuleResultConverter _ruleResultConverter = new();

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // 订阅规则列变化
        _viewModel.RuleColumnsChanged += RebuildRuleColumns;

        // 恢复窗口状态
        RestoreWindowState();

        // 保存窗口状态
        Closing += OnWindowClosing;

        // 窗口加载完成后自动刷新任务列表（此时 View 已就绪，动态列能正常生成）
        Loaded += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_viewModel.WorkingDirectory))
                _viewModel.RefreshTasksCommand.Execute(null);
        };
    }

    /// <summary>恢复上次保存的窗口位置和大小</summary>
    private void RestoreWindowState()
    {
        var state = _viewModel.WindowStateConfig;

        if (state.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
        else
        {
            if (state.Width > 0 && state.Height > 0)
            {
                Width = state.Width;
                Height = state.Height;
            }

            if (!double.IsNaN(state.Left) && !double.IsNaN(state.Top)) { WindowStartupLocation = WindowStartupLocation.Manual; Left = state.Left; Top = state.Top; }
        }
    }

    /// <summary>窗口关闭前保存状态</summary>
    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        var state = _viewModel.WindowStateConfig;

        if (WindowState == WindowState.Maximized)
        {
            state.Maximized = true;
        }
        else
        {
            state.Maximized = false;
            state.Width = Width;
            state.Height = Height;
            state.Left = Left;
            state.Top = Top;
        }

        // 触发 ViewModel 保存
        _viewModel.RefreshTasksCommand.Execute(null);
    }

    /// <summary>浏览选择配置文件</summary>
    private void BrowseConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择质检规则配置文件",
            Filter = "JSON 文件|*.json|所有文件|*.*",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.ConfigPath = dialog.FileName;
            _viewModel.RefreshTasksCommand.Execute(null);
        }
    }

    /// <summary>切换环境变量值的显示/隐藏</summary>
    private void ToggleEnvVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is EnvVarItem item)
            item.IsValueVisible = !item.IsValueVisible;
    }

    /// <summary>根据当前规则名重建动态列</summary>
    private void RebuildRuleColumns()
    {
        // 移除旧的动态列（保留前 4 个静态列：选择、状态、任务名、文件名）
        while (TaskDataGrid.Columns.Count > 4)
            TaskDataGrid.Columns.RemoveAt(4);

        // 为每个规则名添加一列
        foreach (var ruleName in _viewModel.RuleNames)
        {
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetValue(TextBlock.TextProperty, new Binding("RuleResults")
            {
                Converter = _ruleResultConverter,
                ConverterParameter = ruleName
            });
            factory.SetValue(TextBlock.ForegroundProperty, new Binding("RuleResults")
            {
                Converter = new RuleResultToColorConverter(),
                ConverterParameter = ruleName
            });
            factory.SetValue(TextBlock.FontSizeProperty, 16.0);
            factory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            factory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            factory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            var col = new DataGridTemplateColumn
            {
                Header = ruleName,
                Width = 70,
                MinWidth = 60,
                CellTemplate = new DataTemplate { VisualTree = factory }
            };

            TaskDataGrid.Columns.Add(col);
        }
        
        // 追加删除列
        var delCol = new DataGridTemplateColumn { Header = "删除", Width = 50 };
        var delFactory = new FrameworkElementFactory(typeof(Button));
        delFactory.SetValue(Button.ContentProperty, "🗑");
        delFactory.SetValue(Button.WidthProperty, 30.0);
        delFactory.SetValue(Button.HeightProperty, 24.0);
        delFactory.SetValue(Button.PaddingProperty, new Thickness(0));
        delFactory.SetValue(Button.FontSizeProperty, 12.0);
        delFactory.SetValue(Button.TagProperty, new Binding());
        delFactory.SetValue(Button.ToolTipProperty, "删除此任务（压缩包+文件夹）");
        delFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(DeleteTask_Click));
        delCol.CellTemplate = new DataTemplate { VisualTree = delFactory };
        TaskDataGrid.Columns.Add(delCol);

        // 追加日志列到末尾
        var logCol = new DataGridTemplateColumn { Header = "日志", Width = 50 };
        var logFactory = new FrameworkElementFactory(typeof(Button));
        logFactory.SetValue(Button.ContentProperty, "📄");
        logFactory.SetValue(Button.WidthProperty, 30.0);
        logFactory.SetValue(Button.HeightProperty, 24.0);
        logFactory.SetValue(Button.PaddingProperty, new Thickness(0));
        logFactory.SetValue(Button.FontSizeProperty, 12.0);
        logFactory.SetValue(Button.TagProperty, new Binding());
        logFactory.SetValue(Button.IsEnabledProperty, new Binding("HasLog"));
        logFactory.SetValue(Button.ToolTipProperty, "查看质检日志");
        logFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(OpenLog_Click));
        logCol.CellTemplate = new DataTemplate { VisualTree = logFactory };
        TaskDataGrid.Columns.Add(logCol);
        TaskDataGrid.Items.Refresh();
    }
    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaskItem task && !string.IsNullOrEmpty(task.LogPath))
        {
            try
            {
                var tool = _viewModel.LogTool;
                if (tool == "notepad")
                    System.Diagnostics.Process.Start("notepad.exe", task.LogPath);
                else
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("code", task.LogPath) { UseShellExecute = true });
            }
            catch (System.Exception ex) { System.Windows.MessageBox.Show($"打开日志失败: {ex.Message}", "错误"); }
        }
    }
    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaskItem task)
        {
            var result = System.Windows.MessageBox.Show($"确认删除「{task.TaskName}」？\n将删除原始压缩包和解压文件夹，此操作不可恢复。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            // 删除解压文件夹
            if (!string.IsNullOrEmpty(task.ExtractedPath) && System.IO.Directory.Exists(task.ExtractedPath))
            {
                try { System.IO.Directory.Delete(task.ExtractedPath, true); }
                catch (System.Exception ex) { System.Windows.MessageBox.Show($"删除文件夹失败: {ex.Message}", "错误"); }
            }

            // 删除原始文件（仅当 FullPath 是文件且与解压文件夹不同）
            if (!string.IsNullOrEmpty(task.FullPath) && System.IO.File.Exists(task.FullPath))
            {
                var isSame = task.FullPath.Equals(task.ExtractedPath, StringComparison.OrdinalIgnoreCase);
                if (!isSame)
                {
                    try { System.IO.File.Delete(task.FullPath); }
                    catch (System.Exception ex) { System.Windows.MessageBox.Show($"删除文件失败: {ex.Message}", "错误"); }
                }
            }

            _viewModel.Tasks.Remove(task);
        }
    }
    private void FileName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock tb && tb.Tag is TaskItem task && !string.IsNullOrEmpty(task.ExtractedPath))
            System.Diagnostics.Process.Start("explorer.exe", task.ExtractedPath);
    }
}