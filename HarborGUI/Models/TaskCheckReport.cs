namespace HarborGUI.Models;

public class TaskCheckReport
{
    public string TaskName { get; set; } = string.Empty;
    public List<CheckResult> CheckResults { get; set; } = [];
    public bool AllPassed => CheckResults.Count > 0 && CheckResults.TrueForAll(r => r.Passed);
    public string? Elapsed { get; set; }
}
