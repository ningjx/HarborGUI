using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using HarborGUI.Models;
using HarborGUI.Services;
using HarborGUI.Services.Interfaces;

namespace HarborGUI.ViewModels;

/// <summary>
/// 主窗口 ViewModel：协调 UI 与业务逻辑
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly ITaskDiscoveryService _taskDiscovery;
    private readonly ITaskExtractionService _taskExtraction;
    private readonly IVerifyService _verifyService;
    private readonly IConfigService _configService;
    private readonly IVariableResolver _variableResolver;
    private readonly ILogService _log;
    private readonly AppConfig _appConfig;

    private string _workingDirectory = string.Empty;
    private string _configPath = string.Empty;
    private string _statusMessage = "就绪";
    private bool _isRunning;
    private bool _isSelectAllChecked = true;
    private double _overallProgress;
    private string _currentTaskProgress = string.Empty;
    private CancellationTokenSource? _cts;
    private bool _suppressSave = true; // 启动期间禁止保存，防止覆盖

    public MainViewModel(
        ITaskDiscoveryService taskDiscovery,
        ITaskExtractionService taskExtraction,
        IVerifyService verifyService,
        IConfigService configService,
        IVariableResolver variableResolver,
        ILogService log)
    {
        _taskDiscovery = taskDiscovery;
        _taskExtraction = taskExtraction;
        _verifyService = verifyService;
        _configService = configService;
        _variableResolver = variableResolver;
        _log = log;

        // 加载主配置
        _appConfig = _configService.LoadAppConfig();
        _log.Info($"主配置加载完成: WorkingDir={_appConfig.LastWorkingDirectory}, EnvVars={_appConfig.EnvironmentVariables.Count}");
        _workingDirectory = _appConfig.LastWorkingDirectory;
        _configPath = string.IsNullOrWhiteSpace(_appConfig.LastConfigPath)
            ? _configService.GetDefaultVerifyRulesPath()
            : _appConfig.LastConfigPath;

        // 初始化环境变量列表
        foreach (var kv in _appConfig.EnvironmentVariables)
            EnvVars.Add(new EnvVarItem { Key = kv.Key, Value = kv.Value });

        BrowseDirectoryCommand = new RelayCommand(_ => BrowseDirectory());
        RefreshTasksCommand = new RelayCommand(async () => await RefreshTasksAsync());
        SelectAllCommand = new RelayCommand(_ => SelectAll());
        DeselectAllCommand = new RelayCommand(_ => DeselectAll());
        RunVerifyCommand = new RelayCommand(async () => await RunVerifyAsync(), _ => !IsRunning);
        CancelCommand = new RelayCommand(_ => Cancel());
        CustomButtonCommand = new RelayCommand(async _ => await RunCustomScriptAsync());
        ShowLogCommand = new RelayCommand(_ => ShowExecutionLog());
        SaveConfigCommand = new RelayCommand(_ => SaveAppConfigNow());

        // 启动完成，允许保存
        _suppressSave = false;
    }

    // ==================== 属性 ====================

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            _workingDirectory = value;
            _appConfig.LastWorkingDirectory = value;
            OnPropertyChanged();
            SaveAppConfigDeferred();
        }
    }

    public string ConfigPath
    {
        get => _configPath;
        set
        {
            _configPath = value;
            _appConfig.LastConfigPath = value;
            OnPropertyChanged();
            SaveAppConfigDeferred();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            OnPropertyChanged();
            RunVerifyCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsSelectAllChecked
    {
        get => _isSelectAllChecked;
        set
        {
            _isSelectAllChecked = value;
            OnPropertyChanged();
            if (value) SelectAll(); else DeselectAll();
        }
    }

        /// <summary>最大并行任务数（0=不限制）</summary>
    public string ParallelCount
    {
        get => _appConfig.MaxParallelTasks.ToString();
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == "0")
                _appConfig.MaxParallelTasks = 0;
            else if (int.TryParse(value, out var n) && n > 0)
                _appConfig.MaxParallelTasks = n;
            else return;
            OnPropertyChanged();
            SaveAppConfigDeferred();
        }
    }

    /// <summary>日志打开工具 (notepad/vscode)</summary>
    public string LogTool
    {
        get => _appConfig.LogTool;
        set
        {
            _appConfig.LogTool = value;
            OnPropertyChanged();
            SaveAppConfigDeferred();
        }
    }
    public double OverallProgress
    {
        get => _overallProgress;
        set { _overallProgress = value; OnPropertyChanged(); }
    }

    public string CurrentTaskProgress
    {
        get => _currentTaskProgress;
        set { _currentTaskProgress = value; OnPropertyChanged(); }
    }

    /// <summary>窗口状态配置（绑定到 Window）</summary>
    public WindowStateConfig WindowStateConfig => _appConfig.WindowState;

    /// <summary>自定义按钮文本</summary>
    public string CustomButtonLabel => _appConfig.CustomButton?.Label ?? "自定义";

    /// <summary>执行日志消息列表（用于弹出日志窗口）</summary>
    public ObservableCollection<string> ExecutionLogMessages { get; } = [];

    /// <summary>任务列表</summary>
    public ObservableCollection<TaskItem> Tasks { get; } = [];

    /// <summary>质检规则列表</summary>
    public ObservableCollection<VerifyRule> Rules { get; } = [];

    /// <summary>环境变量列表（可编辑）</summary>
    public ObservableCollection<EnvVarItem> EnvVars { get; } = [];

    /// <summary>当前规则名列表（用于动态生成列）</summary>
    public List<string> RuleNames { get; private set; } = [];

    /// <summary>规则列变化时通知 View 重建列</summary>
    public event Action? RuleColumnsChanged;

    // ==================== 命令 ====================

    public RelayCommand BrowseDirectoryCommand { get; }
    public RelayCommand RefreshTasksCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand DeselectAllCommand { get; }
    public RelayCommand RunVerifyCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand CustomButtonCommand { get; }
    public RelayCommand ShowLogCommand { get; }
    public RelayCommand SaveConfigCommand { get; }

    // ==================== 方法 ====================

    private void BrowseDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择工作目录 - 进入目标文件夹后点击【打开】",
            Filter = "文件夹|.",
            FileName = "选择此文件夹",
            CheckFileExists = false,
            CheckPathExists = true,
            ValidateNames = false
        };

        if (!string.IsNullOrEmpty(WorkingDirectory) && Directory.Exists(WorkingDirectory))
            dialog.InitialDirectory = WorkingDirectory;

        if (dialog.ShowDialog() == true)
        {
            WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? dialog.FileName;
            _ = RefreshTasksAsync();
        }
    }

    public Task RefreshTasksAsync()
    {
        if (string.IsNullOrWhiteSpace(WorkingDirectory) || !Directory.Exists(WorkingDirectory))
        {
            StatusMessage = "请先设置有效的工作目录";
            return Task.CompletedTask;
        }

        Tasks.Clear();
        var discovered = _taskDiscovery.DiscoverTasks(WorkingDirectory);
        foreach (var task in discovered)
            Tasks.Add(task);

        RefreshTaskStatuses();
        LoadRules();
        SaveAppConfigDeferred();

        StatusMessage = $"发现 {Tasks.Count} 个任务";
        return Task.CompletedTask;
    }

    /// <summary>检查 tasks 目录下已解压的任务，恢复状态</summary>
    private void RefreshTaskStatuses()
    {
        foreach (var task in Tasks)
        {
            string extractedPath = task.ExtractedPath 
                ?? Path.Combine(Path.Combine(WorkingDirectory, "tasks"), task.TaskName);

            if (!Directory.Exists(extractedPath)) continue;

            task.ExtractedPath = extractedPath;
            var dataRoot = DirectoryHelper.UnwrapNestedFolder(extractedPath);
            var reports = Directory.GetFiles(dataRoot, "质检结果报告*.csv", SearchOption.AllDirectories);
            var logs = Directory.GetFiles(dataRoot, "质检执行日志*.txt", SearchOption.AllDirectories);
            if (logs.Length > 0) task.LogPath = logs.OrderByDescending(File.GetLastWriteTime).First();
            if (reports.Length > 0)
            {
                var latest = reports.OrderByDescending(File.GetLastWriteTime).First();
                task.ReportPath = latest;
                task.HasReport = true;
                ParseReportIntoResults(task, latest);
            }
            else
            {
                task.Status = VerifyTaskStatus.Pending;
            }

            task.IsSelected = task.Status is VerifyTaskStatus.Pending;
        }
    }
private static void ParseReportIntoResults(TaskItem task, string reportPath)
    {
        try
        {
            task.RuleResults.Clear();
            var lines = File.ReadAllLines(reportPath);
            bool allPassed = true, hasResults = false;
            foreach (var line in lines.Skip(1))
            {
                var cols = line.Split(',');
                if (cols.Length < 2) continue;
                var name = cols[0].Trim();
                var isPass = cols[1].Contains("✅");
                var isFail = cols[1].Contains("❌");
                task.RuleResults[name] = isPass ? "✅" : isFail ? "❌" : cols[1].Trim();
                if (name != "任务名") { hasResults = true; if (isFail) allPassed = false; }
                if (name == "任务名" && cols.Length >= 4)
                {
                    var elapsedStr = cols[cols.Length - 1].Trim();
                    if (!string.IsNullOrEmpty(elapsedStr))
                        task.Elapsed = ParseElapsed(elapsedStr);
                }
            }
            task.Status = !hasResults ? VerifyTaskStatus.Pending : allPassed ? VerifyTaskStatus.Passed : VerifyTaskStatus.Failed;
            task.NotifyRuleResultsChanged();
        }
        catch { }
    }

    private static TimeSpan? ParseElapsed(string elapsed)
    {
        if (string.IsNullOrEmpty(elapsed) || elapsed == "<1s")
            return TimeSpan.Zero;
        var match = System.Text.RegularExpressions.Regex.Match(elapsed, @"(?:(\d+)m)?(\d+)s");
        if (match.Success)
        {
            var totalSeconds = 0;
            if (match.Groups[1].Success)
                totalSeconds += int.Parse(match.Groups[1].Value) * 60;
            totalSeconds += int.Parse(match.Groups[2].Value);
            return TimeSpan.FromSeconds(totalSeconds);
        }
        return null;
    }


    private void LoadRules()
    {
        Rules.Clear();
        var config = _configService.LoadVerifyRules(ConfigPath);
        foreach (var rule in config.VerifyRules)
            Rules.Add(rule);

        // 更新规则名列表，触发 View 重建动态列
        var newNames = config.VerifyRules.Select(r => r.RuleName).ToList();
        if (!newNames.SequenceEqual(RuleNames))
        {
            RuleNames = newNames;
            RuleColumnsChanged?.Invoke();
        }

        StatusMessage = Rules.Count > 0
            ? $"已加载 {Rules.Count} 条质检规则"
            : "未找到质检规则，请在配置文件中添加";
    }

    private void SelectAll()
    {
        foreach (var task in Tasks)
            task.IsSelected = true;
    }

    private void DeselectAll()
    {
        foreach (var task in Tasks)
            task.IsSelected = false;
    }

    private void Cancel()
    {
        _cts?.Cancel();
        StatusMessage = "正在取消…";
    }

    private async Task RunVerifyAsync()
    {
        var selectedTasks = Tasks.Where(t => t.IsSelected).ToList();
        var selectedRules = Rules.Where(r => r.IsSelected).ToList();

        if (selectedTasks.Count == 0)
        {
            StatusMessage = "请至少选择一个任务";
            return;
        }
        if (selectedRules.Count == 0)
        {
            StatusMessage = "请至少选择一条质检规则";
            return;
        }

        // 保存环境变量到配置，并更新解析器
        SyncEnvVarsToConfig();

        IsRunning = true;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var progress = new Progress<string>(msg =>
        {
            CurrentTaskProgress = msg;
            StatusMessage = msg;
        });

        var tasksDir = Path.Combine(WorkingDirectory, "tasks");
        Directory.CreateDirectory(tasksDir);

        int totalTasks = selectedTasks.Count; int totalSteps = selectedTasks.Sum(t => selectedRules.Count); int stepCount = 0; int completed = 0, passed = 0;
        var lockObj = new object();
        var maxParallel = _appConfig.MaxParallelTasks;
        var semaphore = maxParallel > 0 ? new SemaphoreSlim(maxParallel) : null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var taskStartOffsets = new Dictionary<TaskItem, TimeSpan>();
        var tickTimer = new System.Windows.Threading.DispatcherTimer(TimeSpan.FromSeconds(1), System.Windows.Threading.DispatcherPriority.Background, (_, _) =>
        {
            foreach (var t in selectedTasks)
                if (t.Status == VerifyTaskStatus.Verifying && taskStartOffsets.TryGetValue(t, out var offset))
                    t.Elapsed = sw.Elapsed - offset;
        }, System.Windows.Application.Current.Dispatcher);
        tickTimer.Start();
        _log.Info($"开始并行质检: {totalTasks} 个任务, {selectedRules.Count} 条规则, 并行={(maxParallel > 0 ? maxParallel.ToString() : "不限")}");
        ExecutionLogMessages.Clear();
        ExecutionLogMessages.Add($"══════ 质检开始: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ══════");
        ExecutionLogMessages.Add($"任务数: {totalTasks}, 规则数: {selectedRules.Count}, 并行: {(maxParallel > 0 ? maxParallel.ToString() : "不限")}");

        try
        {
            var tasks = selectedTasks.Select(async task =>
            {
                if (semaphore != null)
                {
                    task.Status = VerifyTaskStatus.Queued;
                    await semaphore.WaitAsync(token);
                }
                taskStartOffsets[task] = sw.Elapsed;
                Models.TaskCheckReport? report = null;
                try { report = await ProcessOneTask(task, selectedRules, tasksDir, token, sw, () => Interlocked.Increment(ref stepCount)); }
					finally { semaphore?.Release(); }
                if (report != null)
                {
                    task.Elapsed = sw.Elapsed - taskStartOffsets[task];
                    report.Elapsed = task.ElapsedText;
                }
                lock (lockObj)
                {
                    completed++;
                    if (task.Status == VerifyTaskStatus.Passed) passed++;
                    OverallProgress = (double)stepCount / totalSteps * 100;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var logMsg = $"[{completed}/{totalTasks}] {task.TaskName} → {task.StatusText}";
                        StatusMessage = logMsg;
                        ExecutionLogMessages.Add(logMsg);
                    });
                }
            });

            await Task.WhenAll(tasks);

            OverallProgress = 100;
            StatusMessage = $"完成！{completed} 个任务, {passed} 通过, {completed - passed} 失败";
            _log.Info($"批量质检完成: {completed} 任务, {passed} 通过, {completed - passed} 失败");
            ExecutionLogMessages.Add($"══════ 质检完成: {completed} 任务, {passed} 通过, {completed - passed} 失败 ══════");
            System.Windows.Application.Current.Dispatcher.Invoke(NotifyIfNotForeground);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"已取消。完成 {completed}/{totalTasks} 个任务";
            _log.Warning($"批量质检取消: {completed}/{totalTasks}");
            ExecutionLogMessages.Add($"══ 已取消: 完成 {completed}/{totalTasks} 个任务 ══");
        }
        catch (Exception ex)
        {
            StatusMessage = $"执行出错: {ex.Message}";
            _log.Error(ex, "批量质检异常");
            ExecutionLogMessages.Add($"══ 异常: {ex.Message} ══");
        }
        finally
        {
            IsRunning = false; tickTimer.Stop();
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task RunCustomScriptAsync()
    {
        var scriptPath = _appConfig.CustomButton?.Script;
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            StatusMessage = "未配置自定义脚本，请在 config.json 中设置 CustomButton.Script";
            return;
        }

        // 解析脚本路径（相对 Config 目录或绝对路径）
        var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        var resolvedScript = Path.Combine(configDir, scriptPath.TrimStart('.', '/', '\\'));
        if (!File.Exists(resolvedScript))
        {
            StatusMessage = $"脚本文件不存在: {resolvedScript}";
            return;
        }

        ExecutionLogMessages.Clear();
        ExecutionLogMessages.Add($"══════ 自定义: {_appConfig.CustomButton?.Label ?? "自定义"} {DateTime.Now:yyyy-MM-dd HH:mm:ss} ══════");
        ExecutionLogMessages.Add($"脚本: {resolvedScript}");

        var totalTasks = Tasks.Count(t => t.IsSelected);
        var selectedCount = 0;
        foreach (var task in Tasks.Where(t => t.IsSelected))
        {
            var taskDir = DirectoryHelper.UnwrapNestedFolder(task.ExtractedPath ?? "");
            if (string.IsNullOrEmpty(taskDir) || !Directory.Exists(taskDir))
            {
                var msg = $"[{++selectedCount}/{totalTasks}] {task.TaskName}: 未解压，跳过";
                StatusMessage = msg;
                ExecutionLogMessages.Add(msg);
                continue;
            }

            var runMsg = $"[{selectedCount + 1}/{totalTasks}] {task.TaskName}: 正在执行…";
            StatusMessage = runMsg;
            CurrentTaskProgress = runMsg;
            _log.Info($"自定义按钮: 在 {taskDir} 中执行 python {resolvedScript}");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{resolvedScript}\"",
                    WorkingDirectory = taskDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                psi.Environment["PYTHONUTF8"] = "1";

                using var process = Process.Start(psi);
                if (process == null)
                {
                    _log.Error($"无法启动 python 进程: {resolvedScript}");
                    continue;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                selectedCount++;
                var doneMsg = $"[{selectedCount}/{totalTasks}] {task.TaskName}: exitCode={process.ExitCode}";
                var brief = string.IsNullOrWhiteSpace(error)
                    ? output.Trim()
                    : error.Trim();
                if (brief.Length > 120) brief = brief[..120] + "…";
                StatusMessage = brief;
                CurrentTaskProgress = doneMsg;
                ExecutionLogMessages.Add(doneMsg);

                if (!string.IsNullOrWhiteSpace(brief))
                    ExecutionLogMessages.Add($"  {brief}");

                _log.Info($"自定义脚本完成: task={task.TaskName}, exitCode={process.ExitCode}");
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"自定义脚本执行失败: task={task.TaskName}");
                var errMsg = $"[{selectedCount + 1}/{totalTasks}] {task.TaskName}: 失败 - {ex.Message}";
                StatusMessage = $"任务 '{task.TaskName}' 执行失败";
                ExecutionLogMessages.Add(errMsg);
            }
        }

        ExecutionLogMessages.Add($"══════ 完成: 处理了 {selectedCount} 个任务 ══════");
        StatusMessage = selectedCount > 0
            ? $"自定义脚本执行完成，处理了 {selectedCount} 个任务"
            : "没有已选中的任务可执行";
    }

    private Views.ExecutionLogWindow? _executionLogWindow;

    private void ShowExecutionLog()
    {
        if (ExecutionLogMessages.Count == 0) return;

        // 如果已有窗口打开且未关闭，激活到前台
        if (_executionLogWindow != null && _executionLogWindow.IsVisible)
        {
            _executionLogWindow.Activate();
            return;
        }

        _executionLogWindow = new Views.ExecutionLogWindow(ExecutionLogMessages);
        _executionLogWindow.Owner = System.Windows.Application.Current.MainWindow;
        _executionLogWindow.Closed += (_, _) => _executionLogWindow = null;
        _executionLogWindow.Show();
    }

    /// <summary>质检完成后如果窗口不在前台，闪烁任务栏并播放提示音</summary>
    private static void NotifyIfNotForeground()
    {
        var hwnd = new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
        if (NativeMethods.GetForegroundWindow() == hwnd) return;

        var info = new NativeMethods.FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.FLASHWINFO>(),
            hwnd = hwnd,
            dwFlags = NativeMethods.FLASHW_ALL | NativeMethods.FLASHW_TIMERNOFG,
            uCount = 3
        };
        NativeMethods.FlashWindowEx(ref info);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private async Task<Models.TaskCheckReport?> ProcessOneTask(TaskItem task, List<VerifyRule> rules, string tasksDir, CancellationToken token, System.Diagnostics.Stopwatch sw, Action onRuleComplete)
    {
        // 阶段1：解压（仅 ZIP 任务需要）
        if (task.ExtractedPath == null)
        {
            task.Status = VerifyTaskStatus.Extracting;
            try
            {
                task.ExtractedPath = await _taskExtraction.ExtractAsync(task, tasksDir, null);
                }
            catch (Exception ex)
            {
                task.Status = VerifyTaskStatus.Error;
                task.CurrentStep = $"解压失败: {ex.Message}";
                return null;
            }
        }

        // 阶段2：质检
        task.RuleResults.Clear();
        task.NotifyRuleResultsChanged();
        task.Status = VerifyTaskStatus.Verifying;
        task.CurrentStep = $"执行 {rules.Count} 条规则…";

        var progress = new Progress<string>(msg =>
            System.Windows.Application.Current.Dispatcher.Invoke(() => task.CurrentStep = msg));

        // 预填所有选中规则为等待中
task.RuleResults.Clear();
foreach (var r in rules) task.RuleResults[r.RuleName] = "🔄";
task.NotifyRuleResultsChanged();
var ruleProgress = new Progress<CheckResult>(cr =>
    System.Windows.Application.Current.Dispatcher.Invoke(() =>
    {
        task.RuleResults[cr.RuleName] = cr.Passed ? "✅" : "❌";
        task.NotifyRuleResultsChanged();
        if (cr.RuleName != "任务名") onRuleComplete();
    }));
var report = await _verifyService.VerifyTaskAsync(task, rules, progress, token, ruleProgress: ruleProgress);

        task.Status = report.AllPassed ? VerifyTaskStatus.Passed : VerifyTaskStatus.Failed;
        task.Progress = 100;
        task.CurrentStep = report.AllPassed ? "全部通过" : "存在失败项";

				foreach (var cr in report.CheckResults)
				{
					task.RuleResults[cr.RuleName] = cr.Passed ? "✅" : "❌";
				}
				// FailContinue=false 跳过的规则清掉预填的 🔄
				foreach (var key in task.RuleResults.Keys.Where(k => task.RuleResults[k] == "🔄").ToList())
						task.RuleResults[key] = "";
				task.NotifyRuleResultsChanged();
				return report;
			}
    /// <summary>同步 UI 环境变量到 AppConfig 并更新 VariableResolver</summary>
    private void SyncEnvVarsToConfig()
    {
        _appConfig.EnvironmentVariables.Clear();
        foreach (var item in EnvVars)
            _appConfig.EnvironmentVariables[item.Key] = item.Value;

        if (_variableResolver is VariableResolver vr)
            vr.UpdateEnvironmentVariables(_appConfig.EnvironmentVariables);

        SaveAppConfigDeferred();
    }

    /// <summary>延迟保存（避免频繁写入，启动期间禁止）</summary>
    private void SaveAppConfigDeferred()
    {
        if (_suppressSave)
        {
            _log.Debug("启动中，跳过配置保存");
            return;
        }
        _configService.SaveAppConfig(_appConfig);
    }

    /// <summary>手动保存配置</summary>
    private void SaveAppConfigNow()
    {
        SyncEnvVarsToConfig();
        StatusMessage = "配置已保存";
    }

    // ==================== INotifyPropertyChanged ====================

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 环境变量项（用于 UI 列表绑定）
/// </summary>
public class EnvVarItem : INotifyPropertyChanged
{
    private string _key = string.Empty;
    private string _value = string.Empty;
    private bool _isValueVisible;

    public string Key
    {
        get => _key;
        set { _key = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsSecret)); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaskedValue)); }
    }

    /// <summary>是否是敏感字段（含 KEY/SECRET/TOKEN/PASSWORD 等关键词）</summary>
    public bool IsSecret =>
        _key.Contains("KEY", StringComparison.OrdinalIgnoreCase) ||
        _key.Contains("SECRET", StringComparison.OrdinalIgnoreCase) ||
        _key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) ||
        _key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase) ||
        _key.Contains("PWD", StringComparison.OrdinalIgnoreCase);

    /// <summary>是否明文显示值</summary>
    public bool IsValueVisible
    {
        get => _isValueVisible;
        set { _isValueVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaskedValue)); }
    }

    /// <summary>掩码显示（•••），非敏感字段直接返回明文</summary>
    public string MaskedValue => IsSecret && !IsValueVisible
        ? new string('•', Math.Min(_value.Length, 20))
        : _value;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Win32 互操作（任务栏闪烁通知）</summary>
internal static class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    internal const uint FLASHW_ALL = 3;
    internal const uint FLASHW_TIMERNOFG = 12;

    [StructLayout(LayoutKind.Sequential)]
    internal struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }
}
