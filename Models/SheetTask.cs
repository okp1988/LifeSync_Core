using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace LifeSyncTaskClient.Models;

public sealed class SheetTask : INotifyPropertyChanged
{
    private string _remark = string.Empty;
    private string _syncState = TaskSyncStates.Synced;
    private int _monthlyHistoryCount;
    private DateTime _calculatedDisplayDate;
    private NextDateInfo? _nextDateInfo;
    private IReadOnlyList<CycleTimelineBlock>? _cycleTimelineBlocks;
    private bool _isExpanded;
    private int _hierarchyDepth;
    private int _hierarchyOrder;
    private string _predecessorTaskName = string.Empty;

    public string TaskId { get; set; } = string.Empty;
    public int Revision { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool Archived { get; set; }
    public int Level { get; set; } = 1;
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

    public int? DayPassed => GridLastExecutedDate is null
        ? null
        : (DateTime.Today - GridLastExecutedDate.Value.Date).Days;

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
    public string PredecessorTaskId { get; set; } = string.Empty;
    public bool IsLinkedUnlocked { get; set; } = true;
    public DateTime? LinkedActivationDate { get; set; }
    public bool Paused { get; set; }
    public DateTime? ResumeDate { get; set; }
    public List<MinorTask> MinorTasks { get; set; } = [];

    [JsonIgnore]
    public IReadOnlyList<SheetTask> LinkedFollowers { get; private set; } = [];

    [JsonIgnore]
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsEffectivelyPaused => Paused && (ResumeDate is null || ResumeDate.Value.Date > DateTime.Today);

    [JsonIgnore]
    public bool IsLinkedLocked => !string.IsNullOrWhiteSpace(PredecessorTaskId) && !IsLinkedUnlocked;

    [JsonIgnore]
    public IReadOnlyList<MinorTask> ActiveMinorTasks => MinorTasks
        .Where(item => !item.Archived)
        .OrderBy(item => item.Order)
        .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    [JsonIgnore]
    public int OverdueMinorCount => IsEffectivelyPaused ? 0 : ActiveMinorTasks.Count(item => item.IsOverdue);

    [JsonIgnore]
    public string OverdueMinorDisplay => OverdueMinorCount > 0 ? $"{OverdueMinorCount}" : string.Empty;

    [JsonIgnore]
    public bool HasOverdueMinors => OverdueMinorCount > 0;

    [JsonIgnore]
    public bool HasMinorTasks => ActiveMinorTasks.Count > 0;

    [JsonIgnore]
    public bool HasLinkedFollowers => LinkedFollowers.Count > 0;

    [JsonIgnore]
    public bool HasExpandableDetails => !IsEffectivelyPaused && (HasMinorTasks || HasLinkedFollowers);

    [JsonIgnore]
    public string PauseDisplay => ResumeDate is null ? "Paused indefinitely" : $"Paused until {ResumeDate.Value:dd MMM yyyy}";

    [JsonIgnore]
    public string FullPath => string.Join(" / ", new[] { Category, Type, Task }.Where(value => !string.IsNullOrWhiteSpace(value)));

    [JsonIgnore]
    public string LinkedStateDisplay => IsEffectivelyPaused ? "Paused" : IsLinkedLocked ? "Waiting for source" : "Active";

    [JsonIgnore]
    public int HierarchyDepth
    {
        get => _hierarchyDepth;
        set
        {
            if (_hierarchyDepth == value) return;
            _hierarchyDepth = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HierarchyTaskDisplay));
        }
    }

    [JsonIgnore]
    public int HierarchyOrder
    {
        get => _hierarchyOrder;
        set
        {
            if (_hierarchyOrder == value) return;
            _hierarchyOrder = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public string PredecessorTaskName
    {
        get => _predecessorTaskName;
        set
        {
            if (_predecessorTaskName == value) return;
            _predecessorTaskName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HierarchyToolTip));
        }
    }

    [JsonIgnore]
    public string HierarchyTaskDisplay => HierarchyDepth == 0
        ? Task
        : $"{new string(' ', Math.Min(HierarchyDepth, 8) * 2)}└ {Task}";

    [JsonIgnore]
    public string HierarchyToolTip => string.IsNullOrWhiteSpace(PredecessorTaskName)
        ? FullPath
        : $"Unlocked by {PredecessorTaskName}";

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
    public DateTime? GridLastExecutedDate => LastExecutedDate ?? PreviousDate1;

    [JsonIgnore]
    public DateTime? CycleAnchorDate => GridLastExecutedDate;

    [JsonIgnore]
    public string ExpiredDateDisplay => FormatDate(ExpiredDate);

    [JsonIgnore]
    public string ExpiredDayDisplay => FormatDayNumber(DayLeft);

    [JsonIgnore]
    public string WarningDateDisplay => WarningDate is null || WarningDate.Value.Date == ExpiredDate?.Date
        ? "-"
        : WarningDate.Value.ToString("dd MMM yyyy");

    [JsonIgnore]
    public string AlertDateDisplay => SnoozeUntil is null ? "-" : SnoozeUntil.Value.ToString("dd MMM yyyy");

    [JsonIgnore]
    public string AlertDayDisplay
    {
        get
        {
            if (SnoozeUntil is null)
            {
                return string.Empty;
            }

            var days = (SnoozeUntil.Value.Date - DateTime.Today).Days;
            return $"({Math.Abs(days)})";
        }
    }

    [JsonIgnore]
    public string LastExecutedDateDisplay => FormatDate(GridLastExecutedDate);

    [JsonIgnore]
    public string LastExecutedDayDisplay => FormatDayNumber(DayPassed);

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
    public int MonthlyHistoryCount
    {
        get => _monthlyHistoryCount;
        private set
        {
            if (_monthlyHistoryCount == value) return;
            _monthlyHistoryCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MonthlyHistoryDisplay));
        }
    }

    [JsonIgnore]
    public string MonthlyHistoryDisplay => History ? $"{MonthlyHistoryCount}×" : string.Empty;

    [JsonIgnore]
    public string NextDateDisplay => GetNextDateInfo().Date is DateTime date
        ? date.ToString("dd MMM yyyy")
        : "-";

    [JsonIgnore]
    public string NextDateMetricsDisplay => GetNextDateInfo().Metrics;

    [JsonIgnore]
    public string NextDateKind => GetNextDateInfo().Kind;

    [JsonIgnore]
    public IReadOnlyList<CycleTimelineBlock> CycleTimelineBlocks
    {
        get
        {
            EnsureDisplayCacheDate();
            return _cycleTimelineBlocks ??= BuildCycleTimeline(_calculatedDisplayDate);
        }
    }

    [JsonIgnore]
    public bool HasCycleTimeline => CycleAnchorDate is not null
        && ExpiredDate is not null
        && ExpiredDate.Value.Date > CycleAnchorDate.Value.Date;

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

    [JsonIgnore]
    public int PriorityRank => Status switch
    {
        "Expired" when Alert => 0,
        "Warning" when Alert => 1,
        "Expired" => 2,
        "Warning" => 3,
        _ => 4
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyCalculatedFieldsChanged()
    {
        InvalidateDisplayCache();
        OnPropertyChanged(nameof(DayLeft));
        OnPropertyChanged(nameof(DayPassed));
        OnPropertyChanged(nameof(GridLastExecutedDate));
        OnPropertyChanged(nameof(CycleAnchorDate));
        OnPropertyChanged(nameof(ExpiredDateDisplay));
        OnPropertyChanged(nameof(ExpiredDayDisplay));
        OnPropertyChanged(nameof(WarningDateDisplay));
        OnPropertyChanged(nameof(AlertDateDisplay));
        OnPropertyChanged(nameof(AlertDayDisplay));
        OnPropertyChanged(nameof(LastExecutedDateDisplay));
        OnPropertyChanged(nameof(LastExecutedDayDisplay));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(PriorityRank));
        OnPropertyChanged(nameof(Completed));
        OnPropertyChanged(nameof(IsSnoozed));
        OnPropertyChanged(nameof(SnoozeDisplay));
        OnPropertyChanged(nameof(GoogleTaskDisplay));
        OnPropertyChanged(nameof(History));
        OnPropertyChanged(nameof(MonthlyHistoryDisplay));
        OnPropertyChanged(nameof(NextDateDisplay));
        OnPropertyChanged(nameof(NextDateMetricsDisplay));
        OnPropertyChanged(nameof(NextDateKind));
        OnPropertyChanged(nameof(CycleTimelineBlocks));
        OnPropertyChanged(nameof(HasCycleTimeline));
        OnPropertyChanged(nameof(IsEffectivelyPaused));
        OnPropertyChanged(nameof(IsLinkedLocked));
        OnPropertyChanged(nameof(ActiveMinorTasks));
        OnPropertyChanged(nameof(OverdueMinorCount));
        OnPropertyChanged(nameof(OverdueMinorDisplay));
        OnPropertyChanged(nameof(HasOverdueMinors));
        OnPropertyChanged(nameof(HasMinorTasks));
        OnPropertyChanged(nameof(HasLinkedFollowers));
        OnPropertyChanged(nameof(HasExpandableDetails));
        OnPropertyChanged(nameof(PauseDisplay));
        OnPropertyChanged(nameof(FullPath));
        OnPropertyChanged(nameof(LinkedStateDisplay));
        foreach (var minorTask in MinorTasks)
        {
            minorTask.NotifyCalculatedFieldsChanged();
        }
    }

    public void SetMonthlyHistoryCount(int count)
    {
        MonthlyHistoryCount = Math.Max(0, count);
    }

    public void SetLinkedFollowers(IEnumerable<SheetTask> followers)
    {
        LinkedFollowers = followers.ToList();
        OnPropertyChanged(nameof(LinkedFollowers));
        OnPropertyChanged(nameof(HasLinkedFollowers));
        OnPropertyChanged(nameof(HasExpandableDetails));
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

    private static string FormatDayNumber(int? days)
    {
        return days is null ? string.Empty : $"({days.Value})";
    }

    private static string FormatDate(DateTime? date)
    {
        return date?.ToString("dd MMM yyyy") ?? "-";
    }

    private NextDateInfo GetNextDateInfo()
    {
        EnsureDisplayCacheDate();
        return _nextDateInfo ??= BuildNextDateInfo(_calculatedDisplayDate);
    }

    private void EnsureDisplayCacheDate()
    {
        var today = DateTime.Today;
        if (_calculatedDisplayDate == today)
        {
            return;
        }

        _calculatedDisplayDate = today;
        _nextDateInfo = null;
        _cycleTimelineBlocks = null;
    }

    private void InvalidateDisplayCache()
    {
        _calculatedDisplayDate = default;
        _nextDateInfo = null;
        _cycleTimelineBlocks = null;
    }

    private NextDateInfo BuildNextDateInfo(DateTime todayValue)
    {
        var today = todayValue.Date;
        var expired = ExpiredDate?.Date;
        var warning = WarningDate?.Date;
        var snooze = SnoozeUntil?.Date;
        if (expired is null) return new NextDateInfo(null, string.Empty, NextDateKinds.None);

        var expiryDays = (expired.Value - today).Days;
        if (snooze is not null && snooze.Value >= today)
        {
            var snoozeDays = (snooze.Value - today).Days;
            var metrics = today < expired.Value
                ? $"({snoozeDays} | {expiryDays})"
                : $"({expiryDays} | {snoozeDays})";
            return new NextDateInfo(snooze, metrics, NextDateKinds.Snooze);
        }

        if (warning is not null && warning.Value < expired.Value && today < warning.Value)
        {
            var warningDays = (warning.Value - today).Days;
            return new NextDateInfo(warning, $"({warningDays} | {expiryDays})", NextDateKinds.Warning);
        }

        if (today < expired.Value)
        {
            return new NextDateInfo(expired, $"({expiryDays})", NextDateKinds.Expired);
        }

        if (!Alert)
        {
            return new NextDateInfo(expired, $"({expiryDays})", NextDateKinds.Expired);
        }

        var delayedExpiry = snooze is not null && snooze.Value > expired.Value;
        var reminderAnchor = delayedExpiry ? snooze!.Value : expired.Value;
        var reminderDays = Math.Max(0, (today - reminderAnchor).Days);
        var anchorKey = delayedExpiry ? $"-{reminderAnchor:yyyyMMdd}" : string.Empty;
        var stageKey = reminderDays < 7
            ? $"expired{anchorKey}"
            : $"overdue{anchorKey}-{reminderDays / 7}";
        var cycleKey = expired.Value.ToString("yyyyMMdd");
        var currentReminderKey = $"{TaskId}|{cycleKey}|{stageKey}";
        var nextAlert = !string.Equals(LastGoogleTaskKey, currentReminderKey, StringComparison.Ordinal)
            ? today
            : reminderDays < 7
                ? reminderAnchor.AddDays(7)
                : reminderAnchor.AddDays(((reminderDays / 7) + 1) * 7);
        var nextAlertDays = (nextAlert - today).Days;
        return new NextDateInfo(nextAlert, $"({expiryDays} | {nextAlertDays})", NextDateKinds.Alert);
    }

    private IReadOnlyList<CycleTimelineBlock> BuildCycleTimeline(DateTime todayValue)
    {
        if (!HasCycleTimeline) return [];

        var today = todayValue.Date;
        var start = CycleAnchorDate!.Value.Date;
        var expired = ExpiredDate!.Value.Date;
        if (today >= expired)
        {
            var overdueDays = (today - expired).Days;
            var grayCount = Math.Clamp(overdueDays / 10, 0, 10);
            return Enumerable.Range(0, 10)
                .Select(index => new CycleTimelineBlock(index < grayCount ? TimelineBlockStates.Gray : TimelineBlockStates.Red))
                .ToArray();
        }

        var totalDays = Math.Max(1, (expired - start).Days);
        var todayIndex = Math.Clamp((int)Math.Floor((today - start).Days * 10d / totalDays), 0, 9);
        var warning = WarningDate?.Date;
        var hasWarning = warning is not null && warning.Value < expired;
        var warningIndex = hasWarning
            ? Math.Clamp((int)Math.Floor((warning!.Value - start).Days * 10d / totalDays), 0, 9)
            : -1;
        var warningActive = warning is not null && today >= warning.Value;
        var warningPassed = warning is not null && today > warning.Value;

        return Enumerable.Range(0, 10)
            .Select(index => new CycleTimelineBlock(
                warningActive
                    ? warningPassed && index <= todayIndex
                        ? TimelineBlockStates.AfterWarning
                        : TimelineBlockStates.Warning
                    : index == warningIndex
                        ? TimelineBlockStates.Warning
                        : index == todayIndex
                            ? TimelineBlockStates.Today
                            : TimelineBlockStates.Default))
            .ToArray();
    }

    private sealed record NextDateInfo(DateTime? Date, string Metrics, string Kind);
}

public sealed record CycleTimelineBlock(string State);

public static class TimelineBlockStates
{
    public const string Default = "Default";
    public const string Today = "Today";
    public const string Warning = "Warning";
    public const string AfterWarning = "AfterWarning";
    public const string Red = "Red";
    public const string Gray = "Gray";
}

public static class NextDateKinds
{
    public const string None = "None";
    public const string Warning = "Warning";
    public const string Expired = "Expired";
    public const string Snooze = "Snooze";
    public const string Alert = "Alert";
}

public static class TaskSyncStates
{
    public const string Synced = "Synced";
    public const string Pending = "Pending";
    public const string Conflict = "Conflict";
}
