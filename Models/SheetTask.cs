using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace LifeSyncTaskClient.Models;

public sealed class SheetTask : INotifyPropertyChanged
{
    private string _remark = string.Empty;
    private string _syncState = TaskSyncStates.Synced;

    public string TaskId { get; set; } = string.Empty;
    public int Revision { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool Archived { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public DateTime? ExpiredDate { get; set; }
    public DateTime? WarningDate { get; set; }
    public DateTime? PreviousDate1 { get; set; }
    public DateTime? PreviousDate2 { get; set; }
    public bool Completed { get; set; }
    public bool Alert { get; set; }
    public bool History { get; set; }
    public int ExpiredValue { get; set; }
    public string ExpiredUnit { get; set; } = "Month";
    public int WarningValue { get; set; }
    public string WarningUnit { get; set; } = "Month";

    public int? DayLeft => ExpiredDate is null
        ? null
        : (ExpiredDate.Value.Date - DateTime.Today).Days;

    public int? DayPassed => PreviousDate1 is null
        ? null
        : (DateTime.Today - PreviousDate1.Value.Date).Days;

    public string Remark
    {
        get => _remark;
        set
        {
            if (_remark == value)
            {
                return;
            }

            _remark = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RemarkFirstLine));
            OnPropertyChanged(nameof(RemarkPreview));
            OnPropertyChanged(nameof(HasMultiLineRemark));
        }
    }

    public DateTime? LastExecutedDate { get; set; }
    public int RowNumber { get; set; }
    public DateTime? SnoozeUntil { get; set; }
    public string SnoozeNote { get; set; } = string.Empty;
    public string LastGoogleTaskKey { get; set; } = string.Empty;
    public string LastGoogleTaskId { get; set; } = string.Empty;
    public DateTime? LastGoogleTaskCreatedDate { get; set; }
    public string LastLifeSyncOperationId { get; set; } = string.Empty;

    [JsonIgnore]
    public string SyncState
    {
        get => _syncState;
        set
        {
            if (_syncState == value)
            {
                return;
            }

            _syncState = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public string RemarkFirstLine => GetFirstLine(Remark);

    [JsonIgnore]
    public string RemarkPreview => HasMultiLineRemark ? $"{RemarkFirstLine} ..." : RemarkFirstLine;

    [JsonIgnore]
    public bool HasMultiLineRemark => HasMultipleLines(Remark);

    [JsonIgnore]
    public bool IsSnoozed => SnoozeUntil is not null && SnoozeUntil.Value.Date >= DateTime.Today;

    [JsonIgnore]
    public string SnoozeDisplay => SnoozeUntil is null
        ? string.Empty
        : IsSnoozed
            ? $"Snoozed until {SnoozeUntil.Value:dd MMM yyyy}"
            : $"Snooze ended {SnoozeUntil.Value:dd MMM yyyy}";

    [JsonIgnore]
    public string GoogleTaskDisplay => string.IsNullOrWhiteSpace(LastGoogleTaskId)
        ? string.Empty
        : LastGoogleTaskCreatedDate is null
            ? "Google Task created"
            : $"Google Task {LastGoogleTaskCreatedDate.Value:dd MMM yyyy}";

    [JsonIgnore]
    public string Status
    {
        get
        {
            var today = DateTime.Today;
            var isExpired = ExpiredDate is not null && ExpiredDate.Value.Date <= today;
            var isWarning = WarningDate is not null && WarningDate.Value.Date <= today;

            if (Completed)
            {
                return "Completed";
            }

            if (ExpiredDate is null)
            {
                return "Not Started";
            }

            if (isExpired)
            {
                return "Expired";
            }

            if (isWarning)
            {
                return "Warning";
            }

            return "Normal";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyCalculatedFieldsChanged()
    {
        OnPropertyChanged(nameof(DayLeft));
        OnPropertyChanged(nameof(DayPassed));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Completed));
        OnPropertyChanged(nameof(IsSnoozed));
        OnPropertyChanged(nameof(SnoozeDisplay));
        OnPropertyChanged(nameof(GoogleTaskDisplay));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string GetFirstLine(string value)
    {
        return value.Replace("\r\n", "\n").Split('\n')[0];
    }

    private static bool HasMultipleLines(string value)
    {
        return value.Contains('\n') || value.Contains('\r');
    }
}

public static class TaskSyncStates
{
    public const string Synced = "Synced";
    public const string Pending = "Pending";
    public const string Conflict = "Conflict";
}
