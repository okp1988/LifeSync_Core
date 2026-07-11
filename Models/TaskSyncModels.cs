using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LifeSyncTaskClient.Models;

public static class TaskMutationTypes
{
    public const string Create = "create";
    public const string Update = "update";
    public const string Complete = "complete";
    public const string Snooze = "snooze";
    public const string ClearSnooze = "clearSnooze";
    public const string Archive = "archive";
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
    public string State { get; set; } = TaskMutationStates.Pending;
    public TaskMutationPayload Payload { get; set; } = new();
    public SheetTask? ServerTask { get; set; }
}

public sealed class TaskMutationPayload
{
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

    public static TaskMutationPayload FromTask(SheetTask task)
    {
        return new TaskMutationPayload
        {
            Category = task.Category,
            Type = task.Type,
            Task = task.Task,
            ExpiredValue = task.ExpiredValue,
            ExpiredUnit = task.ExpiredUnit,
            WarningValue = task.WarningValue,
            WarningUnit = task.WarningUnit,
            Alert = task.Alert,
            History = task.History
        };
    }
}

public sealed class TaskApiResponse
{
    public bool Success { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public SheetTask? Task { get; set; }
    public SheetTask? ServerTask { get; set; }
}

public sealed class TaskFetchResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public DateTime? ServerTime { get; set; }
    public List<SheetTask> Tasks { get; set; } = [];
}

public sealed class TaskEditDraft
{
    public string TaskId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public int ExpiredValue { get; set; } = 1;
    public string ExpiredUnit { get; set; } = "Month";
    public int WarningValue { get; set; }
    public string WarningUnit { get; set; } = "Month";
    public bool Alert { get; set; } = true;
    public bool History { get; set; } = true;
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
