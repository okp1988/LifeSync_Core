using System.Globalization;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LifeSyncTaskClient.Models;

public static class TaskMutationTypes
{
    public const string Create = "create";
    public const string Update = "update";
    public const string UpdateRemark = "updateRemark";
    public const string UpdateMinors = "updateMinors";
    public const string Complete = "complete";
    public const string Snooze = "snooze";
    public const string ClearSnooze = "clearSnooze";
    public const string Archive = "archive";
    public const string Pause = "pause";
    public const string Resume = "resume";
}

public static class TaskMutationStates
{
    public const string Pending = "Pending";
    public const string Conflict = "Conflict";
}

public sealed class TaskMutation
{
    public string OperationId { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public int ExpectedRevision { get; set; }
    public DateTime QueuedAt { get; set; } = DateTime.Now;
    public DateTimeOffset? UploadAfter { get; set; }
    public string State { get; set; } = TaskMutationStates.Pending;
    public DateTime? LastAttemptAt { get; set; }
    public string LastError { get; set; } = string.Empty;
    public TaskMutationPayload Payload { get; set; } = new();
    public SheetTask? ServerTask { get; set; }

    [JsonIgnore]
    public string OperationDisplay => OperationType switch
    {
        TaskMutationTypes.Create => "Create Task",
        TaskMutationTypes.Update => "Edit Task",
        TaskMutationTypes.UpdateRemark => "Update Remark",
        TaskMutationTypes.UpdateMinors => "Update Minor Tasks",
        TaskMutationTypes.Complete => "Complete Task",
        TaskMutationTypes.Snooze => "Snooze",
        TaskMutationTypes.ClearSnooze => "Clear Snooze",
        TaskMutationTypes.Archive => "Archive",
        TaskMutationTypes.Pause => "Pause",
        TaskMutationTypes.Resume => "Resume",
        _ => OperationType
    };

    [JsonIgnore]
    public string QueuedAtDisplay => QueuedAt.ToString("dd MMM yyyy HH:mm");

    [JsonIgnore]
    public string LastAttemptDisplay => LastAttemptAt?.ToString("dd MMM yyyy HH:mm") ?? "Not attempted";

    [JsonIgnore]
    public string PendingStatusDisplay => !string.IsNullOrWhiteSpace(LastError)
        ? "Failed"
        : UploadAfter is DateTimeOffset uploadAfter && uploadAfter > DateTimeOffset.Now
            ? $"Waiting until {uploadAfter:dd MMM HH:mm}"
            : "Ready to retry";

    [JsonIgnore]
    public string LastErrorDisplay => string.IsNullOrWhiteSpace(LastError)
        ? "No recorded failure."
        : LastError;

    [JsonIgnore]
    public string ResolutionDisplay
    {
        get
        {
            if (LastError.Contains("Unknown action", StringComparison.OrdinalIgnoreCase))
            {
                return "Deploy the latest Apps Script web-app version, then retry this change.";
            }

            if (LastError.Contains("No such host", StringComparison.OrdinalIgnoreCase)
                || LastError.Contains("name or service not known", StringComparison.OrdinalIgnoreCase))
            {
                return "Check the internet connection and DNS access to script.google.com, then retry.";
            }

            if (LastError.Contains("Apps Script URL is required", StringComparison.OrdinalIgnoreCase))
            {
                return "Open Settings, enter the deployed Apps Script URL, save, then retry.";
            }

            if (LastError.Contains("401", StringComparison.OrdinalIgnoreCase)
                || LastError.Contains("403", StringComparison.OrdinalIgnoreCase)
                || LastError.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
                || LastError.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
            {
                return "Check the Apps Script deployment access and API key, then retry.";
            }

            if (OperationType is TaskMutationTypes.UpdateRemark or TaskMutationTypes.UpdateMinors
                && string.IsNullOrWhiteSpace(LastError))
            {
                return "Retry this change. If it reports Unknown action, deploy the latest Apps Script web-app version first.";
            }

            if (UploadAfter is DateTimeOffset uploadAfter && uploadAfter > DateTimeOffset.Now)
            {
                return "This completion is inside its one-hour Undo delay. Retry bypasses the remaining delay.";
            }

            return string.IsNullOrWhiteSpace(LastError)
                ? "Retry this change. If it fails, the error and recovery suggestion will appear here."
                : "Retry the change. If it fails again, review the warning/error log for more detail.";
        }
    }
}

public sealed class TaskMutationPayload
{
    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    [JsonPropertyName("task")]
    public string Task { get; set; } = string.Empty;
    [JsonPropertyName("expiredValue")]
    public int ExpiredValue { get; set; }
    [JsonPropertyName("expiredUnit")]
    public string ExpiredUnit { get; set; } = "Month";
    [JsonPropertyName("warningValue")]
    public int WarningValue { get; set; }
    [JsonPropertyName("warningUnit")]
    public string WarningUnit { get; set; } = "Month";
    [JsonPropertyName("alert")]
    public bool Alert { get; set; }
    [JsonPropertyName("history")]
    public bool History { get; set; }
    [JsonPropertyName("executeDate")]
    [JsonConverter(typeof(DateOnlyStringJsonConverter))]
    public DateTime? ExecuteDate { get; set; }
    [JsonPropertyName("remark")]
    public string Remark { get; set; } = string.Empty;
    [JsonPropertyName("snoozeUntil")]
    [JsonConverter(typeof(DateOnlyStringJsonConverter))]
    public DateTime? SnoozeUntil { get; set; }
    [JsonPropertyName("snoozeNote")]
    public string SnoozeNote { get; set; } = string.Empty;
    [JsonPropertyName("predecessorTaskId")]
    public string PredecessorTaskId { get; set; } = string.Empty;
    [JsonPropertyName("isLinkedUnlocked")]
    public bool IsLinkedUnlocked { get; set; } = true;
    [JsonPropertyName("linkedActivationDate")]
    [JsonConverter(typeof(DateOnlyStringJsonConverter))]
    public DateTime? LinkedActivationDate { get; set; }
    [JsonPropertyName("paused")]
    public bool Paused { get; set; }
    [JsonPropertyName("resumeDate")]
    [JsonConverter(typeof(DateOnlyStringJsonConverter))]
    public DateTime? ResumeDate { get; set; }
    [JsonPropertyName("minorTasks")]
    public List<MinorTask> MinorTasks { get; set; } = [];
    [JsonPropertyName("minorCompletions")]
    public List<MinorTaskCompletionPayload> MinorCompletions { get; set; } = [];

    public static TaskMutationPayload FromTask(SheetTask task)
    {
        return new TaskMutationPayload
        {
            Level = task.Level,
            Category = task.Category,
            Type = task.Type,
            Task = task.Task,
            ExpiredValue = task.ExpiredValue,
            ExpiredUnit = task.ExpiredUnit,
            WarningValue = task.WarningValue,
            WarningUnit = task.WarningUnit,
            Alert = task.Alert,
            History = task.History,
            PredecessorTaskId = task.PredecessorTaskId,
            IsLinkedUnlocked = task.IsLinkedUnlocked,
            LinkedActivationDate = task.LinkedActivationDate,
            Paused = task.Paused,
            ResumeDate = task.ResumeDate,
            MinorTasks = task.MinorTasks.Select(CloneMinorTask).ToList()
        };
    }

    private static MinorTask CloneMinorTask(MinorTask source) => new()
    {
        MinorTaskId = source.MinorTaskId,
        ParentTaskId = source.ParentTaskId,
        Name = source.Name,
        IntervalValue = source.IntervalValue,
        IntervalUnit = source.IntervalUnit,
        LatestCompletionDate = source.LatestCompletionDate,
        DueDate = source.DueDate,
        Order = source.Order,
        Archived = source.Archived
    };
}

public sealed class TaskApiResponse
{
    public bool Success { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public SheetTask? Task { get; set; }
    public SheetTask? ServerTask { get; set; }
    public List<SheetTask> AffectedTasks { get; set; } = [];
}

public sealed class TaskFetchResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public DateTime? ServerTime { get; set; }
    public List<SheetTask> Tasks { get; set; } = [];
    public List<CompletionHistoryRecord> HistoryRecords { get; set; } = [];
    public List<TaskFilterDefinition> Filters { get; set; } = [];
}

public sealed class TaskEditDraft
{
    public string TaskId { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public int ExpiredValue { get; set; } = 1;
    public string ExpiredUnit { get; set; } = "Month";
    public int WarningValue { get; set; }
    public string WarningUnit { get; set; } = "Month";
    public bool Alert { get; set; } = true;
    public bool History { get; set; } = true;
    public string PredecessorTaskId { get; set; } = string.Empty;
    public ObservableCollection<MinorTask> MinorTasks { get; set; } = [];
}

public sealed class DateOnlyStringJsonConverter : JsonConverter<DateTime?>
{
    private const string DateFormat = "yyyy-MM-dd";

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed.Date
            : null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
    }
}
