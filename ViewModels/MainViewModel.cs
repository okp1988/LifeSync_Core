using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using LifeSyncTaskClient.Models;
using LifeSyncTaskClient.Services;

namespace LifeSyncTaskClient.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const string AllFilter = "ALL";
    private readonly JsonFileStore _fileStore = new();
    private readonly GoogleSheetClient _sheetClient = new();
    private readonly ObservableCollection<SheetTask> _tasks = [];
    private AppConfig _config = new();
    private SheetTask? _selectedTask;
    private string _categoryFilter = AllFilter;
    private string _typeFilter = AllFilter;
    private string _statusFilter = AllFilter;
    private string _dayLeftFilter = AllFilter;
    private bool _isLoadingTasks;
    private bool _isMarkingComplete;
    private bool _isTaskSidebarOpen;
    private string _message = "Ready";
    private string _selectedRemarkDraft = string.Empty;
    private DateTime? _completionDate = DateTime.Today;

    public MainViewModel()
    {
        TasksView = CollectionViewSource.GetDefaultView(_tasks);
        TasksView.Filter = FilterTask;
        ApplyDefaultSort();

        RequestTasksCommand = new RelayCommand(RequestTasksAsync, CanUseNetwork);
        RefreshCalculatedFieldsCommand = new RelayCommand(RefreshCalculatedFieldsAsync);
        MarkCompleteCommand = new RelayCommand(MarkCompleteAsync, CanMutateSelectedTask);
        ClearFiltersCommand = new RelayCommand(ClearFiltersAsync);
        CloseTaskSidebarCommand = new RelayCommand(CloseTaskSidebarAsync);
    }

    public ICollectionView TasksView { get; }
    public ObservableCollection<string> Categories { get; } = [AllFilter];
    public ObservableCollection<string> Types { get; } = [AllFilter];
    public string[] Statuses { get; } = [AllFilter, "Normal", "Warning", "Expired"];
    public string[] DayLeftFilters { get; } = [AllFilter, "Overdue", "Due today", "Next 7 days", "More than 7 days"];

    public RelayCommand RequestTasksCommand { get; }
    public RelayCommand RefreshCalculatedFieldsCommand { get; }
    public RelayCommand MarkCompleteCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand CloseTaskSidebarCommand { get; }

    public string GoogleAppsScriptUrl
    {
        get => _config.GoogleAppsScriptUrl;
        set
        {
            if (_config.GoogleAppsScriptUrl == value)
            {
                return;
            }

            _config.GoogleAppsScriptUrl = value;
            OnPropertyChanged();
        }
    }

    public string ApiKey
    {
        get => _config.ApiKey;
        set
        {
            if (_config.ApiKey == value)
            {
                return;
            }

            _config.ApiKey = value;
            OnPropertyChanged();
        }
    }

    public int LogRetentionDays
    {
        get => _config.LogRetentionDays;
        set
        {
            var normalizedValue = Math.Max(1, value);
            if (_config.LogRetentionDays == normalizedValue)
            {
                return;
            }

            _config.LogRetentionDays = normalizedValue;
            OnPropertyChanged();
        }
    }

    public SheetTask? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (_selectedTask == value)
            {
                return;
            }

            _selectedTask = value;
            SelectedRemarkDraft = value?.Remark ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedTaskPath));
            OnPropertyChanged(nameof(SelectedTaskDateDisplay));
            OnPropertyChanged(nameof(SelectedTaskDayLeftDisplay));
            IsTaskSidebarOpen = value is not null;
            RefreshCommands();
        }
    }

    public string CategoryFilter
    {
        get => _categoryFilter;
        set
        {
            value = NormalizeFilter(value);
            if (_categoryFilter == value)
            {
                return;
            }

            _categoryFilter = value;
            OnPropertyChanged();
            TasksView.Refresh();
        }
    }

    public string TypeFilter
    {
        get => _typeFilter;
        set
        {
            value = NormalizeFilter(value);
            if (_typeFilter == value)
            {
                return;
            }

            _typeFilter = value;
            OnPropertyChanged();
            TasksView.Refresh();
        }
    }

    public string StatusFilter
    {
        get => _statusFilter;
        set
        {
            value = NormalizeFilter(value);
            if (_statusFilter == value)
            {
                return;
            }

            _statusFilter = value;
            OnPropertyChanged();
            TasksView.Refresh();
        }
    }

    public string DayLeftFilter
    {
        get => _dayLeftFilter;
        set
        {
            value = NormalizeFilter(value);
            if (_dayLeftFilter == value)
            {
                return;
            }

            _dayLeftFilter = value;
            OnPropertyChanged();
            TasksView.Refresh();
        }
    }

    public bool IsLoadingTasks
    {
        get => _isLoadingTasks;
        private set => SetBusyFlag(ref _isLoadingTasks, value);
    }

    public bool IsMarkingComplete
    {
        get => _isMarkingComplete;
        private set => SetBusyFlag(ref _isMarkingComplete, value);
    }

    public bool IsBusy => IsLoadingTasks || IsMarkingComplete;

    public bool IsTaskSidebarOpen
    {
        get => _isTaskSidebarOpen;
        private set
        {
            if (_isTaskSidebarOpen == value)
            {
                return;
            }

            _isTaskSidebarOpen = value;
            OnPropertyChanged();
        }
    }

    public string Message
    {
        get => _message;
        private set
        {
            if (_message == value)
            {
                return;
            }

            _message = value;
            OnPropertyChanged();
        }
    }

    public string SelectedRemarkDraft
    {
        get => _selectedRemarkDraft;
        set
        {
            if (_selectedRemarkDraft == value)
            {
                return;
            }

            _selectedRemarkDraft = value;
            OnPropertyChanged();
        }
    }

    public string SelectedTaskPath
    {
        get
        {
            if (SelectedTask is null)
            {
                return string.Empty;
            }

            return string.Join(" / ", new[] { SelectedTask.Category, SelectedTask.Type, SelectedTask.Task }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }

    public string SelectedTaskDateDisplay
    {
        get
        {
            if (SelectedTask is null)
            {
                return string.Empty;
            }

            var expiredDate = SelectedTask.ExpiredDate?.Date;
            var warningDate = SelectedTask.WarningDate?.Date;
            var lastExecutedDate = SelectedTask.PreviousDate1?.Date;

            if (expiredDate is null && warningDate is null && lastExecutedDate is null)
            {
                return "No dates";
            }

            string leD = "";
            if (lastExecutedDate is not null)
            {
                leD = $" / L: {FormatDate(lastExecutedDate.Value)}";
            }

            if (expiredDate is not null && warningDate is not null && expiredDate == warningDate)
            {
                return $"E: {FormatDate(expiredDate.Value)}{leD}";
            }

            if (expiredDate is not null && warningDate is not null)
            {
                return $"E: {FormatDate(expiredDate.Value)} / W: {FormatDate(warningDate.Value)}{leD}";
            }

            if (expiredDate is not null || warningDate is not null)
            {
                return $"{FormatDate((expiredDate ?? warningDate)!.Value)}{leD}";
            }

            return $"L: {FormatDate(lastExecutedDate!.Value)}";
        }
    }

    public string SelectedTaskDayLeftDisplay
    {
        get
        {
            return SelectedTask?.DayLeft switch
            {
                null => string.Empty,
                < 0 => $"({Math.Abs(SelectedTask.DayLeft.Value)} day(s) overdue)",
                0 => "(due today)",
                1 => "(1 day left)",
                var days => $"({days} days left)"
            };
        }
    }

    public DateTime? CompletionDate
    {
        get => _completionDate;
        set
        {
            if (_completionDate == value)
            {
                return;
            }

            _completionDate = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync()
    {
        try
        {
            _config = await _fileStore.LoadConfigAsync();
            if (_config.LogRetentionDays < 1)
            {
                _config.LogRetentionDays = 30;
            }

            AppLogger.PruneOldFiles(_config.LogRetentionDays);
            OnPropertyChanged(nameof(GoogleAppsScriptUrl));
            OnPropertyChanged(nameof(ApiKey));
            OnPropertyChanged(nameof(LogRetentionDays));

            var cachedTasks = await _fileStore.LoadTasksAsync();
            ReplaceTasks(cachedTasks);
            ResetFilters();
            AppLogger.Info($"Loaded {cachedTasks.Count} cached task(s) from {AppPaths.TaskCachePath}");
            Message = cachedTasks.Count == 0
                ? $"No cached tasks. Press Request to retrieve from Google Sheet. Data: {AppPaths.DataDirectory}"
                : $"Loaded {cachedTasks.Count} cached task(s). Data: {AppPaths.DataDirectory}";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to load local cache", ex);
            Message = $"Failed to load local cache: {ex.Message}. Data: {AppPaths.DataDirectory}";
        }
    }

    private async Task RequestTasksAsync()
    {
        IsLoadingTasks = true;
        Message = "Loading tasks from Google Sheet...";

        try
        {
            await SaveConfigAsync();
            var tasks = await _sheetClient.GetTasksAsync(_config, CancellationToken.None);
            ReplaceTasks(tasks);
            ResetFilters();
            await _fileStore.SaveTasksAsync(_tasks);
            AppLogger.Info($"Requested and cached {_tasks.Count} task(s) to {AppPaths.TaskCachePath}");
            Message = _tasks.Count == 0
                ? "Google Sheet returned 0 task(s). Check Completed checkboxes and the Apps Script task filter."
                : $"Loaded and cached {_tasks.Count} task(s).";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed network/API call while loading tasks", ex);
            Message = $"Failed network/API call while loading tasks: {ex.Message}. Log: {AppPaths.CurrentWarningErrorLogPath}";
        }
        finally
        {
            IsLoadingTasks = false;
        }
    }

    private async Task MarkCompleteAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var taskToComplete = SelectedTask;
        IsMarkingComplete = true;
        Message = "Marking complete...";

        try
        {
            await SaveConfigAsync();
            await _sheetClient.CompleteAsync(_config, taskToComplete, CompletionDate ?? DateTime.Today, SelectedRemarkDraft, CancellationToken.None);

            _tasks.Remove(taskToComplete);
            await _fileStore.SaveTasksAsync(_tasks);
            RebuildFilterLists();
            SelectedTask = null;
            TasksView.Refresh();
            AppLogger.Info($"Completed row {taskToComplete.RowNumber} and removed it from local cache");
            Message = "Completed in Google Sheet and removed from local JSON cache.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed network/API call while marking complete", ex);
            Message = $"Failed network/API call while marking complete: {ex.Message}. Log: {AppPaths.CurrentWarningErrorLogPath}";
        }
        finally
        {
            IsMarkingComplete = false;
        }
    }

    private Task ClearFiltersAsync()
    {
        ResetFilters();
        return Task.CompletedTask;
    }

    private Task CloseTaskSidebarAsync()
    {
        IsTaskSidebarOpen = false;
        SelectedTask = null;
        SelectedRemarkDraft = string.Empty;
        return Task.CompletedTask;
    }

    private async Task RefreshCalculatedFieldsAsync()
    {
        foreach (var task in _tasks)
        {
            task.NotifyCalculatedFieldsChanged();
        }

        ApplyDefaultSort();
        TasksView.Refresh();
        await _fileStore.SaveTasksAsync(_tasks);
        AppLogger.Info($"Recalculated day left for {_tasks.Count} cached task(s)");
        Message = $"Recalculated day left for {_tasks.Count} cached task(s).";
    }

    private void ResetFilters()
    {
        _categoryFilter = AllFilter;
        _typeFilter = AllFilter;
        _statusFilter = AllFilter;
        _dayLeftFilter = AllFilter;
        OnPropertyChanged(nameof(CategoryFilter));
        OnPropertyChanged(nameof(TypeFilter));
        OnPropertyChanged(nameof(StatusFilter));
        OnPropertyChanged(nameof(DayLeftFilter));
        TasksView.Refresh();
    }

    private async Task SaveConfigAsync()
    {
        await _fileStore.SaveConfigAsync(_config);
    }

    private void ReplaceTasks(IEnumerable<SheetTask> tasks)
    {
        _tasks.Clear();
        foreach (var task in tasks)
        {
            _tasks.Add(task);
        }

        RebuildFilterLists();
        ApplyDefaultSort();
        TasksView.Refresh();
    }

    private void ApplyDefaultSort()
    {
        TasksView.SortDescriptions.Clear();
        TasksView.SortDescriptions.Add(new SortDescription(nameof(SheetTask.Category), ListSortDirection.Ascending));
        TasksView.SortDescriptions.Add(new SortDescription(nameof(SheetTask.Type), ListSortDirection.Ascending));
        TasksView.SortDescriptions.Add(new SortDescription(nameof(SheetTask.Task), ListSortDirection.Ascending));
    }

    private void RebuildFilterLists()
    {
        RebuildList(Categories, _tasks.Select(task => task.Category));
        RebuildList(Types, _tasks.Select(task => task.Type));
    }

    private static void RebuildList(ObservableCollection<string> target, IEnumerable<string> values)
    {
        var selectedValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        target.Clear();
        target.Add(AllFilter);
        foreach (var value in selectedValues)
        {
            target.Add(value);
        }
    }

    private bool FilterTask(object item)
    {
        if (item is not SheetTask task)
        {
            return false;
        }

        return Matches(CategoryFilter, task.Category)
            && Matches(TypeFilter, task.Type)
            && Matches(StatusFilter, task.Status)
            && MatchesDayLeft(task.DayLeft);
    }

    private static bool Matches(string filter, string value)
    {
        return filter == AllFilter || string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? AllFilter : value;
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToString("yyyy-MM-dd");
    }

    private bool MatchesDayLeft(int? dayLeft)
    {
        return DayLeftFilter switch
        {
            "Overdue" => dayLeft is < 0,
            "Due today" => dayLeft == 0,
            "Next 7 days" => dayLeft is >= 0 and <= 7,
            "More than 7 days" => dayLeft is > 7,
            _ => true
        };
    }

    private bool CanUseNetwork() => !IsBusy;

    private bool CanMutateSelectedTask()
    {
        return !IsBusy && SelectedTask is not null;
    }

    private void SetBusyFlag(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(IsBusy));
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        RequestTasksCommand.RaiseCanExecuteChanged();
        MarkCompleteCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
