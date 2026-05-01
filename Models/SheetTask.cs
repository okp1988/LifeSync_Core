using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace LifeSyncTaskClient.Models;

public sealed class SheetTask : INotifyPropertyChanged
{
    private string _remark = string.Empty;

    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public DateTime? ExpiredDate { get; set; }
    public DateTime? WarningDate { get; set; }
    public DateTime? PreviousDate1 { get; set; }
    public DateTime? PreviousDate2 { get; set; }

    public int? DayLeft => ExpiredDate is null
        ? null
        : (ExpiredDate.Value.Date - DateTime.Today).Days;

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
        }
    }

    public DateTime? LastExecutedDate { get; set; }
    public int RowNumber { get; set; }
    public string TrackId { get; set; } = string.Empty;

    [JsonIgnore]
    public string Status
    {
        get
        {
            var today = DateTime.Today;

            if (ExpiredDate is not null && ExpiredDate.Value.Date <= today)
            {
                return "Expired";
            }

            if (WarningDate is not null && WarningDate.Value.Date <= today)
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
        OnPropertyChanged(nameof(Status));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
