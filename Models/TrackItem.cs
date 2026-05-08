using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace LifeSyncTaskClient.Models;

public sealed class TrackItem : INotifyPropertyChanged
{
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
            }
        }
    }

    [JsonIgnore]
    public decimal LeftQuantity => Math.Max(0, TotalQuantity - UsedQuantity);

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
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetField(ref _notes, value);
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

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyCalculatedFieldsChanged()
    {
        OnPropertyChanged(nameof(LeftQuantity));
        OnPropertyChanged(nameof(IsQuantityUsage));
        OnPropertyChanged(nameof(IsChangeCycle));
        OnPropertyChanged(nameof(NextChangeDate));
        OnPropertyChanged(nameof(DaysUntilChange));
        OnPropertyChanged(nameof(ChangeStatus));
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

public sealed class TrackItemHistory
{
    public DateTime Date { get; set; } = DateTime.Today;
    public string Action { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
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
