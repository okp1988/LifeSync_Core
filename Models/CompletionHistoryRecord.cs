using System.Text.Json.Serialization;

namespace LifeSyncTaskClient.Models;

public sealed class CompletionHistoryRecord
{
    public string RecordId { get; set; } = Guid.NewGuid().ToString("N");
    public string OperationId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public DateTime CompletedDate { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.Now;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string State { get; set; } = CompletionHistoryStates.Pending;
    public SheetTask? BeforeTask { get; set; }
    public List<SheetTask> BeforeAffectedTasks { get; set; } = [];
    public string MinorCompletionSummary { get; set; } = string.Empty;

    [JsonIgnore]
    public bool CanUndo { get; set; }

    [JsonIgnore]
    public string CompletedDateDisplay => CompletedDate.ToString("dd MMM yyyy");

    [JsonIgnore]
    public string StateDisplay => State switch
    {
        CompletionHistoryStates.Pending => "Waiting to sync",
        CompletionHistoryStates.Synced => "Synced",
        CompletionHistoryStates.Conflict => "Conflict",
        CompletionHistoryStates.Undone => "Undone",
        _ => State
    };
}

public static class CompletionHistoryStates
{
    public const string Pending = "Pending";
    public const string Synced = "Synced";
    public const string Conflict = "Conflict";
    public const string Undone = "Undone";
}
