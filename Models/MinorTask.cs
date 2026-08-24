using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace LifeSyncTaskClient.Models;

public sealed class MinorTask : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private int? _intervalValue;
    private string _intervalUnit = "Month";
    private DateTime? _latestCompletionDate;
    private DateTime? _dueDate;
    private bool _archived;

    public string MinorTaskId { get; set; } = Guid.NewGuid().ToString("D");
    public string ParentTaskId { get; set; } = string.Empty;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public int? IntervalValue
    {
        get => _intervalValue;
        set
        {
            if (SetField(ref _intervalValue, value))
            {
                OnPropertyChanged(nameof(IntervalDisplay));
                OnPropertyChanged(nameof(CompactIntervalDisplay));
                OnPropertyChanged(nameof(CompactStatusDisplay));
            }
        }
    }

    public string IntervalUnit
    {
        get => _intervalUnit;
        set
        {
            if (SetField(ref _intervalUnit, value))
            {
                OnPropertyChanged(nameof(IntervalDisplay));
                OnPropertyChanged(nameof(CompactIntervalDisplay));
                OnPropertyChanged(nameof(CompactStatusDisplay));
            }
        }
    }

    [JsonConverter(typeof(DateOnlyStringJsonConverter))]
    public DateTime? LatestCompletionDate
    {
        get => _latestCompletionDate;
        set
        {
            if (SetField(ref _latestCompletionDate, value))
            {
                OnPropertyChanged(nameof(LatestCompletionDisplay));
                OnPropertyChanged(nameof(CompactStatusDisplay));
            }
        }
    }

    [JsonConverter(typeof(DateOnlyStringJsonConverter))]
    public DateTime? DueDate
    {
        get => _dueDate;
        set
        {
            if (SetField(ref _dueDate, value))
            {
                OnPropertyChanged(nameof(DueDateDisplay));
                OnPropertyChanged(nameof(IsOverdue));
                OnPropertyChanged(nameof(CompactStatusDisplay));
            }
        }
    }

    public int Order { get; set; }

    public bool Archived
    {
        get => _archived;
        set => SetField(ref _archived, value);
    }

    [JsonIgnore]
    public bool HasInterval => IntervalValue is > 0;

    [JsonIgnore]
    public bool IsOverdue => !Archived && DueDate?.Date <= DateTime.Today;

    [JsonIgnore]
    public string IntervalDisplay => HasInterval ? $"{IntervalValue} {IntervalUnit}" : "No interval";

    [JsonIgnore]
    public string LatestCompletionDisplay => LatestCompletionDate?.ToString("dd MMM yyyy") ?? "Never";

    [JsonIgnore]
    public string DueDateDisplay => DueDate?.ToString("dd MMM yyyy") ?? "No due date";

    [JsonIgnore]
    public string CompactIntervalDisplay
    {
        get
        {
            if (!HasInterval) return "-";
            var unit = IntervalUnit.Trim().ToLowerInvariant() switch
            {
                "day" or "days" => "d",
                "month" or "months" => "mth",
                "year" or "years" => "yr",
                _ => IntervalUnit.Trim().ToLowerInvariant()
            };
            return $"{IntervalValue}{unit}";
        }
    }

    [JsonIgnore]
    public string CompactStatusDisplay =>
        $"E {DueDate?.ToString("dd MMM yyyy") ?? "-"}   "
        + $"L {LatestCompletionDate?.ToString("dd MMM yyyy") ?? "-"}   "
        + $"I {CompactIntervalDisplay}";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyCalculatedFieldsChanged()
    {
        OnPropertyChanged(nameof(HasInterval));
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(IntervalDisplay));
        OnPropertyChanged(nameof(CompactIntervalDisplay));
        OnPropertyChanged(nameof(CompactStatusDisplay));
        OnPropertyChanged(nameof(LatestCompletionDisplay));
        OnPropertyChanged(nameof(DueDateDisplay));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class MinorTaskCompletionDraft : INotifyPropertyChanged
{
    private bool _isSelected;
    private DateTime? _completionDate;

    public required MinorTask MinorTask { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public DateTime? CompletionDate
    {
        get => _completionDate;
        set => SetField(ref _completionDate, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class MinorTaskCompletionPayload
{
    [JsonPropertyName("minorTaskId")]
    public string MinorTaskId { get; set; } = string.Empty;

    [JsonPropertyName("completionDate")]
    [JsonConverter(typeof(DateOnlyStringJsonConverter))]
    public DateTime? CompletionDate { get; set; }
}
