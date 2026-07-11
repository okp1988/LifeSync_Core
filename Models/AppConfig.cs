namespace LifeSyncTaskClient.Models;

public sealed class AppConfig
{
    public string GoogleAppsScriptUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int LogRetentionDays { get; set; } = 30;
    public string TaskTrackerTaskExportMode { get; set; } = TaskTrackerExportModes.Off;
    public string TaskTrackerTrackExportMode { get; set; } = TaskTrackerExportModes.Off;
    public string TaskTrackerExportPath { get; set; } = string.Empty;
    public string TaskTrackerExePath { get; set; } = string.Empty;
    public List<string> TaskTrackerExportedSourceKeys { get; set; } = [];
}

public static class TaskTrackerExportModes
{
    public const string Off = "Off";
    public const string Prompt = "Prompt";
    public const string Auto = "Auto";
}
