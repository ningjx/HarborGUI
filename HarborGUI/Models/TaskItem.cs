using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace HarborGUI.Models;

public enum VerifyTaskStatus
{
    Pending, Queued, Extracting, Ready, Verifying, Passed, Failed, Error
}

public class TaskItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private VerifyTaskStatus _status = VerifyTaskStatus.Pending;
    private string _statusText = "待处理";
    private string _currentStep = string.Empty;
    private double _progress;
    private bool _hasReport;

    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string TaskName => Path.GetFileNameWithoutExtension(FileName);
    public string? ExtractedPath { get; set; }
    public string? ReportPath { get; set; }
    private string? _logPath;
    private bool _hasLog;
    public string? LogPath { get => _logPath; set { _logPath = value; HasLog = value != null; OnPropertyChanged(); } }
    public bool HasLog { get => _hasLog; set { _hasLog = value; OnPropertyChanged(); } }
    public Dictionary<string, string> RuleResults { get; set; } = [];
    private TimeSpan? _elapsed; public TimeSpan? Elapsed { get => _elapsed; set { _elapsed = value; OnPropertyChanged(); OnPropertyChanged(nameof(ElapsedText)); } }
    public string ElapsedText => Elapsed.HasValue ? (Elapsed.Value.TotalMinutes >= 1 ? $"{(int)Elapsed.Value.TotalMinutes}m{Elapsed.Value.Seconds}s" : $"{Elapsed.Value.Seconds}s") : "";

    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public VerifyTaskStatus Status { get => _status; set { _status = value; StatusText = StatusToText(value); OnPropertyChanged(); } }
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
    public string CurrentStep { get => _currentStep; set { _currentStep = value; OnPropertyChanged(); } }
    public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }
    public bool HasReport { get => _hasReport; set { _hasReport = value; OnPropertyChanged(); } }

    public string GetRuleResult(string ruleName) => RuleResults.TryGetValue(ruleName, out var v) ? v : "";
    public void NotifyRuleResultsChanged() => OnPropertyChanged(nameof(RuleResults));

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string StatusToText(VerifyTaskStatus status) => status switch { VerifyTaskStatus.Queued => "排队中", VerifyTaskStatus.Verifying or VerifyTaskStatus.Extracting => "质检中", VerifyTaskStatus.Passed => "合格", VerifyTaskStatus.Failed or VerifyTaskStatus.Error => "不合格", _ => "未质检" };
}
