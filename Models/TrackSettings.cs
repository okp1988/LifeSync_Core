using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace LifeSyncTaskClient.Models;

public sealed class TrackSettings : INotifyPropertyChanged
{
    private decimal _lowStockThreshold = 2;
    private decimal _criticalStockThreshold = 1;
    private decimal _outStockThreshold = 0;
    private string _lowStockColor = TrackSettingColors.Yellow;
    private string _criticalStockColor = TrackSettingColors.Orange;
    private string _outStockColor = TrackSettingColors.Red;
    private string _expiryReminderText = "2 months, 1 month, 1 week";
    private string _expiryTwoMonthColor = TrackSettingColors.Blue;
    private string _expiryOneMonthColor = TrackSettingColors.Yellow;
    private string _expiryOneWeekColor = TrackSettingColors.Orange;
    private string _expiryExpiredColor = TrackSettingColors.Red;
    private ObservableCollection<ExpiryAlertSetting> _expiryAlerts = [];
    private List<string> _shownExpiryAlertKeys = [];

    public decimal LowStockThreshold
    {
        get => _lowStockThreshold;
        set => SetField(ref _lowStockThreshold, Math.Max(0, value));
    }

    public decimal CriticalStockThreshold
    {
        get => _criticalStockThreshold;
        set => SetField(ref _criticalStockThreshold, Math.Max(0, value));
    }

    public decimal OutStockThreshold
    {
        get => _outStockThreshold;
        set => SetField(ref _outStockThreshold, Math.Max(0, value));
    }

    public string LowStockColor
    {
        get => _lowStockColor;
        set => SetField(ref _lowStockColor, value);
    }

    public string CriticalStockColor
    {
        get => _criticalStockColor;
        set => SetField(ref _criticalStockColor, value);
    }

    public string OutStockColor
    {
        get => _outStockColor;
        set => SetField(ref _outStockColor, value);
    }

    public string ExpiryReminderText
    {
        get => ExpiryAlerts.Count > 0
            ? string.Join(", ", ExpiryAlerts.Select(alert => alert.ReminderText))
            : _expiryReminderText;
        set => SetField(ref _expiryReminderText, value);
    }

    public string ExpiryTwoMonthColor
    {
        get => _expiryTwoMonthColor;
        set => SetField(ref _expiryTwoMonthColor, value);
    }

    public string ExpiryOneMonthColor
    {
        get => _expiryOneMonthColor;
        set => SetField(ref _expiryOneMonthColor, value);
    }

    public string ExpiryOneWeekColor
    {
        get => _expiryOneWeekColor;
        set => SetField(ref _expiryOneWeekColor, value);
    }

    public string ExpiryExpiredColor
    {
        get => _expiryExpiredColor;
        set => SetField(ref _expiryExpiredColor, value);
    }

    public ObservableCollection<ExpiryAlertSetting> ExpiryAlerts
    {
        get => _expiryAlerts;
        set => SetField(ref _expiryAlerts, value);
    }

    public List<string> ShownExpiryAlertKeys
    {
        get => _shownExpiryAlertKeys;
        set => SetField(ref _shownExpiryAlertKeys, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ExpiryAlertSetting? GetExpiryAlertForDays(int daysUntilExpiry, int? stockLifetimeDays = null)
    {
        if (daysUntilExpiry < 0)
        {
            return null;
        }

        return ExpiryAlerts
            .Where(alert => alert.Days > 0 && daysUntilExpiry <= alert.Days)
            .Where(alert => stockLifetimeDays is null || alert.Days < stockLifetimeDays.Value)
            .OrderBy(alert => alert.Days)
            .FirstOrDefault();
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ExpiryAlertSetting : INotifyPropertyChanged
{
    private int _amount = 1;
    private string _unit = ExpiryAlertUnits.Month;
    private string _color = TrackSettingColors.Yellow;

    public int Amount
    {
        get => _amount;
        set
        {
            if (SetField(ref _amount, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(Days));
                OnPropertyChanged(nameof(ReminderText));
            }
        }
    }

    public string Unit
    {
        get => _unit;
        set
        {
            if (SetField(ref _unit, value))
            {
                OnPropertyChanged(nameof(Days));
                OnPropertyChanged(nameof(ReminderText));
            }
        }
    }

    public string Color
    {
        get => _color;
        set => SetField(ref _color, value);
    }

    public int Days => Unit switch
    {
        ExpiryAlertUnits.Day => Amount,
        ExpiryAlertUnits.Week => Amount * 7,
        ExpiryAlertUnits.Month => Amount * 30,
        ExpiryAlertUnits.Year => Amount * 365,
        _ => Amount
    };

    public string ReminderText => $"{Amount} {Unit.ToLowerInvariant()}{(Amount == 1 ? string.Empty : "s")}";

    public event PropertyChangedEventHandler? PropertyChanged;

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

public static class ExpiryAlertUnits
{
    public const string Day = "Day";
    public const string Week = "Week";
    public const string Month = "Month";
    public const string Year = "Year";
    public static string[] Choices { get; } = [Day, Week, Month, Year];
}

public static class TrackSettingColors
{
    public const string Yellow = "Yellow";
    public const string Orange = "Orange";
    public const string Red = "Red";
    public const string DarkGray = "DarkGray";
    public const string Green = "Green";
    public const string Blue = "Blue";
    public static string[] Choices { get; } = [Yellow, Orange, Red, DarkGray, Green, Blue];

    public static string ToHighlightBrush(string? value)
    {
        return value?.Trim() switch
        {
            Yellow or "#FFF6D8" or "#FFF8D8" => "#FFF8D8",
            Orange or "#FDEECC" or "#FDEACC" => "#FDEACC",
            Red or "#FDECEC" or "#FCE8E8" => "#FCE8E8",
            DarkGray or "DarkGrey" or "Gray" or "Grey" or "#EEF1F4" => "#EEF1F4",
            Green or "#EAF7EF" => "#EAF7EF",
            Blue or "#EEF4FF" or "#EAF2FF" => "#EAF2FF",
            _ => "Transparent"
        };
    }
}

public sealed class TrackStockBatch
{
    public string BatchId { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public DateTime OwnedDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public decimal OriginalQuantity { get; set; }
    public decimal UsedQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
