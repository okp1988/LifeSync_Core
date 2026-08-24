using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LifeSyncTaskClient.Models;

public static class TaskFilterIds
{
    public const string All = "ALL";
    public const string Default = "DEFAULT";
}

public sealed class TaskFilterDefinition : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isFavourite;

    public string FilterId { get; set; } = Guid.NewGuid().ToString("D");
    public string Name
    {
        get => _name;
        set { if (_name == value) return; _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }
    public bool IsSystem { get; set; }
    public bool IsFavourite
    {
        get => _isFavourite;
        set { if (_isFavourite == value) return; _isFavourite = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }
    public int SortOrder { get; set; }
    public List<string> TaskIds { get; set; } = [];
    public string DisplayName => IsFavourite ? $"★ {Name}" : Name;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class TaskFilterState
{
    public List<TaskFilterDefinition> Filters { get; set; } = [];
    public bool PendingUpload { get; set; }
}

public sealed class FilterTaskSelectionItem : INotifyPropertyChanged
{
    private bool _isChecked;
    public required SheetTask Task { get; init; }
    public bool IsEnabled { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    public bool IsChecked
    {
        get => _isChecked;
        set { if (_isChecked == value) return; _isChecked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
