using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace LifeSyncTaskClient.Models;

public sealed class TrackItem : INotifyPropertyChanged
{
    private static TrackSettings AlertSettings { get; set; } = new();
    private string _name = string.Empty;
    private string _category = string.Empty;
    private string _trackType = TrackItemTypes.QuantityUsage;
    private decimal _totalQuantity;
    private decimal _usedQuantity;
    private bool _isReusable;
    private string _currentUseLocation = string.Empty;
    private DateTime? _startUseDate;
    private int _changeEvery = 12;
    private string _changeUnit = ChangeIntervalUnits.Months;
    private bool _hasExpiryDate;
    private DateTime? _expiryDate;
    private string _expiryReminderText = string.Empty;
    private string _notes = string.Empty;
    private ObservableTrackHistory _history = [];

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Category
    {
        get => _category;
        set => SetField(ref _category, value);
    }

    public string TrackType
    {
        get => _trackType;
        set
        {
            if (SetField(ref _trackType, value))
            {
                OnPropertyChanged(nameof(IsQuantityUsage));
                OnPropertyChanged(nameof(IsChangeCycle));
                OnPropertyChanged(nameof(NextChangeDate));
                OnPropertyChanged(nameof(DaysUntilChange));
                OnPropertyChanged(nameof(ChangeStatus));
                OnPropertyChanged(nameof(AlertDate));
                OnPropertyChanged(nameof(AlertStatus));
                OnPropertyChanged(nameof(RowHighlightColor));
            }
        }
    }

    public decimal TotalQuantity
    {
        get => _totalQuantity;
        set
        {
            if (SetField(ref _totalQuantity, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(LeftQuantity));
                OnPropertyChanged(nameof(StockAlertLevel));
                OnPropertyChanged(nameof(RowHighlightColor));
            }
        }
    }

    public decimal UsedQuantity
    {
        get => _usedQuantity;
        set
        {
            if (SetField(ref _usedQuantity, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(LeftQuantity));
                OnPropertyChanged(nameof(StockAlertLevel));
                OnPropertyChanged(nameof(RowHighlightColor));
            }
        }
    }

    [JsonIgnore]
    public decimal LeftQuantity => Math.Max(0, TotalQuantity - UsedQuantity);

    [JsonIgnore]
    public string StockAlertLevel
    {
        get
        {
            if (!IsQuantityUsage)
            {
                return string.Empty;
            }

            return LeftQuantity switch
            {
                var left when left <= AlertSettings.OutStockThreshold => "Out",
                var left when left <= AlertSettings.CriticalStockThreshold => "Critical",
                var left when left <= AlertSettings.LowStockThreshold => "Low",
                _ => string.Empty
            };
        }
    }

    public bool IsReusable
    {
        get => _isReusable;
        set => SetField(ref _isReusable, value);
    }

    public string CurrentUseLocation
    {
        get => _currentUseLocation;
        set => SetField(ref _currentUseLocation, value);
    }

    public DateTime? StartUseDate
    {
        get => _startUseDate;
        set
        {
            if (SetField(ref _startUseDate, value))
            {
                OnPropertyChanged(nameof(NextChangeDate));
                OnPropertyChanged(nameof(DaysUntilChange));
                OnPropertyChanged(nameof(ChangeStatus));
                OnPropertyChanged(nameof(AlertDate));
                OnPropertyChanged(nameof(AlertStatus));
                OnPropertyChanged(nameof(RowHighlightColor));
            }
        }
    }

    public int ChangeEvery
    {
        get => _changeEvery;
        set
        {
            if (SetField(ref _changeEvery, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(NextChangeDate));
                OnPropertyChanged(nameof(DaysUntilChange));
                OnPropertyChanged(nameof(ChangeStatus));
                OnPropertyChanged(nameof(AlertDate));
                OnPropertyChanged(nameof(AlertStatus));
                OnPropertyChanged(nameof(RowHighlightColor));
            }
        }
    }

    public string ChangeUnit
    {
        get => _changeUnit;
        set
        {
            if (SetField(ref _changeUnit, value))
            {
                OnPropertyChanged(nameof(NextChangeDate));
                OnPropertyChanged(nameof(DaysUntilChange));
                OnPropertyChanged(nameof(ChangeStatus));
                OnPropertyChanged(nameof(AlertDate));
                OnPropertyChanged(nameof(AlertStatus));
                OnPropertyChanged(nameof(RowHighlightColor));
            }
        }
    }

    public bool HasExpiryDate
    {
        get => _hasExpiryDate;
        set
        {
            if (SetField(ref _hasExpiryDate, value))
            {
                OnPropertyChanged(nameof(ExpiryDate));
                OnPropertyChanged(nameof(DaysUntilExpiry));
                OnPropertyChanged(nameof(ExpiryStatus));
                OnPropertyChanged(nameof(NearestExpiryDate));
                OnPropertyChanged(nameof(AlertDate));
                OnPropertyChanged(nameof(AlertStatus));
                OnPropertyChanged(nameof(RowHighlightColor));
            }
        }
    }

    public DateTime? ExpiryDate
    {
        get => HasExpiryDate ? NearestExpiryDate ?? _expiryDate : null;
        set
        {
            if (SetField(ref _expiryDate, value))
            {
                OnPropertyChanged(nameof(DaysUntilExpiry));
                OnPropertyChanged(nameof(ExpiryStatus));
                OnPropertyChanged(nameof(AlertDate));
                OnPropertyChanged(nameof(AlertStatus));
                OnPropertyChanged(nameof(RowHighlightColor));
            }
        }
    }

    public string ExpiryReminderText
    {
        get => string.IsNullOrWhiteSpace(_expiryReminderText)
            ? AlertSettings.ExpiryReminderText
            : _expiryReminderText;
        set
        {
            if (SetField(ref _expiryReminderText, value))
            {
                OnPropertyChanged(nameof(ExpiryStatus));
                OnPropertyChanged(nameof(DaysUntilExpiry));
                OnPropertyChanged(nameof(AlertStatus));
                OnPropertyChanged(nameof(RowHighlightColor));
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set
        {
            if (SetField(ref _notes, value))
            {
                OnPropertyChanged(nameof(NotesFirstLine));
                OnPropertyChanged(nameof(NotesPreview));
                OnPropertyChanged(nameof(HasMultiLineNotes));
            }
        }
    }

    public ObservableTrackHistory History
    {
        get => _history;
        set => SetField(ref _history, value);
    }

    [JsonIgnore]
    public bool IsQuantityUsage => TrackType == TrackItemTypes.QuantityUsage;

    [JsonIgnore]
    public bool IsChangeCycle => TrackType == TrackItemTypes.ChangeCycle;

    [JsonIgnore]
    public DateTime? NextChangeDate
    {
        get
        {
            if (!IsChangeCycle)
            {
                return null;
            }

            var lastChangeDate = History
                .Where(record => record.Action == "Changed")
                .Select(record => (DateTime?)record.Date.Date)
                .OrderByDescending(date => date)
                .FirstOrDefault();

            var lastRecordDate = History
                .Select(record => (DateTime?)record.Date.Date)
                .OrderByDescending(date => date)
                .FirstOrDefault();

            var baseDate = lastChangeDate ?? lastRecordDate ?? StartUseDate?.Date;
            if (baseDate is null)
            {
                return null;
            }

            return AddChangeInterval(baseDate.Value);
        }
    }

    [JsonIgnore]
    public int? DaysUntilChange => NextChangeDate is null
        ? null
        : (NextChangeDate.Value.Date - DateTime.Today).Days;

    [JsonIgnore]
    public string ChangeStatus
    {
        get
        {
            if (!IsChangeCycle || DaysUntilChange is null)
            {
                return string.Empty;
            }

            return DaysUntilChange switch
            {
                < 0 => "Expired",
                0 => "Due today",
                1 => "1 day left",
                var days => $"{days} days left"
            };
        }
    }

    [JsonIgnore]
    public int? DaysUntilExpiry => ExpiryDate is null
        ? null
        : (ExpiryDate.Value.Date - DateTime.Today).Days;

    [JsonIgnore]
    public string ExpiryStatus
    {
        get
        {
            if (DaysUntilExpiry is null)
            {
                return string.Empty;
            }

            if (DaysUntilExpiry < 0)
            {
                return "Expired";
            }

            if (DaysUntilExpiry == 0)
            {
                return "Expires today";
            }

            var reminder = AlertSettings.GetExpiryAlertForDays(DaysUntilExpiry.Value, ExpiryAlertEligibleDays);

            return reminder is null ? string.Empty : $"{DescribeDuration(reminder.Days)} left";
        }
    }

    [JsonIgnore]
    public DateTime? AlertDate => IsChangeCycle ? NextChangeDate : ExpiryDate;

    [JsonIgnore]
    public string AlertStatus => IsChangeCycle ? ChangeStatus : ExpiryStatus;

    [JsonIgnore]
    public TrackStockBatch? NearestExpiryBatch => BuildBatches()
        .Where(batch => HasExpiryDate && batch.RemainingQuantity > 0 && batch.ExpiryDate is not null)
        .OrderBy(batch => batch.ExpiryDate)
        .ThenBy(batch => batch.OwnedDate)
        .FirstOrDefault();

    [JsonIgnore]
    public DateTime? NearestExpiryDate => NearestExpiryBatch?.ExpiryDate;

    [JsonIgnore]
    public int? ExpiryAlertEligibleDays
    {
        get
        {
            var batch = NearestExpiryBatch;
            if (batch?.ExpiryDate is null)
            {
                return null;
            }

            return Math.Max(0, (batch.ExpiryDate.Value.Date - batch.OwnedDate.Date).Days);
        }
    }

    [JsonIgnore]
    public string RowHighlightColor
    {
        get
        {
            return ExpiryStatus switch
            {
                "Expired" or "Expires today" => TrackSettingColors.ToHighlightBrush(AlertSettings.ExpiryExpiredColor),
                _ when DaysUntilExpiry is not null && AlertSettings.GetExpiryAlertForDays(DaysUntilExpiry.Value, ExpiryAlertEligibleDays) is { } alert => TrackSettingColors.ToHighlightBrush(alert.Color),
                _ => StockAlertLevel switch
                {
                    "Out" => TrackSettingColors.ToHighlightBrush(AlertSettings.OutStockColor),
                    "Critical" => TrackSettingColors.ToHighlightBrush(AlertSettings.CriticalStockColor),
                    "Low" => TrackSettingColors.ToHighlightBrush(AlertSettings.LowStockColor),
                    _ => "Transparent"
                }
            };
        }
    }

    [JsonIgnore]
    public string NotesFirstLine => GetFirstLine(Notes);

    [JsonIgnore]
    public string NotesPreview => HasMultiLineNotes ? $"{NotesFirstLine} ..." : NotesFirstLine;

    [JsonIgnore]
    public bool HasMultiLineNotes => HasMultipleLines(Notes);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyCalculatedFieldsChanged()
    {
        OnPropertyChanged(nameof(LeftQuantity));
        OnPropertyChanged(nameof(StockAlertLevel));
        OnPropertyChanged(nameof(RowHighlightColor));
        OnPropertyChanged(nameof(IsQuantityUsage));
        OnPropertyChanged(nameof(IsChangeCycle));
        OnPropertyChanged(nameof(NextChangeDate));
        OnPropertyChanged(nameof(DaysUntilChange));
        OnPropertyChanged(nameof(ChangeStatus));
        OnPropertyChanged(nameof(DaysUntilExpiry));
        OnPropertyChanged(nameof(ExpiryStatus));
        OnPropertyChanged(nameof(NearestExpiryDate));
        OnPropertyChanged(nameof(NearestExpiryBatch));
        OnPropertyChanged(nameof(ExpiryAlertEligibleDays));
        OnPropertyChanged(nameof(AlertDate));
        OnPropertyChanged(nameof(AlertStatus));
    }

    public void RecalculateQuantitiesFromHistory()
    {
        if (History.Count == 0)
        {
            TotalQuantity = 0;
            UsedQuantity = 0;
            StartUseDate = null;
            NotifyCalculatedFieldsChanged();
            return;
        }

        TotalQuantity = History
            .Where(record => record.Action == "Owned")
            .Sum(record => record.Quantity);

        UsedQuantity = History
            .Where(record => record.Action == "Used")
            .Sum(record => record.Quantity)
            - History
                .Where(record => record.Action == "Put Back")
                .Sum(record => record.Quantity);

        if (UsedQuantity < 0)
        {
            UsedQuantity = 0;
        }

        NotifyCalculatedFieldsChanged();
    }

    public List<TrackStockBatch> BuildBatches()
    {
        var ownedRecords = History
            .Where(record => record.Action == "Owned")
            .OrderBy(record => record.Date)
            .ThenBy(record => record.RecordedAt)
            .ToList();

        return ownedRecords
            .Select((record, index) =>
            {
                var batchId = string.IsNullOrWhiteSpace(record.BatchId)
                    ? $"legacy-{index + 1}"
                    : record.BatchId;
                var usedQuantity = History
                    .Where(history => history.Action == "Used" && history.SourceBatchId == batchId)
                    .Sum(history => history.Quantity);
                var returnedQuantity = History
                    .Where(history => history.Action == "Put Back" && history.SourceBatchId == batchId)
                    .Sum(history => history.Quantity);
                var remainingQuantity = Math.Max(0, record.Quantity - usedQuantity + returnedQuantity);
                var expiry = record.ExpiryDate;
                return new TrackStockBatch
                {
                    BatchId = batchId,
                    ExpiryDate = expiry,
                    OwnedDate = record.Date,
                    Location = record.Location,
                    Remark = record.Remark,
                    OriginalQuantity = record.Quantity,
                    UsedQuantity = usedQuantity,
                    ReturnedQuantity = returnedQuantity,
                    RemainingQuantity = remainingQuantity,
                    Display = BuildBatchDisplay(index + 1, record, remainingQuantity, expiry)
                };
            })
            .ToList();
    }

    public static void ConfigureAlerts(TrackSettings settings)
    {
        AlertSettings = settings;
    }

    private DateTime AddChangeInterval(DateTime date)
    {
        return ChangeUnit switch
        {
            ChangeIntervalUnits.Days => date.AddDays(ChangeEvery),
            ChangeIntervalUnits.Weeks => date.AddDays(ChangeEvery * 7),
            ChangeIntervalUnits.Months => date.AddMonths(ChangeEvery),
            ChangeIntervalUnits.Years => date.AddYears(ChangeEvery),
            _ => date.AddMonths(ChangeEvery)
        };
    }

    private static string DescribeDuration(int days)
    {
        return days switch
        {
            7 => "1 week",
            30 => "1 month",
            60 => "2 months",
            _ when days % 30 == 0 => $"{days / 30} months",
            _ when days % 7 == 0 => $"{days / 7} weeks",
            1 => "1 day",
            _ => $"{days} days"
        };
    }

    private static string BuildBatchDisplay(int index, TrackItemHistory record, decimal remainingQuantity, DateTime? expiry)
    {
        var parts = new List<string>
        {
            $"Record {index}",
            $"{record.Date:dd MMM yyyy}",
            $"{remainingQuantity}/{record.Quantity} left"
        };

        if (expiry is not null)
        {
            parts.Add($"exp {expiry.Value:dd MMM yyyy}");
        }

        if (!string.IsNullOrWhiteSpace(record.Location))
        {
            parts.Add(record.Location);
        }

        if (!string.IsNullOrWhiteSpace(record.Remark))
        {
            parts.Add(record.Remark);
        }

        return string.Join(" | ", parts);
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

    private static string GetFirstLine(string value)
    {
        return value.Replace("\r\n", "\n").Split('\n')[0];
    }

    private static bool HasMultipleLines(string value)
    {
        return value.Contains('\n') || value.Contains('\r');
    }
}

public sealed class TrackItemHistory
{
    public DateTime Date { get; set; } = DateTime.Today;
    public DateTime RecordedAt { get; set; } = DateTime.Now;
    public string Action { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;
    public string SourceBatchId { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }

    [JsonIgnore]
    public string BatchLabel => FormatBatchLabel(BatchId);

    [JsonIgnore]
    public string SourceBatchLabel => FormatBatchLabel(SourceBatchId);

    [JsonIgnore]
    public string StockReferenceLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BatchLabel))
            {
                return SourceBatchLabel;
            }

            if (string.IsNullOrWhiteSpace(SourceBatchLabel))
            {
                return BatchLabel;
            }

            return $"{BatchLabel} / {SourceBatchLabel}";
        }
    }

    [JsonIgnore]
    public string StockReferenceDisplay
    {
        get
        {
            var stockLabel = StockReferenceLabel;
            if (string.IsNullOrWhiteSpace(stockLabel))
            {
                return string.Empty;
            }

            return Action switch
            {
                "Owned" => $"Batch {stockLabel}",
                "Used" => $"From {stockLabel}",
                "Put Back" => $"To {stockLabel}",
                _ => stockLabel
            };
        }
    }

    [JsonIgnore]
    public string ActionDisplay => Action switch
    {
        "Owned" => "Add Stock",
        "Used" => "Use Stock",
        "Put Back" => "Put Back",
        "Changed" => "Changed",
        _ => Action
    };

    [JsonIgnore]
    public string QuantityDisplay => Action switch
    {
        "Used" => $"-{Quantity}",
        "Owned" or "Put Back" => $"+{Quantity}",
        _ => Quantity.ToString()
    };

    [JsonIgnore]
    public string LocationDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Location))
            {
                return string.Empty;
            }

            return Action switch
            {
                "Owned" => $"Stored: {Location}",
                "Used" => $"Used at: {Location}",
                "Put Back" => $"Returned to: {Location}",
                _ => Location
            };
        }
    }

    [JsonIgnore]
    public string NoteDisplay => IsGenericRemark(Action, Remark)
        ? string.Empty
        : Remark;

    private static bool IsGenericRemark(string action, string remark)
    {
        if (string.IsNullOrWhiteSpace(remark))
        {
            return true;
        }

        return action switch
        {
            "Owned" => IsAnyRemark(remark, "Add stock", "Owned"),
            "Used" => IsAnyRemark(remark, "Used", "Use stock"),
            "Put Back" => IsAnyRemark(remark, "Return", "Returned", "Put back"),
            "Changed" => IsAnyRemark(remark, "Changed", "Replace"),
            _ => false
        };
    }

    private static bool IsAnyRemark(string remark, params string[] values)
    {
        return values.Any(value => string.Equals(remark.Trim(), value, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatBatchLabel(string batchId)
    {
        if (string.IsNullOrWhiteSpace(batchId))
        {
            return string.Empty;
        }

        if (batchId.StartsWith("batch-", StringComparison.OrdinalIgnoreCase))
        {
            var numberText = batchId.Split('-', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
            if (int.TryParse(numberText, out var number))
            {
                return $"Record {number}";
            }
        }

        return batchId.Length <= 8 ? batchId : batchId[..8];
    }
}

public static class TrackItemTypes
{
    public const string QuantityUsage = "Quantity";
    public const string ChangeCycle = "Change Cycle";
}

public static class ChangeIntervalUnits
{
    public const string Days = "Days";
    public const string Weeks = "Weeks";
    public const string Months = "Months";
    public const string Years = "Years";
}

public sealed class ObservableTrackHistory : System.Collections.ObjectModel.ObservableCollection<TrackItemHistory>
{
    public ObservableTrackHistory()
    {
    }

    public ObservableTrackHistory(IEnumerable<TrackItemHistory> items)
        : base(items.ToList())
    {
    }
}
