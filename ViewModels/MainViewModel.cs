using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using LifeSyncTaskClient.Models;
using LifeSyncTaskClient.Services;

namespace LifeSyncTaskClient.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const string AllFilter = "ALL";
    private readonly JsonFileStore _fileStore = new();
    private readonly GoogleSheetClient _sheetClient = new();
    private readonly ObservableCollection<SheetTask> _tasks = [];
    private readonly List<TaskMutation> _taskSyncQueue = [];
    private readonly SemaphoreSlim _taskSyncGate = new(1, 1);
    private readonly DispatcherTimer _checkinTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private AppConfig _config = new();
    private CheckinSettings _checkinSettings = new();
    private SheetTask? _selectedTask;
    private string _categoryFilter = AllFilter;
    private string _typeFilter = AllFilter;
    private string _statusFilter = AllFilter;
    private string _dayLeftFilter = AllFilter;
    private bool _isLoadingTasks;
    private bool _isMarkingComplete;
    private bool _isTaskSidebarOpen;
    private bool _isTaskSummaryOpen;
    private bool _isTaskEditorOpen;
    private bool _isNewTaskDraft;
    private bool _isTaskConflictOpen;
    private TaskEditDraft _taskEditDraft = new();
    private ObservableCollection<TaskMutation> _taskConflicts = [];
    private TaskMutation? _selectedTaskConflict;
    private string _message = "Ready";
    private bool _isCheckinSettingsOpen;
    private bool _isUpdatingCheckinAllDays;
    private DateTime _lastCheckinDisplayRefreshDate = DateTime.MinValue;
    private DateTime _lastTaskCalculatedFieldsRefreshDate = DateTime.Today;
    private string _selectedRemarkDraft = string.Empty;
    private DateTime? _completionDate = DateTime.Today;
    private ObservableCollection<string> _categories = [AllFilter];
    private ObservableCollection<string> _types = [AllFilter];
    private ObservableCollection<TaskSummaryItem> _expiredTaskSummaryItems = [];
    private ObservableCollection<TaskSummaryItem> _warningTaskSummaryItems = [];
    private TaskSummaryItem? _selectedTaskSummaryItem;
    private TaskSummaryItem? _selectedExpiredTaskSummaryItem;
    private TaskSummaryItem? _selectedWarningTaskSummaryItem;
    private DateTime? _taskSummaryCustomSnoozeUntil = DateTime.Today.AddDays(7);
    private string _taskSummarySnoozeNote = string.Empty;

    public MainViewModel()
    {
        TasksView = CollectionViewSource.GetDefaultView(_tasks);
        TasksView.Filter = FilterTask;
        ApplyDefaultSort();

        RequestTasksCommand = new RelayCommand(RequestTasksAsync, CanUseNetwork);
        MarkCompleteCommand = new RelayCommand(MarkCompleteAsync, CanMutateSelectedTask);
        ClearFiltersCommand = new RelayCommand(ClearFiltersAsync);
        CloseTaskSidebarCommand = new RelayCommand(CloseTaskSidebarAsync);
        OpenSelectedTaskSidebarCommand = new RelayCommand(OpenSelectedTaskSidebarAsync, CanOpenSelectedTaskSidebar);
        NewTaskCommand = new RelayCommand(NewTaskAsync);
        EditTaskCommand = new RelayCommand(EditTaskAsync, CanEditSelectedTask);
        SaveTaskCommand = new RelayCommand(SaveTaskAsync);
        CancelTaskEditCommand = new RelayCommand(CancelTaskEditAsync);
        ArchiveTaskCommand = new RelayCommand(ArchiveTaskAsync, CanEditSelectedTask);
        OpenTaskConflictsCommand = new RelayCommand(OpenTaskConflictsAsync, HasTaskConflicts);
        CloseTaskConflictsCommand = new RelayCommand(CloseTaskConflictsAsync);
        KeepPcTaskConflictCommand = new RelayCommand(KeepPcTaskConflictAsync, HasSelectedTaskConflict);
        UseSheetTaskConflictCommand = new RelayCommand(UseSheetTaskConflictAsync, HasSelectedTaskConflict);
        OpenTaskSummaryCommand = new RelayCommand(OpenTaskSummaryAsync);
        CloseTaskSummaryCommand = new RelayCommand(CloseTaskSummaryAsync);
        OpenSelectedTaskSummaryItemCommand = new RelayCommand(OpenSelectedTaskSummaryItemAsync, HasSelectedTaskSummaryItem);
        SnoozeTaskSummary1DayCommand = new RelayCommand(() => SnoozeSelectedTaskSummaryItemAsync(1), CanSnoozeSelectedTaskSummaryItem);
        SnoozeTaskSummary3DaysCommand = new RelayCommand(() => SnoozeSelectedTaskSummaryItemAsync(3), CanSnoozeSelectedTaskSummaryItem);
        SnoozeTaskSummary7DaysCommand = new RelayCommand(() => SnoozeSelectedTaskSummaryItemAsync(7), CanSnoozeSelectedTaskSummaryItem);
        SnoozeTaskSummary14DaysCommand = new RelayCommand(() => SnoozeSelectedTaskSummaryItemAsync(14), CanSnoozeSelectedTaskSummaryItem);
        SnoozeTaskSummary30DaysCommand = new RelayCommand(() => SnoozeSelectedTaskSummaryItemAsync(30), CanSnoozeSelectedTaskSummaryItem);
        SnoozeTaskSummaryCustomCommand = new RelayCommand(SnoozeSelectedTaskSummaryItemToCustomDateAsync, CanSnoozeSelectedTaskSummaryItemToCustomDate);
        ClearTaskSummarySnoozeCommand = new RelayCommand(ClearSelectedTaskSummaryItemSnoozeAsync, CanClearSelectedTaskSummaryItemSnooze);
        CheckinCommand = new RelayCommand(CheckinAsync);
        OpenCheckinSettingsCommand = new RelayCommand(OpenCheckinSettingsAsync);
        SaveCheckinSettingsCommand = new RelayCommand(SaveCheckinSettingsAsync);
        CancelCheckinSettingsCommand = new RelayCommand(CancelCheckinSettingsAsync);
        CheckinSettingsDraft = new ObservableCollection<CheckinDaySetting>();
        _checkinTimer.Tick += CheckinTimer_Tick;
    }

    public ICollectionView TasksView { get; }
    public ObservableCollection<string> Categories
    {
        get => _categories;
        private set
        {
            _categories = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> Types
    {
        get => _types;
        private set
        {
            _types = value;
            OnPropertyChanged();
        }
    }

    public string[] Statuses { get; } = [AllFilter, "Normal", "Warning", "Expired", "Pending", "Warning + Expired"];
    public string[] DayLeftFilters { get; } = [AllFilter, "Overdue", "Due today", "Next 7 days", "More than 7 days"];
    public string[] TaskCycleUnits { get; } = ["Day", "Month", "Year"];

    public RelayCommand RequestTasksCommand { get; }
    public RelayCommand MarkCompleteCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand CloseTaskSidebarCommand { get; }
    public RelayCommand OpenSelectedTaskSidebarCommand { get; }
    public RelayCommand NewTaskCommand { get; }
    public RelayCommand EditTaskCommand { get; }
    public RelayCommand SaveTaskCommand { get; }
    public RelayCommand CancelTaskEditCommand { get; }
    public RelayCommand ArchiveTaskCommand { get; }
    public RelayCommand OpenTaskConflictsCommand { get; }
    public RelayCommand CloseTaskConflictsCommand { get; }
    public RelayCommand KeepPcTaskConflictCommand { get; }
    public RelayCommand UseSheetTaskConflictCommand { get; }
    public RelayCommand OpenTaskSummaryCommand { get; }
    public RelayCommand CloseTaskSummaryCommand { get; }
    public RelayCommand OpenSelectedTaskSummaryItemCommand { get; }
    public RelayCommand SnoozeTaskSummary1DayCommand { get; }
    public RelayCommand SnoozeTaskSummary3DaysCommand { get; }
    public RelayCommand SnoozeTaskSummary7DaysCommand { get; }
    public RelayCommand SnoozeTaskSummary14DaysCommand { get; }
    public RelayCommand SnoozeTaskSummary30DaysCommand { get; }
    public RelayCommand SnoozeTaskSummaryCustomCommand { get; }
    public RelayCommand ClearTaskSummarySnoozeCommand { get; }
    public RelayCommand CheckinCommand { get; }
    public RelayCommand OpenCheckinSettingsCommand { get; }
    public RelayCommand SaveCheckinSettingsCommand { get; }
    public RelayCommand CancelCheckinSettingsCommand { get; }

    public ObservableCollection<CheckinDaySetting> CheckinSettingsDraft { get; }

    public bool CheckinCheckboxValue => false;

    public string LastCheckinDisplay => _checkinSettings.LastCheckinAt is null
        ? "No check-in yet"
        : _checkinSettings.LastCheckinAt.Value.Date == DateTime.Today
            ? "TODAY"
        : _checkinSettings.LastCheckinAt.Value.ToString("dd MMM yyyy HH:mm");

    public bool IsLastCheckinStale => _checkinSettings.LastCheckinAt is not null
        && _checkinSettings.LastCheckinAt.Value.Date != DateTime.Today;

    public bool IsCheckinSettingsOpen
    {
        get => _isCheckinSettingsOpen;
        private set
        {
            if (_isCheckinSettingsOpen == value)
            {
                return;
            }

            _isCheckinSettingsOpen = value;
            OnPropertyChanged();
        }
    }

    public bool? AllCheckinDaysEnabled
    {
        get
        {
            if (CheckinSettingsDraft.Count == 0)
            {
                return false;
            }

            var enabledCount = CheckinSettingsDraft.Count(day => day.IsEnabled);
            return enabledCount switch
            {
                0 => false,
                var count when count == CheckinSettingsDraft.Count => true,
                _ => null
            };
        }
        set
        {
            var targetValue = value ?? (AllCheckinDaysEnabled == true ? false : true);

            _isUpdatingCheckinAllDays = true;
            foreach (var day in CheckinSettingsDraft)
            {
                day.IsEnabled = targetValue;
            }

            _isUpdatingCheckinAllDays = false;
            OnPropertyChanged();
        }
    }

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
            CompletionDate = DateTime.Today;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedTaskPath));
            OnPropertyChanged(nameof(SelectedTaskDateDisplay));
            OnPropertyChanged(nameof(SelectedTaskDayLeftDisplay));
            OnPropertyChanged(nameof(SelectedTaskSnoozeDisplay));
            OnPropertyChanged(nameof(SelectedTaskGoogleTaskDisplay));
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
            ApplyTaskFilters();
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
            ApplyTaskFilters();
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
            ApplyTaskFilters();
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
            ApplyTaskFilters();
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

    public bool IsBusy => IsLoadingTasks;

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

    public string SelectedTaskSnoozeDisplay => SelectedTask?.SnoozeDisplay ?? string.Empty;

    public string SelectedTaskGoogleTaskDisplay => SelectedTask?.GoogleTaskDisplay ?? string.Empty;

    public string TaskSyncDisplay => TaskConflictCount > 0
        ? $"{TaskConflictCount} conflict / {TaskPendingCount} pending"
        : TaskPendingCount > 0
            ? $"{TaskPendingCount} pending"
            : "Synced";

    public int TaskPendingCount => _taskSyncQueue.Count(item => item.State == TaskMutationStates.Pending);

    public int TaskConflictCount => _taskSyncQueue.Count(item => item.State == TaskMutationStates.Conflict);

    public bool IsTaskEditorOpen
    {
        get => _isTaskEditorOpen;
        private set
        {
            if (_isTaskEditorOpen == value) return;
            _isTaskEditorOpen = value;
            OnPropertyChanged();
        }
    }

    public TaskEditDraft TaskEditDraft
    {
        get => _taskEditDraft;
        private set
        {
            _taskEditDraft = value;
            OnPropertyChanged();
        }
    }

    public string TaskEditorTitle => _isNewTaskDraft ? "New Recurring Task" : "Edit Recurring Task";

    public bool IsTaskConflictOpen
    {
        get => _isTaskConflictOpen;
        private set
        {
            if (_isTaskConflictOpen == value) return;
            _isTaskConflictOpen = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<TaskMutation> TaskConflicts
    {
        get => _taskConflicts;
        private set
        {
            _taskConflicts = value;
            OnPropertyChanged();
        }
    }

    public TaskMutation? SelectedTaskConflict
    {
        get => _selectedTaskConflict;
        set
        {
            if (_selectedTaskConflict == value) return;
            _selectedTaskConflict = value;
            OnPropertyChanged();
            RefreshCommands();
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

    public bool IsTaskSummaryOpen
    {
        get => _isTaskSummaryOpen;
        private set
        {
            if (_isTaskSummaryOpen == value)
            {
                return;
            }

            _isTaskSummaryOpen = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<TaskSummaryItem> ExpiredTaskSummaryItems
    {
        get => _expiredTaskSummaryItems;
        private set
        {
            _expiredTaskSummaryItems = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasExpiredTaskSummaryItems));
            OnPropertyChanged(nameof(TaskSummaryDisplay));
        }
    }

    public ObservableCollection<TaskSummaryItem> WarningTaskSummaryItems
    {
        get => _warningTaskSummaryItems;
        private set
        {
            _warningTaskSummaryItems = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasWarningTaskSummaryItems));
            OnPropertyChanged(nameof(TaskSummaryDisplay));
        }
    }

    public TaskSummaryItem? SelectedTaskSummaryItem
    {
        get => _selectedTaskSummaryItem;
        set
        {
            if (_selectedTaskSummaryItem == value)
            {
                return;
            }

            _selectedTaskSummaryItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedTaskSummaryDisplay));
            RefreshCommands();
        }
    }

    public TaskSummaryItem? SelectedExpiredTaskSummaryItem
    {
        get => _selectedExpiredTaskSummaryItem;
        set
        {
            if (_selectedExpiredTaskSummaryItem == value)
            {
                return;
            }

            _selectedExpiredTaskSummaryItem = value;
            OnPropertyChanged();
            if (value is not null)
            {
                _selectedWarningTaskSummaryItem = null;
                OnPropertyChanged(nameof(SelectedWarningTaskSummaryItem));
                SelectedTaskSummaryItem = value;
            }
        }
    }

    public TaskSummaryItem? SelectedWarningTaskSummaryItem
    {
        get => _selectedWarningTaskSummaryItem;
        set
        {
            if (_selectedWarningTaskSummaryItem == value)
            {
                return;
            }

            _selectedWarningTaskSummaryItem = value;
            OnPropertyChanged();
            if (value is not null)
            {
                _selectedExpiredTaskSummaryItem = null;
                OnPropertyChanged(nameof(SelectedExpiredTaskSummaryItem));
                SelectedTaskSummaryItem = value;
            }
        }
    }

    public DateTime? TaskSummaryCustomSnoozeUntil
    {
        get => _taskSummaryCustomSnoozeUntil;
        set
        {
            if (_taskSummaryCustomSnoozeUntil == value)
            {
                return;
            }

            _taskSummaryCustomSnoozeUntil = value;
            OnPropertyChanged();
            RefreshCommands();
        }
    }

    public string TaskSummarySnoozeNote
    {
        get => _taskSummarySnoozeNote;
        set
        {
            if (_taskSummarySnoozeNote == value)
            {
                return;
            }

            _taskSummarySnoozeNote = value;
            OnPropertyChanged();
        }
    }

    public bool HasExpiredTaskSummaryItems => ExpiredTaskSummaryItems.Count > 0;

    public bool HasWarningTaskSummaryItems => WarningTaskSummaryItems.Count > 0;

    public string TaskSummaryDisplay => $"{ExpiredTaskSummaryItems.Count} expired / {WarningTaskSummaryItems.Count} warning";

    public string SelectedTaskSummaryDisplay => SelectedTaskSummaryItem is null
        ? "Select a summary item"
        : $"{SelectedTaskSummaryItem.Task} - {SelectedTaskSummaryItem.DayState}";

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

            _taskSyncQueue.Clear();
            _taskSyncQueue.AddRange(await _fileStore.LoadTaskSyncQueueAsync());
            var cachedTasks = await _fileStore.LoadTasksAsync();
            ReplaceTasks(cachedTasks);
            _checkinSettings = await _fileStore.LoadCheckinSettingsAsync();
            ResetCheckinSettingsDraft();
            ResetFilters();
            AppLogger.Info($"Loaded {cachedTasks.Count} cached task(s) from {AppPaths.TaskCachePath}");
            AppLogger.Info($"Loaded check-in settings from {AppPaths.CheckinSettingsPath}");
            Message = cachedTasks.Count == 0
                ? "No cached tasks. Press Sync to retrieve from Google Sheet."
                : $"Loaded {cachedTasks.Count} cached task(s). {TaskSyncDisplay}.";
            _checkinTimer.Start();
            RefreshCheckinDisplayIfNeeded(force: true);
            await CheckAndNotifyCheckinAsync();
            RefreshTaskSyncState();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to load local cache", ex);
            Message = $"Failed to load local cache: {ex.Message}. Data: {AppPaths.DataDirectory}";
        }
    }

    public void ClearTaskSelection()
    {
        SelectedTask = null;
        IsTaskSidebarOpen = false;
        SelectedRemarkDraft = string.Empty;
    }

    public void CloseAllPopupsAndSidebars()
    {
        ResetCheckinSettingsDraft();
        IsCheckinSettingsOpen = false;
        IsTaskSummaryOpen = false;
        IsTaskEditorOpen = false;
        IsTaskConflictOpen = false;
        SelectedTaskSummaryItem = null;
        SelectedExpiredTaskSummaryItem = null;
        SelectedWarningTaskSummaryItem = null;
        ClearTaskSelection();
    }

    public void ApplyTaskDayLeftSort(ListSortDirection? direction)
    {
        if (direction is null)
        {
            ApplyDefaultSort();
        }
        else
        {
            TasksView.SortDescriptions.Clear();
            TasksView.SortDescriptions.Add(new SortDescription(nameof(SheetTask.DayLeft), direction.Value));
        }

        TasksView.Refresh();
    }

    private async Task RequestTasksAsync()
    {
        IsLoadingTasks = true;
        Message = "Syncing queued changes and Google Sheet tasks...";

        try
        {
            await SaveConfigAsync();
            await ProcessPendingMutationsAsync();
            var tasks = await _sheetClient.GetTasksAsync(_config, CancellationToken.None);
            MergeServerTasks(tasks);
            ResetFilters();
            await _fileStore.SaveTasksAsync(_tasks);
            await _fileStore.SaveTaskSyncQueueAsync(_taskSyncQueue);
            Message = $"Sync complete. {_tasks.Count(task => !task.Archived)} task(s); {TaskSyncDisplay}.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed network/API call while loading tasks", ex);
            Message = $"Sync stopped: {ex.Message}. Pending work is still saved locally.";
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
        if (string.IsNullOrWhiteSpace(taskToComplete.TaskId))
        {
            Message = "This cached row has no stable Task ID. Deploy the new Apps Script migration and press Sync first.";
            return;
        }

        AppLogger.Info($"Mark Complete clicked for task {taskToComplete.TaskId}: {DescribeTask(taskToComplete)}");
        var confirmation = MessageBox.Show(
            $"Mark this task as completed?\n\n{DescribeTask(taskToComplete)}",
            "Confirm Complete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            AppLogger.Info($"Mark Complete cancelled for row {taskToComplete.RowNumber}: {DescribeTask(taskToComplete)}");
            Message = "Mark Complete cancelled.";
            return;
        }

        var completionDate = CompletionDate ?? DateTime.Today;
        var remark = SelectedRemarkDraft;
        IsMarkingComplete = true;
        Message = "Completing task...";

        try
        {
            var nextExpiredDate = AddTaskInterval(completionDate, taskToComplete.ExpiredValue, taskToComplete.ExpiredUnit);
            var nextWarningDate = AddTaskInterval(completionDate, taskToComplete.WarningValue, taskToComplete.WarningUnit);
            if (nextWarningDate > nextExpiredDate)
            {
                Message = "Warning interval cannot produce a date after the expired date.";
                return;
            }

            taskToComplete.Remark = remark;
            taskToComplete.LastExecutedDate = completionDate;
            taskToComplete.PreviousDate2 = taskToComplete.PreviousDate1;
            taskToComplete.PreviousDate1 = completionDate;
            taskToComplete.ExpiredDate = nextExpiredDate;
            taskToComplete.WarningDate = nextWarningDate;
            taskToComplete.SnoozeUntil = null;
            taskToComplete.SnoozeNote = string.Empty;
            taskToComplete.NotifyCalculatedFieldsChanged();
            var mutation = CreateMutation(taskToComplete, TaskMutationTypes.Complete);
            mutation.Payload.ExecuteDate = completionDate;
            mutation.Payload.Remark = remark;
            await QueueAndTryMutationAsync(mutation);
            SelectedTask = null;
            IsTaskSidebarOpen = false;
            SelectedRemarkDraft = string.Empty;
            RebuildFilterLists();
            RebuildTaskSummary();
            TasksView.Refresh();
            Message = $"Completion saved locally. {TaskSyncDisplay}.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to mark complete locally", ex);
            Message = $"Failed to mark complete locally: {ex.Message}. Log: {AppPaths.CurrentWarningErrorLogPath}";
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
        SelectedRemarkDraft = string.Empty;
        return Task.CompletedTask;
    }

    private Task OpenSelectedTaskSidebarAsync()
    {
        if (SelectedTask is null)
        {
            return Task.CompletedTask;
        }

        IsTaskSummaryOpen = false;
        IsTaskEditorOpen = false;
        IsTaskSidebarOpen = true;
        return Task.CompletedTask;
    }

    private Task OpenTaskSummaryAsync()
    {
        SelectedTask = null;
        IsTaskSidebarOpen = false;
        RebuildTaskSummary();
        IsTaskSummaryOpen = true;
        return Task.CompletedTask;
    }

    private Task CloseTaskSummaryAsync()
    {
        IsTaskSummaryOpen = false;
        SelectedTaskSummaryItem = null;
        SelectedExpiredTaskSummaryItem = null;
        SelectedWarningTaskSummaryItem = null;
        return Task.CompletedTask;
    }

    private Task OpenSelectedTaskSummaryItemAsync()
    {
        if (SelectedTaskSummaryItem is null)
        {
            return Task.CompletedTask;
        }

        var task = SelectedTaskSummaryItem.TaskItem;
        IsTaskSummaryOpen = false;
        SelectedTaskSummaryItem = null;
        SelectedExpiredTaskSummaryItem = null;
        SelectedWarningTaskSummaryItem = null;
        SelectedTask = task;
        IsTaskSidebarOpen = true;
        return Task.CompletedTask;
    }

    private Task SnoozeSelectedTaskSummaryItemAsync(int days)
    {
        return SnoozeSelectedTaskSummaryItemUntilAsync(DateTime.Today.AddDays(days));
    }

    private Task SnoozeSelectedTaskSummaryItemToCustomDateAsync()
    {
        if (TaskSummaryCustomSnoozeUntil is null)
        {
            Message = "Choose a custom snooze date first.";
            return Task.CompletedTask;
        }

        return SnoozeSelectedTaskSummaryItemUntilAsync(TaskSummaryCustomSnoozeUntil.Value.Date);
    }

    private async Task SnoozeSelectedTaskSummaryItemUntilAsync(DateTime snoozeUntil)
    {
        if (SelectedTaskSummaryItem is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedTaskSummaryItem.TaskItem.TaskId))
        {
            Message = "Cannot snooze until this task has a stable Task ID. Press Sync first.";
            return;
        }

        try
        {
            var task = SelectedTaskSummaryItem.TaskItem;
            var note = TaskSummarySnoozeNote;
            task.SnoozeUntil = snoozeUntil.Date;
            task.SnoozeNote = note;
            task.NotifyCalculatedFieldsChanged();
            var mutation = CreateMutation(task, TaskMutationTypes.Snooze);
            mutation.Payload.SnoozeUntil = snoozeUntil.Date;
            mutation.Payload.SnoozeNote = note;
            await QueueAndTryMutationAsync(mutation);
            RebuildTaskSummary();
            TasksView.Refresh();
            Message = $"Snoozed '{task.Task}' until {snoozeUntil:dd MMM yyyy}.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to snooze task", ex);
            Message = $"Failed to snooze task: {ex.Message}. Log: {AppPaths.CurrentWarningErrorLogPath}";
        }
    }

    private async Task ClearSelectedTaskSummaryItemSnoozeAsync()
    {
        if (SelectedTaskSummaryItem is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedTaskSummaryItem.TaskItem.TaskId))
        {
            Message = "Cannot clear snooze until this task has a stable Task ID. Press Sync first.";
            return;
        }

        try
        {
            var task = SelectedTaskSummaryItem.TaskItem;
            task.SnoozeUntil = null;
            task.SnoozeNote = string.Empty;
            task.NotifyCalculatedFieldsChanged();
            await QueueAndTryMutationAsync(CreateMutation(task, TaskMutationTypes.ClearSnooze));
            RebuildTaskSummary();
            TasksView.Refresh();
            Message = $"Cleared snooze for '{task.Task}'.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to clear task snooze", ex);
            Message = $"Failed to clear task snooze: {ex.Message}. Log: {AppPaths.CurrentWarningErrorLogPath}";
        }
    }

    private Task NewTaskAsync()
    {
        ClearTaskSelection();
        _isNewTaskDraft = true;
        TaskEditDraft = new TaskEditDraft();
        OnPropertyChanged(nameof(TaskEditorTitle));
        IsTaskEditorOpen = true;
        return Task.CompletedTask;
    }

    private Task EditTaskAsync()
    {
        if (SelectedTask is null)
        {
            return Task.CompletedTask;
        }

        _isNewTaskDraft = false;
        TaskEditDraft = new TaskEditDraft
        {
            TaskId = SelectedTask.TaskId,
            Category = SelectedTask.Category,
            Type = SelectedTask.Type,
            Task = SelectedTask.Task,
            ExpiredValue = SelectedTask.ExpiredValue,
            ExpiredUnit = SelectedTask.ExpiredUnit,
            WarningValue = SelectedTask.WarningValue,
            WarningUnit = SelectedTask.WarningUnit,
            Alert = SelectedTask.Alert,
            History = SelectedTask.History
        };
        OnPropertyChanged(nameof(TaskEditorTitle));
        IsTaskSidebarOpen = false;
        IsTaskEditorOpen = true;
        return Task.CompletedTask;
    }

    private async Task SaveTaskAsync()
    {
        var draft = TaskEditDraft;
        if (string.IsNullOrWhiteSpace(draft.Category)
            || string.IsNullOrWhiteSpace(draft.Type)
            || string.IsNullOrWhiteSpace(draft.Task))
        {
            Message = "Category, type, and task are required.";
            return;
        }

        if (draft.ExpiredValue <= 0 || draft.WarningValue < 0)
        {
            Message = "Expired value must be positive and warning value cannot be negative.";
            return;
        }

        if (!TaskCycleUnits.Contains(draft.ExpiredUnit) || !TaskCycleUnits.Contains(draft.WarningUnit))
        {
            Message = "Task cycle units must be Day, Month, or Year.";
            return;
        }

        SheetTask task;
        string operationType;
        if (_isNewTaskDraft)
        {
            task = new SheetTask
            {
                TaskId = Guid.NewGuid().ToString("D"),
                Revision = 0
            };
            _tasks.Add(task);
            operationType = TaskMutationTypes.Create;
        }
        else
        {
            task = _tasks.FirstOrDefault(item => item.TaskId == draft.TaskId) ?? SelectedTask!;
            if (task is null || string.IsNullOrWhiteSpace(task.TaskId))
            {
                Message = "This task has no stable ID. Press Sync before editing it.";
                return;
            }
            operationType = TaskMutationTypes.Update;
        }

        task.Category = draft.Category.Trim();
        task.Type = draft.Type.Trim();
        task.Task = draft.Task.Trim();
        task.ExpiredValue = draft.ExpiredValue;
        task.ExpiredUnit = draft.ExpiredUnit;
        task.WarningValue = draft.WarningValue;
        task.WarningUnit = draft.WarningUnit;
        task.Alert = draft.Alert;
        task.History = draft.History;
        task.NotifyCalculatedFieldsChanged();

        await QueueAndTryMutationAsync(CreateMutation(task, operationType));
        IsTaskEditorOpen = false;
        _isNewTaskDraft = false;
        SelectedTask = task;
        RebuildFilterLists();
        RebuildTaskSummary();
        TasksView.Refresh();
        Message = $"Saved '{task.Task}'. {TaskSyncDisplay}.";
    }

    private Task CancelTaskEditAsync()
    {
        IsTaskEditorOpen = false;
        _isNewTaskDraft = false;
        return Task.CompletedTask;
    }

    private async Task ArchiveTaskAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var task = SelectedTask;
        if (MessageBox.Show(
                $"Archive '{task.Task}'? It will remain in Google Sheet history.",
                "Archive Task",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        task.Archived = true;
        await QueueAndTryMutationAsync(CreateMutation(task, TaskMutationTypes.Archive));
        ClearTaskSelection();
        TasksView.Refresh();
        RebuildTaskSummary();
        Message = $"Archived '{task.Task}'. {TaskSyncDisplay}.";
    }

    private Task OpenTaskConflictsAsync()
    {
        RebuildTaskConflicts();
        IsTaskConflictOpen = true;
        return Task.CompletedTask;
    }

    private Task CloseTaskConflictsAsync()
    {
        IsTaskConflictOpen = false;
        SelectedTaskConflict = null;
        return Task.CompletedTask;
    }

    private async Task KeepPcTaskConflictAsync()
    {
        if (SelectedTaskConflict?.ServerTask is null)
        {
            return;
        }

        var conflict = SelectedTaskConflict;
        conflict.ExpectedRevision = conflict.ServerTask.Revision;
        conflict.State = TaskMutationStates.Pending;
        conflict.ServerTask = null;
        await _fileStore.SaveTaskSyncQueueAsync(_taskSyncQueue);
        await ProcessPendingMutationsAsync(conflict.TaskId);
        RebuildTaskConflicts();
        Message = $"Conflict resolution attempted. {TaskSyncDisplay}.";
    }

    private async Task UseSheetTaskConflictAsync()
    {
        if (SelectedTaskConflict?.ServerTask is null)
        {
            return;
        }

        var conflict = SelectedTaskConflict;
        var local = _tasks.FirstOrDefault(task => task.TaskId == conflict.TaskId);
        if (local is not null)
        {
            CopyTask(conflict.ServerTask, local);
        }
        else if (!conflict.ServerTask.Archived)
        {
            _tasks.Add(CloneTask(conflict.ServerTask));
        }

        _taskSyncQueue.RemoveAll(item => item.TaskId == conflict.TaskId);
        await PersistTaskStateAsync();
        RebuildTaskConflicts();
        TasksView.Refresh();
        RebuildTaskSummary();
        Message = $"Accepted Google Sheet version. {TaskSyncDisplay}.";
    }

    private async Task CheckinAsync()
    {
        _checkinSettings.LastCheckinAt = DateTime.Now;
        _checkinSettings.LastAlertDate = DateTime.Today;
        await _fileStore.SaveCheckinSettingsAsync(_checkinSettings);
        RefreshCheckinDisplayIfNeeded(force: true);
        Message = $"Checked in at {LastCheckinDisplay}.";
    }

    private Task OpenCheckinSettingsAsync()
    {
        ResetCheckinSettingsDraft();
        IsCheckinSettingsOpen = true;
        return Task.CompletedTask;
    }

    private async Task SaveCheckinSettingsAsync()
    {
        var invalidDay = CheckinSettingsDraft.FirstOrDefault(day => !TryParseCheckinTime(day.TimeText, out _));
        if (invalidDay is not null)
        {
            Message = $"{invalidDay.DayName} check-in time must use HHmm, for example 1200.";
            return;
        }

        _checkinSettings.Days = new ObservableCollection<CheckinDaySetting>(
            CheckinSettingsDraft.Select(CloneCheckinDaySetting));
        await _fileStore.SaveCheckinSettingsAsync(_checkinSettings);
        IsCheckinSettingsOpen = false;
        Message = "Saved check-in notification settings.";
        await CheckAndNotifyCheckinAsync();
    }

    private Task CancelCheckinSettingsAsync()
    {
        ResetCheckinSettingsDraft();
        IsCheckinSettingsOpen = false;
        return Task.CompletedTask;
    }

    private async void CheckinTimer_Tick(object? sender, EventArgs e)
    {
        RefreshCheckinDisplayIfNeeded();
        RefreshTaskCalculatedFieldsIfDateChanged();
        await CheckAndNotifyCheckinAsync();
    }

    private async Task CheckAndNotifyCheckinAsync()
    {
        var now = DateTime.Now;
        var todaySetting = _checkinSettings.Days.FirstOrDefault(day => day.DayOfWeek == now.DayOfWeek);
        if (todaySetting is null
            || !todaySetting.IsEnabled
            || !TryParseCheckinTime(todaySetting.TimeText, out var checkinTime)
            || now.TimeOfDay < checkinTime
            || _checkinSettings.LastCheckinAt?.Date == now.Date
            || _checkinSettings.LastAlertDate?.Date == now.Date)
        {
            return;
        }

        _checkinSettings.LastAlertDate = now.Date;
        await _fileStore.SaveCheckinSettingsAsync(_checkinSettings);
        MessageBox.Show(
            $"You have not checked in today. Scheduled check-in time was {todaySetting.TimeText}.",
            "Check-in Reminder",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        Message = "Check-in reminder shown.";
    }

    private void ResetCheckinSettingsDraft()
    {
        foreach (var day in CheckinSettingsDraft)
        {
            day.PropertyChanged -= CheckinDaySetting_PropertyChanged;
        }

        CheckinSettingsDraft.Clear();
        foreach (var day in _checkinSettings.Days.Select(CloneCheckinDaySetting))
        {
            day.PropertyChanged += CheckinDaySetting_PropertyChanged;
            CheckinSettingsDraft.Add(day);
        }

        OnPropertyChanged(nameof(AllCheckinDaysEnabled));
        RefreshCheckinDisplayIfNeeded(force: true);
    }

    private void CheckinDaySetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isUpdatingCheckinAllDays && e.PropertyName == nameof(CheckinDaySetting.IsEnabled))
        {
            OnPropertyChanged(nameof(AllCheckinDaysEnabled));
        }
    }

    private void RefreshCheckinDisplayIfNeeded(bool force = false)
    {
        var now = DateTime.Now;
        if (!force
            && _lastCheckinDisplayRefreshDate == now.Date
            && !(now.TimeOfDay.Hours == 0 && now.TimeOfDay.Minutes == 15))
        {
            return;
        }

        _lastCheckinDisplayRefreshDate = now.Date;
        OnPropertyChanged(nameof(LastCheckinDisplay));
        OnPropertyChanged(nameof(IsLastCheckinStale));
        OnPropertyChanged(nameof(CheckinCheckboxValue));
    }

    private void RefreshTaskCalculatedFieldsIfDateChanged()
    {
        var today = DateTime.Today;
        if (_lastTaskCalculatedFieldsRefreshDate == today)
        {
            return;
        }

        ApplyTaskFilters();
    }

    private static CheckinDaySetting CloneCheckinDaySetting(CheckinDaySetting source)
    {
        return new CheckinDaySetting
        {
            DayOfWeek = source.DayOfWeek,
            DayName = source.DayName,
            IsEnabled = source.IsEnabled,
            TimeText = source.TimeText
        };
    }

    private static bool TryParseCheckinTime(string? value, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (value is null || value.Length != 4 || !value.All(char.IsDigit))
        {
            return false;
        }

        var hour = int.Parse(value[..2]);
        var minute = int.Parse(value[2..]);
        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return false;
        }

        time = new TimeSpan(hour, minute, 0);
        return true;
    }

    private void ApplyTaskFilters()
    {
        foreach (var task in _tasks)
        {
            task.NotifyCalculatedFieldsChanged();
        }

        RebuildTaskSummary();
        ApplyDefaultSort();
        TasksView.Refresh();
        _lastTaskCalculatedFieldsRefreshDate = DateTime.Today;
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
        ApplyTaskFilters();
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
            task.NotifyCalculatedFieldsChanged();
            _tasks.Add(task);
        }

        RefreshTaskSyncState();
        RebuildFilterLists();
        RebuildTaskSummary();
        ApplyDefaultSort();
        TasksView.Refresh();
    }

    private TaskMutation CreateMutation(SheetTask task, string operationType)
    {
        return new TaskMutation
        {
            TaskId = task.TaskId,
            OperationType = operationType,
            ExpectedRevision = task.Revision,
            Payload = TaskMutationPayload.FromTask(task)
        };
    }

    private async Task QueueAndTryMutationAsync(TaskMutation mutation)
    {
        _taskSyncQueue.Add(mutation);
        await PersistTaskStateAsync();
        StartTaskMutationUpload(mutation.TaskId);
    }

    private void StartTaskMutationUpload(string taskId)
    {
        _ = UploadPendingTaskMutationsAsync(taskId);
    }

    private async Task UploadPendingTaskMutationsAsync(string taskId)
    {
        try
        {
            await ProcessPendingMutationsAsync(taskId);
            Message = $"Upload complete. {TaskSyncDisplay}.";
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Task mutation queued for later: {taskId}", ex);
            Message = $"Saved locally; upload is pending: {ex.Message}";
        }
    }

    private async Task ProcessPendingMutationsAsync(string? onlyTaskId = null)
    {
        await _taskSyncGate.WaitAsync();
        try
        {
            var taskIds = _taskSyncQueue
                .Where(item => item.State == TaskMutationStates.Pending
                    && (onlyTaskId is null || item.TaskId == onlyTaskId))
                .OrderBy(item => item.QueuedAt)
                .Select(item => item.TaskId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var taskId in taskIds)
            {
                while (true)
                {
                    var mutation = _taskSyncQueue
                        .Where(item => item.TaskId == taskId && item.State == TaskMutationStates.Pending)
                        .OrderBy(item => item.QueuedAt)
                        .FirstOrDefault();
                    if (mutation is null)
                    {
                        break;
                    }

                    var response = await _sheetClient.SendMutationAsync(_config, mutation, CancellationToken.None);
                    if (response.Success && response.Task is not null)
                    {
                        var local = _tasks.FirstOrDefault(task => task.TaskId == taskId);
                        if (local is not null)
                        {
                            CopyTask(response.Task, local);
                        }
                        _taskSyncQueue.Remove(mutation);
                        foreach (var next in _taskSyncQueue.Where(item => item.TaskId == taskId && item.State == TaskMutationStates.Pending))
                        {
                            next.ExpectedRevision = response.Task.Revision;
                        }
                        await PersistTaskStateAsync();
                        continue;
                    }

                    if (string.Equals(response.ErrorCode, "REVISION_CONFLICT", StringComparison.OrdinalIgnoreCase))
                    {
                        mutation.State = TaskMutationStates.Conflict;
                        mutation.ServerTask = response.ServerTask;
                        await PersistTaskStateAsync();
                        break;
                    }

                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Error)
                        ? $"Google Sheet rejected {mutation.OperationType}."
                        : response.Error);
                }
            }

            RefreshTaskSyncState();
        }
        finally
        {
            _taskSyncGate.Release();
        }
    }

    private void MergeServerTasks(IEnumerable<SheetTask> serverTasks)
    {
        var serverList = serverTasks.ToList();
        var missingId = serverList.FirstOrDefault(task => string.IsNullOrWhiteSpace(task.TaskId));
        if (missingId is not null)
        {
            throw new InvalidOperationException($"Google Sheet returned a task without Task ID: {DescribeTask(missingId)}. Run migrateLifeSyncTaskSchema first.");
        }

        var serverById = serverList
            .GroupBy(task => task.TaskId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Count() == 1
                    ? group.Single()
                    : throw new InvalidOperationException($"Duplicate Task ID returned by Google Sheet: {group.Key}"),
                StringComparer.OrdinalIgnoreCase);

        var pendingIds = _taskSyncQueue.Select(item => item.TaskId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var localPending = _tasks.Where(task => pendingIds.Contains(task.TaskId)).ToList();
        var merged = serverById.Values
            .Where(task => !task.Archived && !pendingIds.Contains(task.TaskId))
            .Select(CloneTask)
            .Concat(localPending)
            .ToList();

        ReplaceTasks(merged);
    }

    private async Task PersistTaskStateAsync()
    {
        RefreshTaskSyncState();
        await _fileStore.SaveTasksAsync(_tasks);
        await _fileStore.SaveTaskSyncQueueAsync(_taskSyncQueue);
    }

    private void RefreshTaskSyncState()
    {
        foreach (var task in _tasks)
        {
            task.SyncState = _taskSyncQueue.Any(item => item.TaskId == task.TaskId && item.State == TaskMutationStates.Conflict)
                ? TaskSyncStates.Conflict
                : _taskSyncQueue.Any(item => item.TaskId == task.TaskId)
                    ? TaskSyncStates.Pending
                    : TaskSyncStates.Synced;
        }

        OnPropertyChanged(nameof(TaskPendingCount));
        OnPropertyChanged(nameof(TaskConflictCount));
        OnPropertyChanged(nameof(TaskSyncDisplay));
        RebuildTaskConflicts();
        RefreshCommands();
    }

    private void RebuildTaskConflicts()
    {
        TaskConflicts = new ObservableCollection<TaskMutation>(
            _taskSyncQueue
                .Where(item => item.State == TaskMutationStates.Conflict)
                .OrderBy(item => item.QueuedAt));
        if (SelectedTaskConflict is not null && !TaskConflicts.Contains(SelectedTaskConflict))
        {
            SelectedTaskConflict = null;
        }
    }

    private static SheetTask CloneTask(SheetTask source)
    {
        var task = new SheetTask();
        CopyTask(source, task);
        return task;
    }

    private static void CopyTask(SheetTask source, SheetTask target)
    {
        target.TaskId = source.TaskId;
        target.Revision = source.Revision;
        target.UpdatedAt = source.UpdatedAt;
        target.Archived = source.Archived;
        target.Category = source.Category;
        target.Type = source.Type;
        target.Task = source.Task;
        target.ExpiredDate = source.ExpiredDate;
        target.WarningDate = source.WarningDate;
        target.PreviousDate1 = source.PreviousDate1;
        target.PreviousDate2 = source.PreviousDate2;
        target.Completed = source.Completed;
        target.Alert = source.Alert;
        target.History = source.History;
        target.ExpiredValue = source.ExpiredValue;
        target.ExpiredUnit = source.ExpiredUnit;
        target.WarningValue = source.WarningValue;
        target.WarningUnit = source.WarningUnit;
        target.Remark = source.Remark;
        target.LastExecutedDate = source.LastExecutedDate;
        target.RowNumber = source.RowNumber;
        target.SnoozeUntil = source.SnoozeUntil;
        target.SnoozeNote = source.SnoozeNote;
        target.LastGoogleTaskKey = source.LastGoogleTaskKey;
        target.LastGoogleTaskId = source.LastGoogleTaskId;
        target.LastGoogleTaskCreatedDate = source.LastGoogleTaskCreatedDate;
        target.LastLifeSyncOperationId = source.LastLifeSyncOperationId;
        target.NotifyCalculatedFieldsChanged();
    }

    private static DateTime AddTaskInterval(DateTime date, int value, string unit)
    {
        return unit switch
        {
            "Day" => date.AddDays(value),
            "Month" => date.AddMonths(value),
            "Year" => date.AddYears(value),
            _ => throw new InvalidOperationException($"Unsupported task cycle unit: {unit}")
        };
    }

    private void RebuildTaskSummary()
    {
        var today = DateTime.Today;
        var activeTasks = _tasks
            .Where(task => !task.Completed && !task.Archived && !IsTaskSnoozedForToday(task))
            .ToList();

        ExpiredTaskSummaryItems = new ObservableCollection<TaskSummaryItem>(
            activeTasks
                .Where(task => task.ExpiredDate?.Date <= today)
                .OrderBy(task => task.ExpiredDate)
                .ThenBy(task => task.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(task => task.Type, StringComparer.OrdinalIgnoreCase)
                .ThenBy(task => task.Task, StringComparer.OrdinalIgnoreCase)
                .Select(task => new TaskSummaryItem(task, "Expired / Overdue")));

        WarningTaskSummaryItems = new ObservableCollection<TaskSummaryItem>(
            activeTasks
                .Where(task => task.ExpiredDate?.Date > today && task.WarningDate?.Date <= today)
                .OrderBy(task => task.WarningDate)
                .ThenBy(task => task.ExpiredDate)
                .ThenBy(task => task.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(task => task.Type, StringComparer.OrdinalIgnoreCase)
                .ThenBy(task => task.Task, StringComparer.OrdinalIgnoreCase)
                .Select(task => new TaskSummaryItem(task, "Warning")));

        if (SelectedTaskSummaryItem is not null
            && !ExpiredTaskSummaryItems.Concat(WarningTaskSummaryItems)
                .Any(item => ReferenceEquals(item.TaskItem, SelectedTaskSummaryItem.TaskItem)))
        {
            SelectedTaskSummaryItem = null;
            SelectedExpiredTaskSummaryItem = null;
            SelectedWarningTaskSummaryItem = null;
        }

        OnPropertyChanged(nameof(HasExpiredTaskSummaryItems));
        OnPropertyChanged(nameof(HasWarningTaskSummaryItems));
        OnPropertyChanged(nameof(TaskSummaryDisplay));
        RefreshCommands();
    }

    private static bool IsTaskSnoozedForToday(SheetTask task)
    {
        return task.SnoozeUntil?.Date >= DateTime.Today;
    }

    private bool IsTaskInConflict(SheetTask task)
    {
        return _taskSyncQueue.Any(item =>
            item.State == TaskMutationStates.Conflict
            && string.Equals(item.TaskId, task.TaskId, StringComparison.OrdinalIgnoreCase));
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
        Categories = BuildFilterList(_tasks.Select(task => task.Category));
        Types = BuildFilterList(_tasks.Select(task => task.Type));
        RestoreDynamicFilterSelections();
    }

    private void RestoreDynamicFilterSelections()
    {
        _categoryFilter = Categories.Contains(_categoryFilter) ? _categoryFilter : AllFilter;
        _typeFilter = Types.Contains(_typeFilter) ? _typeFilter : AllFilter;
        OnPropertyChanged(nameof(CategoryFilter));
        OnPropertyChanged(nameof(TypeFilter));
    }

    private static ObservableCollection<string> BuildFilterList(IEnumerable<string> values)
    {
        var selectedValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filterValues = new ObservableCollection<string> { AllFilter };
        foreach (var value in selectedValues)
        {
            filterValues.Add(value);
        }

        return filterValues;
    }

    private bool FilterTask(object item)
    {
        if (item is not SheetTask task)
        {
            return false;
        }

        return !task.Archived
            && Matches(_categoryFilter, task.Category)
            && Matches(_typeFilter, task.Type)
            && MatchesStatus(_statusFilter, task)
            && MatchesDayLeft(task.DayLeft);
    }

    private static bool Matches(string filter, string value)
    {
        return filter == AllFilter || string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesStatus(string filter, SheetTask task)
    {
        return filter switch
        {
            AllFilter => true,
            "Pending" => !string.Equals(task.SyncState, TaskSyncStates.Synced, StringComparison.OrdinalIgnoreCase),
            "Warning + Expired" => string.Equals(task.Status, "Warning", StringComparison.OrdinalIgnoreCase)
                || string.Equals(task.Status, "Expired", StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(filter, task.Status, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? AllFilter : value;
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToString("dd MMM yyyy");
    }

    private static string DescribeTask(SheetTask task)
    {
        return string.Join(" -> ", new[] { task.Category, task.Type, task.Task }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private bool MatchesDayLeft(int? dayLeft)
    {
        return _dayLeftFilter switch
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
        return SelectedTask is not null
            && !SelectedTask.Archived
            && !IsTaskInConflict(SelectedTask)
            && !string.IsNullOrWhiteSpace(SelectedTask.TaskId);
    }

    private bool CanEditSelectedTask()
    {
        return CanMutateSelectedTask();
    }

    private bool CanOpenSelectedTaskSidebar()
    {
        return SelectedTask is not null && !SelectedTask.Archived;
    }

    private bool HasTaskConflicts()
    {
        return TaskConflictCount > 0;
    }

    private bool HasSelectedTaskConflict()
    {
        return SelectedTaskConflict is not null;
    }

    private bool HasSelectedTaskSummaryItem()
    {
        return SelectedTaskSummaryItem is not null;
    }

    private bool CanSnoozeSelectedTaskSummaryItem()
    {
        return SelectedTaskSummaryItem?.TaskItem.Completed == false
            && !SelectedTaskSummaryItem.TaskItem.Archived
            && !IsTaskInConflict(SelectedTaskSummaryItem.TaskItem)
            && !string.IsNullOrWhiteSpace(SelectedTaskSummaryItem.TaskItem.TaskId);
    }

    private bool CanSnoozeSelectedTaskSummaryItemToCustomDate()
    {
        return CanSnoozeSelectedTaskSummaryItem() && TaskSummaryCustomSnoozeUntil is not null;
    }

    private bool CanClearSelectedTaskSummaryItemSnooze()
    {
        return SelectedTaskSummaryItem?.TaskItem.SnoozeUntil is not null
            && !IsTaskInConflict(SelectedTaskSummaryItem.TaskItem)
            && !string.IsNullOrWhiteSpace(SelectedTaskSummaryItem.TaskItem.TaskId);
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
        OpenSelectedTaskSidebarCommand.RaiseCanExecuteChanged();
        EditTaskCommand.RaiseCanExecuteChanged();
        ArchiveTaskCommand.RaiseCanExecuteChanged();
        OpenTaskConflictsCommand.RaiseCanExecuteChanged();
        KeepPcTaskConflictCommand.RaiseCanExecuteChanged();
        UseSheetTaskConflictCommand.RaiseCanExecuteChanged();
        OpenSelectedTaskSummaryItemCommand.RaiseCanExecuteChanged();
        SnoozeTaskSummary1DayCommand.RaiseCanExecuteChanged();
        SnoozeTaskSummary3DaysCommand.RaiseCanExecuteChanged();
        SnoozeTaskSummary7DaysCommand.RaiseCanExecuteChanged();
        SnoozeTaskSummary14DaysCommand.RaiseCanExecuteChanged();
        SnoozeTaskSummary30DaysCommand.RaiseCanExecuteChanged();
        SnoozeTaskSummaryCustomCommand.RaiseCanExecuteChanged();
        ClearTaskSummarySnoozeCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

public sealed class TaskSummaryItem
{
    public TaskSummaryItem(SheetTask taskItem, string group)
    {
        TaskItem = taskItem;
        Group = group;
    }

    public SheetTask TaskItem { get; }

    public string Group { get; }

    public string Category => TaskItem.Category;

    public string Type => TaskItem.Type;

    public string Task => TaskItem.Task;

    public DateTime? ExpiredDate => TaskItem.ExpiredDate;

    public DateTime? WarningDate => TaskItem.WarningDate;

    public string CategoryBadge => string.IsNullOrWhiteSpace(Category) ? "No Category" : Category;

    public string TypeBadge => string.IsNullOrWhiteSpace(Type) ? "No Type" : Type;

    public string SeverityBrush => Group == "Expired / Overdue" ? "#B42318" : "#B7791F";

    public string SeverityBackground => Group == "Expired / Overdue" ? "#FFF1F0" : "#FFF8DB";

    public string SeverityBorder => Group == "Expired / Overdue" ? "#F3B4AE" : "#EBCB73";

    public string DateLine
    {
        get
        {
            var parts = new List<string>();
            if (ExpiredDate is not null)
            {
                parts.Add($"Expired {ExpiredDate.Value:dd MMM yyyy}");
            }

            if (WarningDate is not null)
            {
                parts.Add($"Warning {WarningDate.Value:dd MMM yyyy}");
            }

            if (TaskItem.PreviousDate1 is not null)
            {
                parts.Add($"Last {TaskItem.PreviousDate1.Value:dd MMM yyyy}");
            }

            return string.Join("  |  ", parts);
        }
    }

    public string DayState => TaskItem.DayLeft switch
    {
        null => string.Empty,
        < 0 => $"{Math.Abs(TaskItem.DayLeft.Value)} overdue",
        0 => "Due today",
        1 => "1 day left",
        var days => $"{days} days left"
    };

    public string DayStateBrush => TaskItem.DayLeft switch
    {
        null => "#65758B",
        < 0 => "#B42318",
        0 => "#B42318",
        <= 7 => "#B7791F",
        _ => "#2F6F9F"
    };

    public string RemarkPreview => TaskItem.RemarkPreview;

    public string Snooze => TaskItem.SnoozeDisplay;

    public string GoogleTask => TaskItem.GoogleTaskDisplay;

    public string Sync => TaskItem.SyncState;

    public Visibility RemarkVisibility => string.IsNullOrWhiteSpace(RemarkPreview)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility SnoozeVisibility => string.IsNullOrWhiteSpace(Snooze)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility GoogleTaskVisibility => string.IsNullOrWhiteSpace(GoogleTask)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility SyncVisibility => string.Equals(Sync, TaskSyncStates.Synced, StringComparison.OrdinalIgnoreCase)
        ? Visibility.Collapsed
        : Visibility.Visible;
}
