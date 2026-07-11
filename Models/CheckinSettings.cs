using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LifeSyncTaskClient.Models;

public sealed class CheckinSettings
{
    public DateTime? LastCheckinAt { get; set; }
    public DateTime? LastAlertDate { get; set; }
    public ObservableCollection<CheckinDaySetting> Days { get; set; } = [];
}

public sealed class CheckinDaySetting : INotifyPropertyChanged
{
    private bool _isEnabled = true;
    private string _timeText = "1200";

    public DayOfWeek DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    public string TimeText
    {
        get => _timeText;
        set
        {
            var normalizedValue = value?.Trim() ?? string.Empty;
            if (_timeText == normalizedValue)
            {
                return;
            }

            _timeText = normalizedValue;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
