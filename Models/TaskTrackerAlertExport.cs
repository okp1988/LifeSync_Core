namespace LifeSyncTaskClient.Models;

public sealed class TaskTrackerAlertExportDocument
{
    public int SchemaVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string SourceApp { get; set; } = "LifeSync";
    public List<TaskTrackerAlertRequest> Requests { get; set; } = [];
}

public sealed class TaskTrackerAlertRequest
{
    public string RequestId { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public DateTime EligibleAt { get; set; } = DateTime.Now;
    public DateTime ScheduledAt { get; set; } = DateTime.Now;
    public List<TaskTrackerNotificationRule> NotificationRules { get; set; } = [];
    public string Priority { get; set; } = "none";
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class TaskTrackerNotificationRule
{
    public string Type { get; set; } = "specificTime";
    public DateTime? SpecificAt { get; set; }
}
