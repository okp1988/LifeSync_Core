using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
    private const string NormalSortMode = "Normal";
    private static readonly TimeSpan CompletionUploadDelay = TimeSpan.FromHours(1);
    private readonly JsonFileStore _fileStore = new();
    private readonly GoogleSheetClient _sheetClient = new();
    private readonly RangeObservableCollection<SheetTask> _tasks = [];
    private readonly List<TaskMutation> _taskSyncQueue = [];
    private readonly List<CompletionHistoryRecord> _completionHistoryRecords = [];
    private readonly ObservableCollection<CompletionHistoryRecord> _completionHistoryItems = [];
    private readonly ObservableCollection<SheetTask> _expiredPriorityItems = [];
    private readonly ObservableCollection<SheetTask> _warningPriorityItems = [];
    private readonly ObservableCollection<SheetTask> _pausedTaskItems = [];
    private readonly ObservableCollection<MinorTaskCompletionDraft> _minorCompletionDrafts = [];
    private readonly ObservableCollection<TaskFilterDefinition> _taskFilters = [];
    private readonly ObservableCollection<TaskFilterDefinition> _filterManagerDrafts = [];
    private readonly ObservableCollection<FilterTaskSelectionItem> _filterTaskSelectionItems = [];
    private readonly SemaphoreSlim _taskSyncGate = new(1, 1);
    private readonly DispatcherTimer _checkinTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer _completionUploadTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private AppConfig _config = new();
    private AppConfig _configDraft = new();
    private CheckinSettings _checkinSettings = new();
    private SheetTask? _selectedTask;
    private SheetTask? _selectedPriorityTask;
    private SheetTask? _selectedExpiredPriorityTask;
    private SheetTask? _selectedWarningPriorityTask;
    private bool _isPriorityDetailOpen;
    private string _categoryFilter = AllFilter;
    private string _typeFilter = AllFilter;
    private string _statusFilter = AllFilter;
    private string _taskSearchText = string.Empty;
    private MainViewKind _currentMainView = MainViewKind.Tasks;
    private bool _isLoadingTasks;
    private bool _isMarkingComplete;
    private bool _isTaskSidebarOpen;
    private bool _isTaskEditorOpen;
    private bool _isNewTaskDraft;
    private bool _isTaskConflictOpen;
    private TaskEditDraft _taskEditDraft = new();
    private ObservableCollection<TaskMutation> _taskConflicts = [];
    private TaskMutation? _selectedTaskConflict;
    private string _message = "Ready";
    private bool _isSettingsOpen;
    private bool _isUpdatingCheckinAllDays;
    private DateTime _lastCheckinDisplayRefreshDate = DateTime.MinValue;
    private DateTime _lastTaskCalculatedFieldsRefreshDate = DateTime.Today;
    private string _selectedRemarkDraft = string.Empty;
    private DateTime? _completionDate = DateTime.Today;
    private ObservableCollection<string> _categories = [AllFilter];
    private ObservableCollection<string> _types = [AllFilter];
    private ObservableCollection<TaskSummaryItem> _expiredTaskSummaryItems = [];
    private ObservableCollection<TaskSummaryItem> _warningTaskSummaryItems = [];
    private ObservableCollection<TaskSummaryItem> _snoozedTaskSummaryItems = [];
    private TaskSummaryItem? _selectedTaskSummaryItem;
    private TaskSummaryItem? _selectedExpiredTaskSummaryItem;
    private TaskSummaryItem? _selectedWarningTaskSummaryItem;
    private TaskSummaryItem? _selectedSnoozedTaskSummaryItem;
    private DateTime? _taskSummaryCustomSnoozeUntil = DateTime.Today.AddDays(7);
    private string _taskSummarySnoozeNote = string.Empty;
    private bool _areSecondaryViewsReady;
    private DateTime _historyMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private bool _isPausedManagerOpen;
    private bool _isPauseEditorOpen;
    private SheetTask? _pauseTargetTask;
    private DateTime? _pauseResumeDate;
    private string _mainTaskSearchText = string.Empty;
    private TaskFilterState _taskFilterState = new();
    private TaskFilterDefinition? _selectedSavedFilter;
    private TaskFilterDefinition? _managedFilter;
    private bool _isFilterManagerOpen;
    private string _filterTaskSearchText = string.Empty;
    private bool _updatingFilterSelections;

    public MainViewModel()
    {
        TasksView = CollectionViewSource.GetDefaultView(_tasks);
        TasksView.Filter = FilterTask;
        ApplyTaskSort();

        RequestTasksCommand = new RelayCommand(RequestTasksAsync, CanUseNetwork);
        MarkCompleteCommand = new RelayCommand(MarkCompleteAsync, CanMutateSelectedTask);
        ClearFiltersCommand = new RelayCommand(ClearFiltersAsync);
        ToggleAllTaskDetailsCommand = new RelayCommand(ToggleAllTaskDetailsAsync, CanToggleAllTaskDetails);
        CloseTaskSidebarCommand = new RelayCommand(CloseTaskSidebarAsync);
        OpenSelectedTaskSidebarCommand = new RelayCommand(OpenSelectedTaskSidebarAsync, CanOpenSelectedTaskSidebar);
        NewTaskCommand = new RelayCommand(NewTaskAsync);
        EditTaskCommand = new RelayCommand(EditTaskAsync, CanEditSelectedTask);
        SaveTaskCommand = new RelayCommand(SaveTaskAsync);
        CancelTaskEditCommand = new RelayCommand(CancelTaskEditAsync);
        ArchiveTaskCommand = new RelayCommand(ArchiveTaskAsync, CanEditSelectedTask);
        PauseSelectedTaskCommand = new RelayCommand(OpenPauseSelectedTaskAsync, CanPauseSelectedTask);
        OpenPausedManagerCommand = new RelayCommand(OpenPausedManagerAsync);
        ClosePausedManagerCommand = new RelayCommand(ClosePausedManagerAsync);
        SavePauseCommand = new RelayCommand(SavePauseAsync, HasPauseTargetTask);
        CancelPauseCommand = new RelayCommand(CancelPauseAsync);
        ResumeTaskCommand = new ParameterRelayCommand<SheetTask>(ResumeTaskAsync, CanManageTask);
        OpenFilterManagerCommand = new RelayCommand(OpenFilterManagerAsync);
        CloseFilterManagerCommand = new RelayCommand(CloseFilterManagerAsync);
        NewFilterCommand = new RelayCommand(NewFilterAsync);
        SaveFilterCommand = new RelayCommand(SaveManagedFilterAsync);
        DeleteFilterCommand = new RelayCommand(DeleteManagedFilterAsync);
        SetFavouriteFilterCommand = new RelayCommand(SetFavouriteManagedFilterAsync);
        EditPausedTaskCommand = new ParameterRelayCommand<SheetTask>(EditPausedTaskAsync, CanManageTask);
        EditLinkedTaskCommand = new ParameterRelayCommand<SheetTask>(EditLinkedTaskAsync, CanManageTask);
        AddMinorTaskCommand = new RelayCommand(AddMinorTaskAsync);
        RemoveMinorTaskCommand = new ParameterRelayCommand<MinorTask>(RemoveMinorTaskAsync);
        ClearTaskLinkCommand = new RelayCommand(ClearTaskLinkAsync);
        ClearMinorTaskDateCommand = new ParameterRelayCommand<MinorTask>(ClearMinorTaskDateAsync);
        ClearPauseResumeDateCommand = new RelayCommand(ClearPauseResumeDateAsync);
        OpenTaskConflictsCommand = new RelayCommand(OpenTaskConflictsAsync, HasTaskConflicts);
        CloseTaskConflictsCommand = new RelayCommand(CloseTaskConflictsAsync);
        KeepPcTaskConflictCommand = new RelayCommand(KeepPcTaskConflictAsync, HasSelectedTaskConflict);
        UseSheetTaskConflictCommand = new RelayCommand(UseSheetTaskConflictAsync, HasSelectedTaskConflict);
        OpenSelectedTaskSummaryItemCommand = new RelayCommand(OpenSelectedTaskSummaryItemAsync, HasSelectedTaskSummaryItem);
        SnoozeTaskSummary1DayCommand = new RelayCommand(() => SnoozeSelectedTaskSummaryItemAsync(1), CanSnoozeSelectedTaskSummaryItem);
        SnoozeTaskSummary3DaysCommand = new RelayCommand(() => SnoozeSelectedTaskSummaryItemAsync(3), CanSnoozeSelectedTaskSummaryItem);
        SnoozeTaskSummary7DaysCommand = new RelayCommand(() => SnoozeSelectedTaskSummaryItemAsync(7), CanSnoozeSelectedTaskSummaryItem);
        SnoozeTaskSummary14DaysCommand = new RelayCommand(() => SnoozeSelectedTaskSummaryItemAsync(14), CanSnoozeSelectedTaskSummaryItem);
        SnoozeTaskSummary30DaysCommand = new RelayCommand(() => SnoozeSelectedTaskSummaryItemAsync(30), CanSnoozeSelectedTaskSummaryItem);
        SnoozeTaskSummaryCustomCommand = new RelayCommand(SnoozeSelectedTaskSummaryItemToCustomDateAsync, CanSnoozeSelectedTaskSummaryItemToCustomDate);
        ClearTaskSummarySnoozeCommand = new RelayCommand(ClearSelectedTaskSummaryItemSnoozeAsync, CanClearSelectedTaskSummaryItemSnooze);
        ShowTasksViewCommand = new RelayCommand(ShowTasksViewAsync);
        ShowPriorityViewCommand = new RelayCommand(ShowPriorityViewAsync, CanOpenSecondaryView);
        ShowDailySummaryViewCommand = new RelayCommand(ShowDailySummaryViewAsync, CanOpenSecondaryView);
        ShowHistoryViewCommand = new RelayCommand(ShowHistoryViewAsync, CanOpenSecondaryView);
        PreviousHistoryMonthCommand = new RelayCommand(ShowPreviousHistoryMonthAsync);
        NextHistoryMonthCommand = new RelayCommand(ShowNextHistoryMonthAsync, CanShowNextHistoryMonth);
        OpenPriorityDetailCommand = new RelayCommand(OpenPriorityDetailAsync, HasSelectedPriorityTask);
        ClosePriorityDetailCommand = new RelayCommand(ClosePriorityDetailAsync);
        UndoCompletionCommand = new ParameterRelayCommand<CompletionHistoryRecord>(UndoCompletionAsync, record => record.CanUndo);
        CheckinCommand = new RelayCommand(CheckinAsync);
        OpenSettingsCommand = new RelayCommand(OpenSettingsAsync);
        SaveSettingsCommand = new RelayCommand(SaveSettingsAsync);
        CancelSettingsCommand = new RelayCommand(CancelSettingsAsync);
        CheckinSettingsDraft = new ObservableCollection<CheckinDaySetting>();
        _checkinTimer.Tick += CheckinTimer_Tick;
        _completionUploadTimer.Tick += CompletionUploadTimer_Tick;
    }

    public ICollectionView TasksView { get; }
    public ObservableCollection<CompletionHistoryRecord> CompletionHistoryItems => _completionHistoryItems;
    public ObservableCollection<SheetTask> ExpiredPriorityItems => _expiredPriorityItems;
    public ObservableCollection<SheetTask> WarningPriorityItems => _warningPriorityItems;
    public ObservableCollection<SheetTask> PausedTaskItems => _pausedTaskItems;
    public ObservableCollection<MinorTaskCompletionDraft> MinorCompletionDrafts => _minorCompletionDrafts;
    public ObservableCollection<TaskFilterDefinition> TaskFilters => _taskFilters;
    public ObservableCollection<TaskFilterDefinition> FilterManagerDrafts => _filterManagerDrafts;
    public ObservableCollection<FilterTaskSelectionItem> FilterTaskSelectionItems => _filterTaskSelectionItems;
    public ObservableCollection<string> Categories
    {
        get => _categories;
        private set
        {
            _categories = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TaskCategoryOptions));
        }
    }

    public ObservableCollection<string> Types
    {
        get => _types;
        private set
        {
            _types = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TaskTypeOptions));
        }
    }

    public IEnumerable<string> TaskCategoryOptions => Categories.Where(value => value != AllFilter);

    public IEnumerable<string> TaskTypeOptions => Types.Where(value => value != AllFilter);

    public string[] Statuses { get; } = [AllFilter, "Normal", "Warning", "Expired", "Pending", "Warning + Expired"];
    public int[] TaskLevels { get; } = [1, 2, 3, 4, 5];
    public string[] TaskCycleUnits { get; } = ["Day", "Month", "Year"];

    public RelayCommand RequestTasksCommand { get; }
    public RelayCommand MarkCompleteCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand ToggleAllTaskDetailsCommand { get; }
    public RelayCommand CloseTaskSidebarCommand { get; }
    public RelayCommand OpenSelectedTaskSidebarCommand { get; }
    public RelayCommand NewTaskCommand { get; }
    public RelayCommand EditTaskCommand { get; }
    public RelayCommand SaveTaskCommand { get; }
    public RelayCommand CancelTaskEditCommand { get; }
    public RelayCommand ArchiveTaskCommand { get; }
    public RelayCommand PauseSelectedTaskCommand { get; }
    public RelayCommand OpenPausedManagerCommand { get; }
    public RelayCommand ClosePausedManagerCommand { get; }
    public RelayCommand SavePauseCommand { get; }
    public RelayCommand CancelPauseCommand { get; }
    public ParameterRelayCommand<SheetTask> ResumeTaskCommand { get; }
    public ParameterRelayCommand<SheetTask> EditPausedTaskCommand { get; }
    public ParameterRelayCommand<SheetTask> EditLinkedTaskCommand { get; }
    public RelayCommand OpenFilterManagerCommand { get; }
    public RelayCommand CloseFilterManagerCommand { get; }
    public RelayCommand NewFilterCommand { get; }
    public RelayCommand SaveFilterCommand { get; }
    public RelayCommand DeleteFilterCommand { get; }
    public RelayCommand SetFavouriteFilterCommand { get; }
    public RelayCommand AddMinorTaskCommand { get; }
    public ParameterRelayCommand<MinorTask> RemoveMinorTaskCommand { get; }
    public RelayCommand ClearTaskLinkCommand { get; }
    public ParameterRelayCommand<MinorTask> ClearMinorTaskDateCommand { get; }
    public RelayCommand ClearPauseResumeDateCommand { get; }
    public RelayCommand OpenTaskConflictsCommand { get; }
    public RelayCommand CloseTaskConflictsCommand { get; }
    public RelayCommand KeepPcTaskConflictCommand { get; }
    public RelayCommand UseSheetTaskConflictCommand { get; }
    public RelayCommand OpenSelectedTaskSummaryItemCommand { get; }
    public RelayCommand SnoozeTaskSummary1DayCommand { get; }
    public RelayCommand SnoozeTaskSummary3DaysCommand { get; }
    public RelayCommand SnoozeTaskSummary7DaysCommand { get; }
    public RelayCommand SnoozeTaskSummary14DaysCommand { get; }
    public RelayCommand SnoozeTaskSummary30DaysCommand { get; }
    public RelayCommand SnoozeTaskSummaryCustomCommand { get; }
    public RelayCommand ClearTaskSummarySnoozeCommand { get; }
    public RelayCommand ShowTasksViewCommand { get; }
    public RelayCommand ShowPriorityViewCommand { get; }
    public RelayCommand ShowDailySummaryViewCommand { get; }
    public RelayCommand ShowHistoryViewCommand { get; }
    public RelayCommand PreviousHistoryMonthCommand { get; }
    public RelayCommand NextHistoryMonthCommand { get; }
    public RelayCommand OpenPriorityDetailCommand { get; }
    public RelayCommand ClosePriorityDetailCommand { get; }
    public ParameterRelayCommand<CompletionHistoryRecord> UndoCompletionCommand { get; }
    public RelayCommand CheckinCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand CancelSettingsCommand { get; }

    public ObservableCollection<CheckinDaySetting> CheckinSettingsDraft { get; }

    public IEnumerable<SheetTask> LinkSourceOptions => _tasks
        .Where(task => !task.Archived
            && !string.Equals(task.TaskId, TaskEditDraft.TaskId, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(MainTaskSearchText)
                || task.FullPath.Contains(MainTaskSearchText.Trim(), StringComparison.OrdinalIgnoreCase)))
        .OrderBy(task => task.Category, StringComparer.OrdinalIgnoreCase)
        .ThenBy(task => task.Type, StringComparer.OrdinalIgnoreCase)
        .ThenBy(task => task.Task, StringComparer.OrdinalIgnoreCase);

    public string MainTaskSearchText
    {
        get => _mainTaskSearchText;
        set
        {
            if (_mainTaskSearchText == value) return;
            _mainTaskSearchText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LinkSourceOptions));
        }
    }

    public TaskFilterDefinition? SelectedSavedFilter
    {
        get => _selectedSavedFilter;
        set
        {
            if (ReferenceEquals(_selectedSavedFilter, value)) return;
            _selectedSavedFilter = value;
            OnPropertyChanged();
            RebuildFilterLists();
            TasksView.Refresh();
            RefreshTaskExpansionToggle();
        }
    }

    public TaskFilterDefinition? ManagedFilter
    {
        get => _managedFilter;
        set
        {
            if (ReferenceEquals(_managedFilter, value)) return;
            _managedFilter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEditManagedFilter));
            RebuildFilterTaskSelections();
        }
    }

    public bool IsFilterManagerOpen
    {
        get => _isFilterManagerOpen;
        private set { if (_isFilterManagerOpen == value) return; _isFilterManagerOpen = value; OnPropertyChanged(); }
    }

    public bool CanEditManagedFilter => ManagedFilter is { IsSystem: false };

    public string FilterTaskSearchText
    {
        get => _filterTaskSearchText;
        set { if (_filterTaskSearchText == value) return; _filterTaskSearchText = value; OnPropertyChanged(); RebuildFilterTaskSelections(); }
    }

    public int PausedTaskCount => _tasks.Count(task => !task.Archived && task.IsEffectivelyPaused);

    public string PausedButtonDisplay => $"Paused ({PausedTaskCount})";

    public string ExpandCollapseAllGlyph => AreAllVisibleTaskDetailsExpanded ? "\uE70E" : "\uE70D";

    public string ExpandCollapseAllToolTip => AreAllVisibleTaskDetailsExpanded
        ? "Collapse All Task Details"
        : "Expand All Task Details";

    public bool IsPausedManagerOpen
    {
        get => _isPausedManagerOpen;
        private set
        {
            if (_isPausedManagerOpen == value) return;
            _isPausedManagerOpen = value;
            OnPropertyChanged();
        }
    }

    public bool IsPauseEditorOpen
    {
        get => _isPauseEditorOpen;
        private set
        {
            if (_isPauseEditorOpen == value) return;
            _isPauseEditorOpen = value;
            OnPropertyChanged();
        }
    }

    public DateTime? PauseResumeDate
    {
        get => _pauseResumeDate;
        set
        {
            if (_pauseResumeDate == value) return;
            _pauseResumeDate = value;
            OnPropertyChanged();
        }
    }

    public string PauseTargetDisplay => _pauseTargetTask?.FullPath ?? string.Empty;

    public MainViewKind CurrentMainView
    {
        get => _currentMainView;
        private set
        {
            if (_currentMainView == value)
            {
                return;
            }

            _currentMainView = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTasksView));
            OnPropertyChanged(nameof(IsPriorityView));
            OnPropertyChanged(nameof(IsDailySummaryView));
            OnPropertyChanged(nameof(IsHistoryView));
        }
    }

    public bool IsTasksView => CurrentMainView == MainViewKind.Tasks;

    public bool IsPriorityView => CurrentMainView == MainViewKind.Priority;

    public bool IsDailySummaryView => CurrentMainView == MainViewKind.DailySummary;

    public bool IsHistoryView => CurrentMainView == MainViewKind.History;

    public bool AreSecondaryViewsReady
    {
        get => _areSecondaryViewsReady;
        private set
        {
            if (_areSecondaryViewsReady == value)
            {
                return;
            }

            _areSecondaryViewsReady = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PriorityViewDisplay));
            OnPropertyChanged(nameof(DailySummaryViewDisplay));
            OnPropertyChanged(nameof(HistoryViewDisplay));
            ShowPriorityViewCommand.RaiseCanExecuteChanged();
            ShowDailySummaryViewCommand.RaiseCanExecuteChanged();
            ShowHistoryViewCommand.RaiseCanExecuteChanged();
        }
    }

    public string PriorityViewDisplay => AreSecondaryViewsReady
        ? $"Priority ({ExpiredPriorityItems.Count + WarningPriorityItems.Count})"
        : "Priority (loading)";

    public string HistoryViewDisplay => AreSecondaryViewsReady
        ? $"History ({CompletionHistoryItems.Count})"
        : "History (loading)";

    public string HistoryMonthDisplay => _historyMonth.ToString("MMMM yyyy");

    public SheetTask? SelectedPriorityTask
    {
        get => _selectedPriorityTask;
        set
        {
            if (_selectedPriorityTask == value) return;
            _selectedPriorityTask = value;
            OnPropertyChanged();
            if (value is not null && IsPriorityDetailOpen)
            {
                SelectedTask = value;
            }
            OpenPriorityDetailCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsPriorityDetailOpen
    {
        get => _isPriorityDetailOpen;
        private set
        {
            if (_isPriorityDetailOpen == value) return;
            _isPriorityDetailOpen = value;
            OnPropertyChanged();
        }
    }

    public SheetTask? SelectedExpiredPriorityTask
    {
        get => _selectedExpiredPriorityTask;
        set
        {
            if (_selectedExpiredPriorityTask == value) return;
            _selectedExpiredPriorityTask = value;
            OnPropertyChanged();
            if (value is not null)
            {
                _selectedWarningPriorityTask = null;
                OnPropertyChanged(nameof(SelectedWarningPriorityTask));
                SelectedPriorityTask = value;
            }
        }
    }

    public SheetTask? SelectedWarningPriorityTask
    {
        get => _selectedWarningPriorityTask;
        set
        {
            if (_selectedWarningPriorityTask == value) return;
            _selectedWarningPriorityTask = value;
            OnPropertyChanged();
            if (value is not null)
            {
                _selectedExpiredPriorityTask = null;
                OnPropertyChanged(nameof(SelectedExpiredPriorityTask));
                SelectedPriorityTask = value;
            }
        }
    }

    public bool HasExpiredPriorityItems => ExpiredPriorityItems.Count > 0;

    public bool HasWarningPriorityItems => WarningPriorityItems.Count > 0;

    public bool HasCompletionHistoryItems => CompletionHistoryItems.Count > 0;

    public bool CheckinCheckboxValue => false;

    public string LastCheckinDisplay => _checkinSettings.LastCheckinAt is null
        ? "No check-in yet"
        : _checkinSettings.LastCheckinAt.Value.Date == DateTime.Today
            ? "TODAY"
        : _checkinSettings.LastCheckinAt.Value.ToString("dd MMM yyyy HH:mm");

    public bool IsLastCheckinStale => _checkinSettings.LastCheckinAt is not null
        && _checkinSettings.LastCheckinAt.Value.Date != DateTime.Today;

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        private set
        {
            if (_isSettingsOpen == value)
            {
                return;
            }

            _isSettingsOpen = value;
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
        get => _configDraft.GoogleAppsScriptUrl;
        set
        {
            if (_configDraft.GoogleAppsScriptUrl == value)
            {
                return;
            }

            _configDraft.GoogleAppsScriptUrl = value;
            OnPropertyChanged();
        }
    }

    public string ApiKey
    {
        get => _configDraft.ApiKey;
        set
        {
            if (_configDraft.ApiKey == value)
            {
                return;
            }

            _configDraft.ApiKey = value;
            OnPropertyChanged();
        }
    }

    public int LogRetentionDays
    {
        get => _configDraft.LogRetentionDays;
        set
        {
            var normalizedValue = Math.Max(1, value);
            if (_configDraft.LogRetentionDays == normalizedValue)
            {
                return;
            }

            _configDraft.LogRetentionDays = normalizedValue;
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
            PrepareMinorCompletionDrafts(value);
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

    public string TaskSearchText
    {
        get => _taskSearchText;
        set
        {
            value ??= string.Empty;
            if (_taskSearchText == value)
            {
                return;
            }

            _taskSearchText = value;
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
            OnPropertyChanged(nameof(LinkSourceOptions));
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
            foreach (var minorDraft in _minorCompletionDrafts)
            {
                minorDraft.CompletionDate = value;
            }
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
            OnPropertyChanged(nameof(DailySummaryViewDisplay));
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
            OnPropertyChanged(nameof(DailySummaryViewDisplay));
        }
    }

    public ObservableCollection<TaskSummaryItem> SnoozedTaskSummaryItems
    {
        get => _snoozedTaskSummaryItems;
        private set
        {
            _snoozedTaskSummaryItems = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSnoozedTaskSummaryItems));
            OnPropertyChanged(nameof(TaskSummaryDisplay));
            OnPropertyChanged(nameof(DailySummaryViewDisplay));
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
            TaskSummaryCustomSnoozeUntil = value?.TaskItem.SnoozeUntil?.Date >= DateTime.Today
                ? value.TaskItem.SnoozeUntil.Value.Date
                : DateTime.Today.AddDays(7);
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
                _selectedSnoozedTaskSummaryItem = null;
                OnPropertyChanged(nameof(SelectedSnoozedTaskSummaryItem));
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
                _selectedSnoozedTaskSummaryItem = null;
                OnPropertyChanged(nameof(SelectedSnoozedTaskSummaryItem));
                SelectedTaskSummaryItem = value;
            }
        }
    }

    public TaskSummaryItem? SelectedSnoozedTaskSummaryItem
    {
        get => _selectedSnoozedTaskSummaryItem;
        set
        {
            if (_selectedSnoozedTaskSummaryItem == value)
            {
                return;
            }

            _selectedSnoozedTaskSummaryItem = value;
            OnPropertyChanged();
            if (value is not null)
            {
                _selectedExpiredTaskSummaryItem = null;
                OnPropertyChanged(nameof(SelectedExpiredTaskSummaryItem));
                _selectedWarningTaskSummaryItem = null;
                OnPropertyChanged(nameof(SelectedWarningTaskSummaryItem));
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

    public bool HasSnoozedTaskSummaryItems => SnoozedTaskSummaryItems.Count > 0;

    public string TaskSummaryDisplay => $"{ExpiredTaskSummaryItems.Count} expired / {WarningTaskSummaryItems.Count} warning / {SnoozedTaskSummaryItems.Count} snoozed";

    public string DailySummaryViewDisplay => AreSecondaryViewsReady
        ? $"Daily Summary ({ExpiredTaskSummaryItems.Count + WarningTaskSummaryItems.Count + SnoozedTaskSummaryItems.Count})"
        : "Daily Summary (loading)";

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

            var taskSyncQueueLoad = _fileStore.LoadTaskSyncQueueAsync();
            var cachedTasksLoad = _fileStore.LoadTasksAsync();
            var completionHistoryLoad = _fileStore.LoadCompletionHistoryAsync();
            var checkinSettingsLoad = _fileStore.LoadCheckinSettingsAsync();
            var taskFiltersLoad = _fileStore.LoadTaskFiltersAsync();
            await Task.WhenAll(taskSyncQueueLoad, cachedTasksLoad, completionHistoryLoad, checkinSettingsLoad, taskFiltersLoad);

            _taskSyncQueue.Clear();
            _taskSyncQueue.AddRange(await taskSyncQueueLoad);
            _completionHistoryRecords.Clear();
            _completionHistoryRecords.AddRange(await completionHistoryLoad);
            var reconciledHistory = false;
            foreach (var record in _completionHistoryRecords.Where(item => item.State == CompletionHistoryStates.Pending))
            {
                var matchingMutation = _taskSyncQueue.FirstOrDefault(item => item.OperationId == record.OperationId);
                if (matchingMutation?.State == TaskMutationStates.Conflict)
                {
                    record.State = CompletionHistoryStates.Conflict;
                    reconciledHistory = true;
                }
                else if (matchingMutation is null)
                {
                    record.State = CompletionHistoryStates.Synced;
                    reconciledHistory = true;
                }
            }
            _checkinSettings = await checkinSettingsLoad;
            _taskFilterState = await taskFiltersLoad;
            LoadTaskFilters(_taskFilterState.Filters);
            ResetSettingsDraft();
            ResetFilters(resetSortMode: true);
            var cachedTasks = await cachedTasksLoad;
            if (_completionHistoryRecords.Count == 0)
            {
                SeedLegacyCompletionHistory(cachedTasks);
                await _fileStore.SaveCompletionHistoryAsync(_completionHistoryRecords);
            }
            else if (reconciledHistory)
            {
                await _fileStore.SaveCompletionHistoryAsync(_completionHistoryRecords);
            }
            ReplaceTasks(cachedTasks, rebuildSecondaryViews: false);
            _ = BuildSecondaryViewsAfterStartupAsync();
            AppLogger.Info($"Loaded {cachedTasks.Count} cached task(s) from {AppPaths.TaskCachePath}");
            AppLogger.Info($"Loaded check-in settings from {AppPaths.CheckinSettingsPath}");
            Message = cachedTasks.Count == 0
                ? "No cached tasks. Press Sync to retrieve from Google Sheet."
                : $"Loaded {cachedTasks.Count} cached task(s). {TaskSyncDisplay}.";
            _checkinTimer.Start();
            _completionUploadTimer.Start();
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
        ResetSettingsDraft();
        IsSettingsOpen = false;
        IsTaskEditorOpen = false;
        IsTaskConflictOpen = false;
        IsPausedManagerOpen = false;
        IsPauseEditorOpen = false;
        IsFilterManagerOpen = false;
        _pauseTargetTask = null;
        SavePauseCommand.RaiseCanExecuteChanged();
        PauseResumeDate = null;
        _minorCompletionDrafts.Clear();
        SelectedTaskSummaryItem = null;
        SelectedExpiredTaskSummaryItem = null;
        SelectedWarningTaskSummaryItem = null;
        SelectedSnoozedTaskSummaryItem = null;
        SelectedPriorityTask = null;
        SelectedExpiredPriorityTask = null;
        SelectedWarningPriorityTask = null;
        IsPriorityDetailOpen = false;
        ClearTaskSelection();
    }

    private async Task RequestTasksAsync()
    {
        IsLoadingTasks = true;
        Message = "Syncing queued changes and Google Sheet tasks...";

        try
        {
            await ProcessPendingMutationsAsync(includeDelayed: true);
            var snapshot = await _sheetClient.GetTaskSnapshotAsync(_config, CancellationToken.None);
            MergeServerTasks(snapshot.Tasks);
            MergeServerCompletionHistory(snapshot.HistoryRecords);
            ResetFilters(resetSortMode: false);
            await _fileStore.SaveTasksAsync(_tasks);
            await _fileStore.SaveTaskSyncQueueAsync(_taskSyncQueue);
            await _fileStore.SaveCompletionHistoryAsync(_completionHistoryRecords);
            var filterWarning = FindFilterMembershipWarning();
            Message = $"Sync complete. {_tasks.Count(task => !task.Archived)} task(s); {TaskSyncDisplay}."
                + (string.IsNullOrWhiteSpace(filterWarning) ? string.Empty : $" Filter warning: {filterWarning}");
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

        if (taskToComplete.ExpiredValue <= 0 || taskToComplete.WarningValue < 0)
        {
            Message = "Cannot complete this task: expired value must be positive and warning value cannot be negative. Edit the task cycle first.";
            return;
        }

        AppLogger.Info($"Mark Complete clicked for task {taskToComplete.TaskId}: {DescribeTask(taskToComplete)}");
        var selectedMinors = _minorCompletionDrafts.Where(item => item.IsSelected).ToList();
        if (selectedMinors.Any(item => item.CompletionDate is null))
        {
            Message = "Every selected minor task needs a completion date.";
            return;
        }

        var selectedMinorDisplay = selectedMinors.Count == 0
            ? "None"
            : string.Join(", ", selectedMinors.Select(item => item.MinorTask.Name));

        if (MessageBox.Show(
                $"Mark this task as completed?\n\n{DescribeTask(taskToComplete)}\n\nMinor tasks: {selectedMinorDisplay}",
                "Confirm Complete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            AppLogger.Info($"Mark Complete cancelled for row {taskToComplete.RowNumber}: {DescribeTask(taskToComplete)}");
            Message = "Mark Complete cancelled.";
            return;
        }

        await CompleteSelectedTaskAsync(selectedMinors);
    }

    private void PrepareMinorCompletionDrafts(SheetTask? task)
    {
        _minorCompletionDrafts.Clear();
        if (task is null)
        {
            return;
        }

        foreach (var minor in task.ActiveMinorTasks)
        {
            _minorCompletionDrafts.Add(new MinorTaskCompletionDraft
            {
                MinorTask = minor,
                CompletionDate = CompletionDate ?? DateTime.Today
            });
        }
    }

    private async Task CompleteSelectedTaskAsync(IReadOnlyList<MinorTaskCompletionDraft> selectedMinors)
    {
        if (SelectedTask is null) return;

        var taskToComplete = SelectedTask;
        var completionDate = CompletionDate ?? DateTime.Today;
        var remark = SelectedRemarkDraft;

        IsMarkingComplete = true;
        Message = "Completing task...";

        try
        {
            var beforeTask = CloneTask(taskToComplete);
            var affectedFollowers = _tasks
                .Where(task => !task.Archived
                    && !task.IsLinkedUnlocked
                    && string.Equals(task.PredecessorTaskId, taskToComplete.TaskId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var beforeAffectedTasks = affectedFollowers.Select(CloneTask).ToList();
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

            var minorPayloads = new List<MinorTaskCompletionPayload>();
            var minorSummaryItems = new List<string>();
            foreach (var draft in selectedMinors)
            {
                var minor = taskToComplete.MinorTasks.First(item => string.Equals(
                    item.MinorTaskId,
                    draft.MinorTask.MinorTaskId,
                    StringComparison.OrdinalIgnoreCase));
                var minorDate = draft.CompletionDate!.Value.Date;
                minor.LatestCompletionDate = minorDate;
                minor.DueDate = minor.IntervalValue is > 0
                    ? AddTaskInterval(minorDate, minor.IntervalValue.Value, minor.IntervalUnit)
                    : null;
                minor.NotifyCalculatedFieldsChanged();
                minorPayloads.Add(new MinorTaskCompletionPayload
                {
                    MinorTaskId = minor.MinorTaskId,
                    CompletionDate = minorDate
                });
                minorSummaryItems.Add($"{minor.Name} ({minorDate:dd MMM yyyy})");
            }

            if (!string.IsNullOrWhiteSpace(taskToComplete.PredecessorTaskId))
            {
                taskToComplete.IsLinkedUnlocked = false;
                taskToComplete.LinkedActivationDate = null;
            }
            foreach (var follower in affectedFollowers)
            {
                RestoreMissingLinkedDates(follower);
                follower.IsLinkedUnlocked = true;
                follower.LinkedActivationDate = completionDate.Date;
                follower.NotifyCalculatedFieldsChanged();
            }
            taskToComplete.NotifyCalculatedFieldsChanged();
            var mutation = CreateMutation(taskToComplete, TaskMutationTypes.Complete);
            mutation.Payload.ExecuteDate = completionDate;
            mutation.Payload.Remark = remark;
            mutation.Payload.MinorCompletions = minorPayloads;
            mutation.UploadAfter = DateTimeOffset.Now.Add(CompletionUploadDelay);
            _taskSyncQueue.Add(mutation);
            _completionHistoryRecords.Add(new CompletionHistoryRecord
            {
                OperationId = mutation.OperationId,
                TaskId = taskToComplete.TaskId,
                CompletedDate = completionDate.Date,
                Category = taskToComplete.Category,
                Type = taskToComplete.Type,
                Task = taskToComplete.Task,
                Remark = remark,
                State = CompletionHistoryStates.Pending,
                BeforeTask = beforeTask,
                BeforeAffectedTasks = beforeAffectedTasks,
                MinorCompletionSummary = string.Join(", ", minorSummaryItems)
            });
            RefreshTaskRelationships();
            await PersistTaskStateAsync();
            SelectedTask = null;
            IsTaskSidebarOpen = false;
            SelectedRemarkDraft = string.Empty;
            RebuildFilterLists();
            RebuildTaskSummary();
            RebuildPriorityItems();
            RebuildCompletionHistory();
            RebuildPausedTasks();
            IsPriorityDetailOpen = false;
            TasksView.Refresh();
            RefreshTaskExpansionToggle();
            Message = $"Completion saved locally. Undo is available until it syncs after {mutation.UploadAfter:HH:mm}. {TaskSyncDisplay}.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to mark complete locally", ex);
            Message = $"Failed to mark complete locally: {ex.Message}. Log: {AppPaths.CurrentWarningErrorLogPath}";
        }
        finally
        {
            IsMarkingComplete = false;
            _minorCompletionDrafts.Clear();
        }
    }

    private Task ClearFiltersAsync()
    {
        ResetFilters(resetSortMode: true);
        return Task.CompletedTask;
    }

    private Task ToggleAllTaskDetailsAsync()
    {
        var expandableTasks = GetVisibleExpandableTasks();
        if (expandableTasks.Count == 0)
        {
            return Task.CompletedTask;
        }

        var expand = !expandableTasks.All(task => task.IsExpanded);
        foreach (var task in expandableTasks)
        {
            task.IsExpanded = expand;
        }

        TasksView.Refresh();
        RefreshTaskExpansionToggle();
        return Task.CompletedTask;
    }

    public void NotifyTaskExpansionChanged()
    {
        RefreshTaskExpansionToggle();
    }

    private Task OpenFilterManagerAsync()
    {
        _filterManagerDrafts.Clear();
        foreach (var filter in _taskFilters.Select(CloneTaskFilter)) _filterManagerDrafts.Add(filter);
        FilterTaskSearchText = string.Empty;
        var selectedId = SelectedSavedFilter?.FilterId ?? TaskFilterIds.Default;
        ManagedFilter = _filterManagerDrafts.FirstOrDefault(item => item.FilterId == selectedId)
            ?? _filterManagerDrafts.FirstOrDefault();
        OnPropertyChanged(nameof(FilterManagerDrafts));
        IsFilterManagerOpen = true;
        return Task.CompletedTask;
    }

    private Task CloseFilterManagerAsync()
    {
        ManagedFilter = null;
        _filterManagerDrafts.Clear();
        FilterTaskSearchText = string.Empty;
        IsFilterManagerOpen = false;
        Message = "Filter changes discarded.";
        return Task.CompletedTask;
    }

    private Task NewFilterAsync()
    {
        var filter = new TaskFilterDefinition
        {
            Name = $"New Filter {_filterManagerDrafts.Count(item => !item.IsSystem) + 1}",
            SortOrder = _filterManagerDrafts.Count
        };
        _filterManagerDrafts.Add(filter);
        ManagedFilter = filter;
        OnPropertyChanged(nameof(FilterManagerDrafts));
        return Task.CompletedTask;
    }

    private async Task SaveManagedFilterAsync()
    {
        var customFilters = _filterManagerDrafts.Where(item => !item.IsSystem).ToList();
        if (customFilters.Any(item => string.IsNullOrWhiteSpace(item.Name))
            || customFilters.GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            Message = "Every custom filter needs a unique name.";
            return;
        }

        foreach (var filter in customFilters)
        {
            filter.Name = filter.Name.Trim();
            filter.TaskIds = filter.TaskIds
                .Where(id => _tasks.Any(task => string.Equals(task.TaskId, id, StringComparison.OrdinalIgnoreCase) && !task.IsEffectivelyPaused))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        if (_filterManagerDrafts.Count(item => item.IsFavourite) != 1)
        {
            foreach (var filter in _filterManagerDrafts) filter.IsFavourite = filter.FilterId == TaskFilterIds.Default;
        }

        var selectedId = SelectedSavedFilter?.FilterId;
        _taskFilters.Clear();
        foreach (var filter in _filterManagerDrafts.Select(CloneTaskFilter)) _taskFilters.Add(filter);
        OnPropertyChanged(nameof(TaskFilters));
        SelectedSavedFilter = _taskFilters.FirstOrDefault(item => item.FilterId == selectedId)
            ?? _taskFilters.FirstOrDefault(item => item.IsFavourite)
            ?? _taskFilters.First(item => item.FilterId == TaskFilterIds.Default);
        await SaveTaskFiltersLocallyAsync(pending: false);
        TasksView.Refresh();
        IsFilterManagerOpen = false;
        ManagedFilter = null;
        _filterManagerDrafts.Clear();
        Message = "Filter configuration saved.";
    }

    private Task DeleteManagedFilterAsync()
    {
        if (ManagedFilter is null || ManagedFilter.IsSystem) return Task.CompletedTask;
        var removed = ManagedFilter;
        _filterManagerDrafts.Remove(removed);
        ManagedFilter = _filterManagerDrafts.FirstOrDefault();
        Message = $"'{removed.Name}' will be deleted when Save is pressed.";
        return Task.CompletedTask;
    }

    private Task SetFavouriteManagedFilterAsync()
    {
        if (ManagedFilter is null) return Task.CompletedTask;
        foreach (var filter in _filterManagerDrafts) filter.IsFavourite = ReferenceEquals(filter, ManagedFilter);
        OnPropertyChanged(nameof(FilterManagerDrafts));
        Message = $"'{ManagedFilter.Name}' will become the startup filter when Save is pressed.";
        return Task.CompletedTask;
    }

    private void LoadTaskFilters(IEnumerable<TaskFilterDefinition> filters)
    {
        _taskFilters.Clear();
        var incoming = filters.ToList();
        var all = incoming.FirstOrDefault(item => item.FilterId == TaskFilterIds.All)
            ?? new TaskFilterDefinition { FilterId = TaskFilterIds.All, Name = "ALL", IsSystem = true, SortOrder = 0 };
        var defaultFilter = incoming.FirstOrDefault(item => item.FilterId == TaskFilterIds.Default)
            ?? new TaskFilterDefinition { FilterId = TaskFilterIds.Default, Name = "DEFAULT", IsSystem = true, SortOrder = 1 };
        all.IsSystem = true;
        defaultFilter.IsSystem = true;
        _taskFilters.Add(all);
        _taskFilters.Add(defaultFilter);
        foreach (var filter in incoming.Where(item => item.FilterId != TaskFilterIds.All && item.FilterId != TaskFilterIds.Default)
                     .OrderBy(item => item.SortOrder).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            filter.IsSystem = false;
            _taskFilters.Add(filter);
        }
        var favourite = _taskFilters.FirstOrDefault(item => item.IsFavourite) ?? defaultFilter;
        foreach (var filter in _taskFilters) filter.IsFavourite = ReferenceEquals(filter, favourite);
        _selectedSavedFilter = favourite;
        OnPropertyChanged(nameof(TaskFilters));
        OnPropertyChanged(nameof(SelectedSavedFilter));
    }

    private static TaskFilterDefinition CloneTaskFilter(TaskFilterDefinition source) => new()
    {
        FilterId = source.FilterId,
        Name = source.Name,
        IsSystem = source.IsSystem,
        IsFavourite = source.IsFavourite,
        SortOrder = source.SortOrder,
        TaskIds = source.TaskIds.ToList()
    };

    private void RebuildFilterTaskSelections()
    {
        foreach (var oldItem in _filterTaskSelectionItems) oldItem.PropertyChanged -= FilterTaskSelectionChanged;
        _filterTaskSelectionItems.Clear();
        if (ManagedFilter is null) return;
        var selectedIds = ManagedFilter.TaskIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var task in _tasks.OrderBy(item => item.HierarchyOrder))
        {
            if (!string.IsNullOrWhiteSpace(FilterTaskSearchText)
                && !task.FullPath.Contains(FilterTaskSearchText.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            var isFollower = !string.IsNullOrWhiteSpace(task.PredecessorTaskId);
            var effectiveChecked = ManagedFilter.FilterId == TaskFilterIds.All
                ? !task.Archived
                : ManagedFilter.FilterId == TaskFilterIds.Default
                    ? IsActiveViewTask(task)
                    : selectedIds.Contains(task.TaskId);
            var rootIncluded = selectedIds.Contains(FindRootTask(task).TaskId);
            var membershipMismatch = isFollower && !task.IsEffectivelyPaused && rootIncluded != selectedIds.Contains(task.TaskId);
            var item = new FilterTaskSelectionItem
            {
                Task = task,
                IsChecked = effectiveChecked,
                IsEnabled = !ManagedFilter.IsSystem && !task.IsEffectivelyPaused && !isFollower,
                StatusDisplay = task.IsEffectivelyPaused ? "Paused" : membershipMismatch ? "Filter mismatch" : isFollower ? "Follows main task" : string.Empty
            };
            item.PropertyChanged += FilterTaskSelectionChanged;
            _filterTaskSelectionItems.Add(item);
        }
    }

    private void FilterTaskSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updatingFilterSelections || e.PropertyName != nameof(FilterTaskSelectionItem.IsChecked)
            || sender is not FilterTaskSelectionItem { IsEnabled: true } root) return;
        _updatingFilterSelections = true;
        try
        {
            var groupIds = new[] { root.Task }.Concat(GetDescendants(root.Task.TaskId))
                .Where(item => !item.IsEffectivelyPaused)
                .Select(item => item.TaskId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            ManagedFilter!.TaskIds.RemoveAll(id => groupIds.Contains(id));
            if (root.IsChecked) ManagedFilter.TaskIds.AddRange(groupIds);
            foreach (var item in _filterTaskSelectionItems.Where(item => groupIds.Contains(item.Task.TaskId)))
            {
                item.IsChecked = root.IsChecked;
            }
        }
        finally { _updatingFilterSelections = false; }
    }

    private IEnumerable<SheetTask> GetDescendants(string taskId)
    {
        return GetDescendants(taskId, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private IEnumerable<SheetTask> GetDescendants(string taskId, HashSet<string> visited)
    {
        if (!visited.Add(taskId)) yield break;
        foreach (var child in _tasks.Where(item => string.Equals(item.PredecessorTaskId, taskId, StringComparison.OrdinalIgnoreCase)))
        {
            yield return child;
            foreach (var descendant in GetDescendants(child.TaskId, visited)) yield return descendant;
        }
    }

    private SheetTask FindRootTask(SheetTask task)
    {
        var current = task;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrWhiteSpace(current.PredecessorTaskId) && visited.Add(current.TaskId))
        {
            var predecessor = _tasks.FirstOrDefault(item => string.Equals(item.TaskId, current.PredecessorTaskId, StringComparison.OrdinalIgnoreCase));
            if (predecessor is null) break;
            current = predecessor;
        }
        return current;
    }

    private string FindFilterMembershipWarning()
    {
        foreach (var filter in _taskFilters.Where(item => !item.IsSystem))
        {
            foreach (var task in _tasks.Where(item => !item.Archived && !item.IsEffectivelyPaused && !string.IsNullOrWhiteSpace(item.PredecessorTaskId)))
            {
                var root = FindRootTask(task);
                var rootIncluded = filter.TaskIds.Contains(root.TaskId, StringComparer.OrdinalIgnoreCase);
                var taskIncluded = filter.TaskIds.Contains(task.TaskId, StringComparer.OrdinalIgnoreCase);
                if (rootIncluded != taskIncluded) return $"'{task.Task}' does not follow its main task in '{filter.Name}'.";
            }
        }
        return string.Empty;
    }

    private async Task SaveTaskFiltersLocallyAsync(bool pending)
    {
        _taskFilterState = new TaskFilterState { Filters = _taskFilters.ToList(), PendingUpload = pending };
        await _fileStore.SaveTaskFiltersAsync(_taskFilterState);
    }

    private Task ShowTasksViewAsync()
    {
        CloseAllPopupsAndSidebars();
        CurrentMainView = MainViewKind.Tasks;
        return Task.CompletedTask;
    }

    private Task ShowPriorityViewAsync()
    {
        CloseAllPopupsAndSidebars();
        SelectedPriorityTask = null;
        IsPriorityDetailOpen = false;
        RebuildPriorityItems();
        CurrentMainView = MainViewKind.Priority;
        return Task.CompletedTask;
    }

    private Task ShowDailySummaryViewAsync()
    {
        CloseAllPopupsAndSidebars();
        RebuildTaskSummary();
        CurrentMainView = MainViewKind.DailySummary;
        return Task.CompletedTask;
    }

    private Task ShowHistoryViewAsync()
    {
        CloseAllPopupsAndSidebars();
        RebuildCompletionHistory();
        CurrentMainView = MainViewKind.History;
        return Task.CompletedTask;
    }

    private Task ShowPreviousHistoryMonthAsync()
    {
        _historyMonth = _historyMonth.AddMonths(-1);
        OnPropertyChanged(nameof(HistoryMonthDisplay));
        RebuildCompletionHistory();
        NextHistoryMonthCommand.RaiseCanExecuteChanged();
        return Task.CompletedTask;
    }

    private Task ShowNextHistoryMonthAsync()
    {
        if (!CanShowNextHistoryMonth()) return Task.CompletedTask;
        _historyMonth = _historyMonth.AddMonths(1);
        OnPropertyChanged(nameof(HistoryMonthDisplay));
        RebuildCompletionHistory();
        NextHistoryMonthCommand.RaiseCanExecuteChanged();
        return Task.CompletedTask;
    }

    private bool CanShowNextHistoryMonth()
    {
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        return _historyMonth < currentMonth;
    }

    private Task OpenPriorityDetailAsync()
    {
        if (SelectedPriorityTask is null)
        {
            return Task.CompletedTask;
        }

        SelectedTask = SelectedPriorityTask;
        IsTaskSidebarOpen = false;
        IsPriorityDetailOpen = true;
        return Task.CompletedTask;
    }

    private Task ClosePriorityDetailAsync()
    {
        IsPriorityDetailOpen = false;
        SelectedPriorityTask = null;
        SelectedExpiredPriorityTask = null;
        SelectedWarningPriorityTask = null;
        ClearTaskSelection();
        return Task.CompletedTask;
    }

    private Task CloseTaskSidebarAsync()
    {
        IsTaskSidebarOpen = false;
        SelectedRemarkDraft = string.Empty;
        _minorCompletionDrafts.Clear();
        return Task.CompletedTask;
    }

    private Task OpenSelectedTaskSidebarAsync()
    {
        if (SelectedTask is null)
        {
            return Task.CompletedTask;
        }

        IsTaskEditorOpen = false;
        PrepareMinorCompletionDrafts(SelectedTask);
        IsTaskSidebarOpen = true;
        return Task.CompletedTask;
    }

    private Task OpenSelectedTaskSummaryItemAsync()
    {
        if (SelectedTaskSummaryItem is null)
        {
            return Task.CompletedTask;
        }

        var task = SelectedTaskSummaryItem.TaskItem;
        SelectedTask = task;
        IsTaskSidebarOpen = true;
        return Task.CompletedTask;
    }

    private Task SnoozeSelectedTaskSummaryItemAsync(int days)
    {
        var currentSnoozeDate = SelectedTaskSummaryItem?.TaskItem.SnoozeUntil?.Date;
        var startDate = currentSnoozeDate >= DateTime.Today ? currentSnoozeDate.Value : DateTime.Today;
        return SnoozeSelectedTaskSummaryItemUntilAsync(startDate.AddDays(days));
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
            RebuildPriorityItems();
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
            RebuildPriorityItems();
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
        MainTaskSearchText = string.Empty;
        _isNewTaskDraft = true;
        TaskEditDraft = new TaskEditDraft();
        OnPropertyChanged(nameof(TaskEditorTitle));
        IsTaskEditorOpen = true;
        return Task.CompletedTask;
    }

    private Task AddMinorTaskAsync()
    {
        TaskEditDraft.MinorTasks.Add(new MinorTask
        {
            MinorTaskId = Guid.NewGuid().ToString("D"),
            ParentTaskId = TaskEditDraft.TaskId,
            Order = TaskEditDraft.MinorTasks.Count
        });
        return Task.CompletedTask;
    }

    private Task RemoveMinorTaskAsync(MinorTask minorTask)
    {
        TaskEditDraft.MinorTasks.Remove(minorTask);
        for (var index = 0; index < TaskEditDraft.MinorTasks.Count; index++)
        {
            TaskEditDraft.MinorTasks[index].Order = index;
        }
        return Task.CompletedTask;
    }

    private Task ClearTaskLinkAsync()
    {
        TaskEditDraft.PredecessorTaskId = string.Empty;
        OnPropertyChanged(nameof(TaskEditDraft));
        return Task.CompletedTask;
    }

    private Task ClearMinorTaskDateAsync(MinorTask minorTask)
    {
        minorTask.LatestCompletionDate = null;
        minorTask.DueDate = null;
        return Task.CompletedTask;
    }

    private Task ClearPauseResumeDateAsync()
    {
        PauseResumeDate = null;
        return Task.CompletedTask;
    }

    private Task EditTaskAsync()
    {
        if (SelectedTask is null)
        {
            return Task.CompletedTask;
        }

        _isNewTaskDraft = false;
        MainTaskSearchText = string.Empty;
        TaskEditDraft = new TaskEditDraft
        {
            TaskId = SelectedTask.TaskId,
            Level = SelectedTask.Level,
            Category = SelectedTask.Category,
            Type = SelectedTask.Type,
            Task = SelectedTask.Task,
            ExpiredValue = SelectedTask.ExpiredValue,
            ExpiredUnit = SelectedTask.ExpiredUnit,
            WarningValue = SelectedTask.WarningValue,
            WarningUnit = SelectedTask.WarningUnit,
            Alert = SelectedTask.Alert,
            History = SelectedTask.History,
            PredecessorTaskId = SelectedTask.PredecessorTaskId,
            MinorTasks = new ObservableCollection<MinorTask>(SelectedTask.ActiveMinorTasks.Select(CloneMinorTask))
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

        if (!TaskLevels.Contains(draft.Level))
        {
            Message = "Level must be between 1 and 5.";
            return;
        }

        if (draft.MinorTasks.Any(minor => string.IsNullOrWhiteSpace(minor.Name)))
        {
            Message = "Every minor task needs a name.";
            return;
        }

        if (draft.MinorTasks.Any(minor => minor.IntervalValue is <= 0))
        {
            Message = "A minor task interval must be blank or greater than zero.";
            return;
        }

        if (draft.MinorTasks.Any(minor => minor.IntervalValue is not null && !TaskCycleUnits.Contains(minor.IntervalUnit)))
        {
            Message = "Minor task cycle units must be Day, Month, or Year.";
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

        if (!ValidateTaskLink(task, draft.PredecessorTaskId, out var linkError))
        {
            Message = linkError;
            if (_isNewTaskDraft) _tasks.Remove(task);
            return;
        }

        DateTime? recalculatedExpiredDate = null;
        DateTime? recalculatedWarningDate = null;
        if (task.GridLastExecutedDate is DateTime recurrenceAnchor)
        {
            recalculatedExpiredDate = AddTaskInterval(recurrenceAnchor.Date, draft.ExpiredValue, draft.ExpiredUnit);
            recalculatedWarningDate = AddTaskInterval(recurrenceAnchor.Date, draft.WarningValue, draft.WarningUnit);
            if (recalculatedWarningDate > recalculatedExpiredDate)
            {
                Message = "Warning date cannot be after expired date.";
                return;
            }
        }

        task.Level = draft.Level;
        task.Category = ToTitleCase(draft.Category);
        task.Type = ToTitleCase(draft.Type);
        task.Task = ToTitleCase(draft.Task);
        task.ExpiredValue = draft.ExpiredValue;
        task.ExpiredUnit = draft.ExpiredUnit;
        task.WarningValue = draft.WarningValue;
        task.WarningUnit = draft.WarningUnit;
        if (recalculatedExpiredDate is not null)
        {
            task.ExpiredDate = recalculatedExpiredDate;
            task.WarningDate = recalculatedWarningDate;
        }
        task.Alert = draft.Alert;
        task.History = draft.History;
        var oldPredecessorId = task.PredecessorTaskId;
        task.PredecessorTaskId = draft.PredecessorTaskId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(task.PredecessorTaskId))
        {
            task.IsLinkedUnlocked = true;
            task.LinkedActivationDate = null;
        }
        else if (!string.Equals(oldPredecessorId, task.PredecessorTaskId, StringComparison.OrdinalIgnoreCase))
        {
            task.IsLinkedUnlocked = task.ExpiredDate is not null || task.WarningDate is not null;
            task.LinkedActivationDate = task.IsLinkedUnlocked ? task.GridLastExecutedDate : null;
        }

        var retainedArchivedMinors = task.MinorTasks.Where(minor => minor.Archived).Select(CloneMinorTask).ToList();
        var draftMinorIds = draft.MinorTasks.Select(minor => minor.MinorTaskId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removedMinors = task.MinorTasks
            .Where(minor => !minor.Archived && !draftMinorIds.Contains(minor.MinorTaskId))
            .Select(CloneMinorTask)
            .ToList();
        foreach (var removedMinor in removedMinors) removedMinor.Archived = true;
        task.MinorTasks = draft.MinorTasks.Select((minor, index) =>
        {
            var copy = CloneMinorTask(minor);
            copy.ParentTaskId = task.TaskId;
            copy.Name = ToTitleCase(copy.Name);
            copy.Order = index;
            copy.Archived = false;
            copy.DueDate = copy.IntervalValue is > 0 && copy.LatestCompletionDate is DateTime minorAnchor
                ? AddTaskInterval(minorAnchor.Date, copy.IntervalValue.Value, copy.IntervalUnit)
                : null;
            return copy;
        }).Concat(removedMinors).Concat(retainedArchivedMinors).ToList();
        task.NotifyCalculatedFieldsChanged();

        if (!string.Equals(oldPredecessorId, task.PredecessorTaskId, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(task.PredecessorTaskId))
        {
            var root = FindRootTask(task);
            var groupIds = new[] { task }.Concat(GetDescendants(task.TaskId)).Select(item => item.TaskId).ToList();
            foreach (var filter in _taskFilters.Where(item => !item.IsSystem))
            {
                var shouldInclude = filter.TaskIds.Contains(root.TaskId, StringComparer.OrdinalIgnoreCase);
                filter.TaskIds.RemoveAll(id => groupIds.Contains(id, StringComparer.OrdinalIgnoreCase));
                if (shouldInclude) filter.TaskIds.AddRange(groupIds.Where(id => !filter.TaskIds.Contains(id, StringComparer.OrdinalIgnoreCase)));
            }
            await SaveTaskFiltersLocallyAsync(pending: false);
        }

        await QueueAndTryMutationAsync(CreateMutation(task, operationType));
        RefreshTaskRelationships();
        IsTaskEditorOpen = false;
        _isNewTaskDraft = false;
        SelectedTask = task;
        RebuildFilterLists();
        RebuildTaskSummary();
        RebuildPriorityItems();
        RebuildCompletionHistory();
        RebuildPausedTasks();
        TasksView.Refresh();
        RefreshTaskExpansionToggle();
        var membershipWarning = FindFilterMembershipWarning();
        Message = $"Saved '{task.Task}'. {TaskSyncDisplay}."
            + (string.IsNullOrWhiteSpace(membershipWarning) ? string.Empty : $" Filter warning: {membershipWarning}");
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
        if (task.LinkedFollowers.Count > 0)
        {
            Message = "Unlink this task's followers before archiving their source.";
            return;
        }
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
        RebuildPriorityItems();
        Message = $"Archived '{task.Task}'. {TaskSyncDisplay}.";
    }

    private Task OpenPausedManagerAsync()
    {
        RebuildPausedTasks();
        IsPausedManagerOpen = true;
        return Task.CompletedTask;
    }

    private Task ClosePausedManagerAsync()
    {
        IsPausedManagerOpen = false;
        return Task.CompletedTask;
    }

    private Task OpenPauseSelectedTaskAsync()
    {
        if (SelectedTask is null) return Task.CompletedTask;
        _pauseTargetTask = SelectedTask;
        SavePauseCommand.RaiseCanExecuteChanged();
        PauseResumeDate = SelectedTask.ResumeDate;
        OnPropertyChanged(nameof(PauseTargetDisplay));
        IsPauseEditorOpen = true;
        return Task.CompletedTask;
    }

    private async Task SavePauseAsync()
    {
        if (_pauseTargetTask is null) return;
        if (PauseResumeDate?.Date <= DateTime.Today)
        {
            Message = "Resume date must be after today, or blank for an indefinite pause.";
            return;
        }

        var task = _pauseTargetTask;
        var activeFollowers = task.LinkedFollowers
            .Where(follower => !follower.IsEffectivelyPaused)
            .Select(follower => follower.Task)
            .ToList();
        if (activeFollowers.Count > 0)
        {
            Message = $"Pause the follower task(s) first: {string.Join(", ", activeFollowers)}.";
            return;
        }
        task.Paused = true;
        task.ResumeDate = PauseResumeDate?.Date;
        task.IsExpanded = false;
        task.NotifyCalculatedFieldsChanged();
        foreach (var filter in _taskFilters.Where(item => !item.IsSystem))
        {
            filter.TaskIds.RemoveAll(id => string.Equals(id, task.TaskId, StringComparison.OrdinalIgnoreCase));
        }
        await SaveTaskFiltersLocallyAsync(pending: false);
        await QueueAndTryMutationAsync(CreateMutation(task, TaskMutationTypes.Pause));
        IsPauseEditorOpen = false;
        _pauseTargetTask = null;
        SavePauseCommand.RaiseCanExecuteChanged();
        ClearTaskSelection();
        RefreshActiveViews();
        Message = $"Paused '{task.Task}'. Its current dates are unchanged.";
    }

    private Task CancelPauseAsync()
    {
        IsPauseEditorOpen = false;
        _pauseTargetTask = null;
        SavePauseCommand.RaiseCanExecuteChanged();
        PauseResumeDate = null;
        return Task.CompletedTask;
    }

    private async Task ResumeTaskAsync(SheetTask task)
    {
        if (!string.IsNullOrWhiteSpace(task.PredecessorTaskId))
        {
            var predecessor = _tasks.FirstOrDefault(item => string.Equals(
                item.TaskId,
                task.PredecessorTaskId,
                StringComparison.OrdinalIgnoreCase));
            if (predecessor?.IsEffectivelyPaused == true)
            {
                Message = $"Resume the main task '{predecessor.Task}' first.";
                return;
            }
        }
        task.Paused = false;
        task.ResumeDate = null;
        task.NotifyCalculatedFieldsChanged();
        await QueueAndTryMutationAsync(CreateMutation(task, TaskMutationTypes.Resume));
        RefreshActiveViews();
        Message = $"Resumed '{task.Task}' with its original dates.";
    }

    private Task EditPausedTaskAsync(SheetTask task)
    {
        IsPausedManagerOpen = false;
        SelectedTask = task;
        return EditTaskAsync();
    }

    private Task EditLinkedTaskAsync(SheetTask task)
    {
        SelectedTask = task;
        return EditTaskAsync();
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

        var compoundHistory = _completionHistoryRecords.FirstOrDefault(record => string.Equals(
            record.OperationId,
            conflict.OperationId,
            StringComparison.OrdinalIgnoreCase));
        if (compoundHistory is not null)
        {
            foreach (var beforeAffected in compoundHistory.BeforeAffectedTasks)
            {
                var affected = _tasks.FirstOrDefault(task => string.Equals(
                    task.TaskId,
                    beforeAffected.TaskId,
                    StringComparison.OrdinalIgnoreCase));
                if (affected is not null) CopyTask(beforeAffected, affected);
            }
            compoundHistory.State = CompletionHistoryStates.Undone;
            compoundHistory.CanUndo = false;
            compoundHistory.BeforeTask = null;
            compoundHistory.BeforeAffectedTasks.Clear();
        }

        _taskSyncQueue.RemoveAll(item => item.TaskId == conflict.TaskId);
        await PersistTaskStateAsync();
        RefreshTaskRelationships();
        RebuildPausedTasks();
        RebuildTaskConflicts();
        TasksView.Refresh();
        RebuildTaskSummary();
        RebuildPriorityItems();
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

    private Task OpenSettingsAsync()
    {
        ResetSettingsDraft();
        IsSettingsOpen = true;
        return Task.CompletedTask;
    }

    private async Task SaveSettingsAsync()
    {
        var invalidDay = CheckinSettingsDraft.FirstOrDefault(day => !TryParseCheckinTime(day.TimeText, out _));
        if (invalidDay is not null)
        {
            Message = $"{invalidDay.DayName} check-in time must use HHmm, for example 1200.";
            return;
        }

        _configDraft.LogRetentionDays = Math.Max(1, _configDraft.LogRetentionDays);
        _config = CloneAppConfig(_configDraft);
        _checkinSettings.Days = new ObservableCollection<CheckinDaySetting>(
            CheckinSettingsDraft.Select(CloneCheckinDaySetting));
        await _fileStore.SaveConfigAsync(_config);
        await _fileStore.SaveCheckinSettingsAsync(_checkinSettings);
        AppLogger.PruneOldFiles(_config.LogRetentionDays);
        IsSettingsOpen = false;
        Message = "Saved settings.";
        RefreshCommands();
        await CheckAndNotifyCheckinAsync();
    }

    private Task CancelSettingsAsync()
    {
        ResetSettingsDraft();
        IsSettingsOpen = false;
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

    private void ResetSettingsDraft()
    {
        _configDraft = CloneAppConfig(_config);
        OnPropertyChanged(nameof(GoogleAppsScriptUrl));
        OnPropertyChanged(nameof(ApiKey));
        OnPropertyChanged(nameof(LogRetentionDays));
        ResetCheckinSettingsDraft();
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

        var previousMonth = new DateTime(
            _lastTaskCalculatedFieldsRefreshDate.Year,
            _lastTaskCalculatedFieldsRefreshDate.Month,
            1);
        var currentMonth = new DateTime(today.Year, today.Month, 1);
        if (_historyMonth == previousMonth)
        {
            _historyMonth = currentMonth;
            OnPropertyChanged(nameof(HistoryMonthDisplay));
            NextHistoryMonthCommand.RaiseCanExecuteChanged();
        }

        foreach (var task in _tasks)
        {
            task.NotifyCalculatedFieldsChanged();
        }

        RebuildPausedTasks();
        TasksView.Refresh();
        RebuildTaskSummary();
        RebuildPriorityItems();
        RebuildCompletionHistory();
        ApplyTaskSort();
        _lastTaskCalculatedFieldsRefreshDate = today;
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

    private static AppConfig CloneAppConfig(AppConfig source)
    {
        return new AppConfig
        {
            GoogleAppsScriptUrl = source.GoogleAppsScriptUrl,
            ApiKey = source.ApiKey,
            LogRetentionDays = source.LogRetentionDays
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
        TasksView.Refresh();
        RefreshTaskExpansionToggle();
    }

    private List<SheetTask> GetVisibleExpandableTasks() => TasksView
        .Cast<SheetTask>()
        .Where(task => task.HasExpandableDetails)
        .ToList();

    private bool AreAllVisibleTaskDetailsExpanded
    {
        get
        {
            var expandableTasks = GetVisibleExpandableTasks();
            return expandableTasks.Count > 0 && expandableTasks.All(task => task.IsExpanded);
        }
    }

    private bool CanToggleAllTaskDetails() => GetVisibleExpandableTasks().Count > 0;

    private void RefreshTaskExpansionToggle()
    {
        OnPropertyChanged(nameof(ExpandCollapseAllGlyph));
        OnPropertyChanged(nameof(ExpandCollapseAllToolTip));
        ToggleAllTaskDetailsCommand.RaiseCanExecuteChanged();
    }

    private void ResetFilters(bool resetSortMode)
    {
        _categoryFilter = AllFilter;
        _typeFilter = AllFilter;
        _statusFilter = AllFilter;
        _taskSearchText = string.Empty;
        OnPropertyChanged(nameof(CategoryFilter));
        OnPropertyChanged(nameof(TypeFilter));
        OnPropertyChanged(nameof(StatusFilter));
        OnPropertyChanged(nameof(TaskSearchText));
        ApplyTaskFilters();
    }

    private void ReplaceTasks(IEnumerable<SheetTask> tasks, bool rebuildSecondaryViews = true)
    {
        var preparedTasks = tasks.ToList();
        foreach (var task in preparedTasks)
        {
            task.NotifyCalculatedFieldsChanged();
        }

        _tasks.ReplaceAll(preparedTasks);

        RefreshTaskRelationships();
        RebuildPausedTasks();

        RefreshTaskSyncState();
        RebuildFilterLists();
        if (rebuildSecondaryViews)
        {
            RebuildTaskSummary();
            RebuildPriorityItems();
            RebuildCompletionHistory();
            AreSecondaryViewsReady = true;
        }
        ApplyTaskSort();
        RefreshTaskExpansionToggle();
    }

    private void RefreshTaskRelationships()
    {
        var followersBySource = _tasks
            .Where(task => !task.Archived && !string.IsNullOrWhiteSpace(task.PredecessorTaskId))
            .GroupBy(task => task.PredecessorTaskId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.AsEnumerable(), StringComparer.OrdinalIgnoreCase);
        foreach (var task in _tasks)
        {
            task.SetLinkedFollowers(followersBySource.GetValueOrDefault(task.TaskId) ?? []);
            task.PredecessorTaskName = _tasks.FirstOrDefault(item => string.Equals(
                item.TaskId,
                task.PredecessorTaskId,
                StringComparison.OrdinalIgnoreCase))?.Task ?? string.Empty;
            task.NotifyCalculatedFieldsChanged();
        }
        AssignHierarchyOrder();
        OnPropertyChanged(nameof(LinkSourceOptions));
    }

    private void AssignHierarchyOrder()
    {
        var byId = _tasks.Where(task => !string.IsNullOrWhiteSpace(task.TaskId))
            .ToDictionary(task => task.TaskId, StringComparer.OrdinalIgnoreCase);
        var roots = _tasks.Where(task => string.IsNullOrWhiteSpace(task.PredecessorTaskId)
                || !byId.ContainsKey(task.PredecessorTaskId))
            .OrderBy(task => task.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(task => task.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(task => task.Task, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 0;
        void Visit(SheetTask task, int depth)
        {
            if (!visited.Add(task.TaskId)) return;
            task.HierarchyDepth = depth;
            task.HierarchyOrder = order++;
            foreach (var child in task.LinkedFollowers
                         .OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(item => item.Type, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(item => item.Task, StringComparer.OrdinalIgnoreCase))
            {
                Visit(child, depth + 1);
            }
        }
        foreach (var root in roots) Visit(root, 0);
        foreach (var task in _tasks.Where(task => !visited.Contains(task.TaskId))) Visit(task, 0);
    }

    private void RebuildPausedTasks()
    {
        _pausedTaskItems.Clear();
        foreach (var task in _tasks
                     .Where(task => !task.Archived && task.IsEffectivelyPaused)
                     .OrderBy(task => task.ResumeDate is null ? 1 : 0)
                     .ThenBy(task => task.ResumeDate)
                     .ThenBy(task => task.Category, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(task => task.Type, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(task => task.Task, StringComparer.OrdinalIgnoreCase))
        {
            _pausedTaskItems.Add(task);
        }
        OnPropertyChanged(nameof(PausedTaskCount));
        OnPropertyChanged(nameof(PausedButtonDisplay));
    }

    private void RefreshActiveViews()
    {
        RefreshTaskRelationships();
        RebuildPausedTasks();
        RebuildFilterLists();
        TasksView.Refresh();
        RefreshTaskExpansionToggle();
        RebuildTaskSummary();
        RebuildPriorityItems();
    }

    private bool ValidateTaskLink(SheetTask task, string? predecessorTaskId, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(predecessorTaskId)) return true;
        if (string.Equals(task.TaskId, predecessorTaskId, StringComparison.OrdinalIgnoreCase))
        {
            error = "A task cannot link to itself.";
            return false;
        }

        var source = _tasks.FirstOrDefault(item => string.Equals(item.TaskId, predecessorTaskId, StringComparison.OrdinalIgnoreCase));
        if (source is null || source.Archived)
        {
            error = "The selected linked source is no longer available.";
            return false;
        }
        var current = source;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrWhiteSpace(current.PredecessorTaskId))
        {
            if (!visited.Add(current.TaskId)
                || string.Equals(current.PredecessorTaskId, task.TaskId, StringComparison.OrdinalIgnoreCase))
            {
                error = "This main task would create a linked-task cycle.";
                return false;
            }
            current = _tasks.FirstOrDefault(item => string.Equals(
                item.TaskId,
                current.PredecessorTaskId,
                StringComparison.OrdinalIgnoreCase));
            if (current is null) break;
        }
        return true;
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

    private async Task ProcessPendingMutationsAsync(string? onlyTaskId = null, bool includeDelayed = false)
    {
        await _taskSyncGate.WaitAsync();
        try
        {
            var uploadFailures = new List<string>();
            var now = DateTimeOffset.Now;
            var taskIds = _taskSyncQueue
                .Where(item => item.State == TaskMutationStates.Pending
                    && (onlyTaskId is null || item.TaskId == onlyTaskId))
                .GroupBy(item => item.TaskId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(item => item.QueuedAt).First())
                .Where(item => includeDelayed || item.UploadAfter is null || item.UploadAfter <= now)
                .OrderBy(item => item.QueuedAt)
                .Select(item => item.TaskId)
                .ToList();

            foreach (var taskId in taskIds)
            {
                while (true)
                {
                    var mutation = _taskSyncQueue
                        .Where(item => item.TaskId == taskId
                            && item.State == TaskMutationStates.Pending)
                        .OrderBy(item => item.QueuedAt)
                        .FirstOrDefault();
                    if (mutation is null)
                    {
                        break;
                    }
                    if (!includeDelayed && mutation.UploadAfter is not null && mutation.UploadAfter > now)
                    {
                        break;
                    }

                    try
                    {
                        var response = await _sheetClient.SendMutationAsync(_config, mutation, CancellationToken.None);
                        if (response.Success && response.Task is not null)
                        {
                            var local = _tasks.FirstOrDefault(task => task.TaskId == taskId);
                            if (local is not null)
                            {
                                CopyTask(response.Task, local);
                            }
                            foreach (var affectedServerTask in response.AffectedTasks)
                            {
                                var affectedLocal = _tasks.FirstOrDefault(task => string.Equals(
                                    task.TaskId,
                                    affectedServerTask.TaskId,
                                    StringComparison.OrdinalIgnoreCase));
                                if (affectedLocal is null)
                                {
                                    _tasks.Add(CloneTask(affectedServerTask));
                                }
                                else if (!_taskSyncQueue.Any(item => item != mutation
                                             && string.Equals(item.TaskId, affectedLocal.TaskId, StringComparison.OrdinalIgnoreCase)))
                                {
                                    CopyTask(affectedServerTask, affectedLocal);
                                }
                                else
                                {
                                    CopyServerManagedState(affectedServerTask, affectedLocal);
                                }

                                foreach (var pendingAffected in _taskSyncQueue.Where(item => item != mutation
                                             && string.Equals(item.TaskId, affectedServerTask.TaskId, StringComparison.OrdinalIgnoreCase)
                                             && item.State == TaskMutationStates.Pending))
                                {
                                    pendingAffected.ExpectedRevision = affectedServerTask.Revision;
                                }
                            }
                            _taskSyncQueue.Remove(mutation);
                            MarkCompletionHistorySynced(mutation.OperationId);
                            foreach (var next in _taskSyncQueue.Where(item => item.TaskId == taskId && item.State == TaskMutationStates.Pending))
                            {
                                next.ExpectedRevision = response.Task.Revision;
                            }
                            await PersistTaskStateAsync();
                            RefreshTaskRelationships();
                            RebuildPausedTasks();
                            continue;
                        }

                        if (string.Equals(response.ErrorCode, "REVISION_CONFLICT", StringComparison.OrdinalIgnoreCase))
                        {
                            mutation.State = TaskMutationStates.Conflict;
                            mutation.ServerTask = response.ServerTask;
                            MarkCompletionHistoryConflict(mutation.OperationId);
                            await PersistTaskStateAsync();
                            break;
                        }

                        throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Error)
                            ? $"Google Sheet rejected {mutation.OperationType}."
                            : response.Error);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Pending task mutation failed; continuing with other tasks: {taskId}", ex);
                        uploadFailures.Add($"{mutation.Payload.Task}: {ex.Message}");
                        break;
                    }
                }
            }

            RefreshTaskSyncState();
            RebuildPriorityItems();
            if (uploadFailures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{uploadFailures.Count} pending change(s) could not upload. First failure: {uploadFailures[0]}");
            }
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
        foreach (var affectedTaskId in _completionHistoryRecords
                     .Where(record => record.State is CompletionHistoryStates.Pending or CompletionHistoryStates.Conflict)
                     .SelectMany(record => record.BeforeAffectedTasks)
                     .Select(task => task.TaskId))
        {
            pendingIds.Add(affectedTaskId);
        }
        var localPending = _tasks.Where(task => pendingIds.Contains(task.TaskId)).ToList();
        var merged = serverById.Values
            .Where(task => !task.Archived && !pendingIds.Contains(task.TaskId))
            .Select(CloneTask)
            .Concat(localPending)
            .ToList();

        ReplaceTasks(merged);
    }

    private void MergeServerCompletionHistory(IEnumerable<CompletionHistoryRecord> serverRecords)
    {
        foreach (var serverRecord in serverRecords)
        {
            if (string.IsNullOrWhiteSpace(serverRecord.TaskId) || serverRecord.CompletedDate == default) continue;

            var existing = !string.IsNullOrWhiteSpace(serverRecord.OperationId)
                ? _completionHistoryRecords.FirstOrDefault(record => string.Equals(
                    record.OperationId,
                    serverRecord.OperationId,
                    StringComparison.OrdinalIgnoreCase))
                : _completionHistoryRecords.FirstOrDefault(record =>
                    string.Equals(record.TaskId, serverRecord.TaskId, StringComparison.OrdinalIgnoreCase)
                    && record.CompletedDate.Date == serverRecord.CompletedDate.Date
                    && string.Equals(record.Remark, serverRecord.Remark, StringComparison.Ordinal));
            if (existing is not null)
            {
                if (existing.State != CompletionHistoryStates.Undone)
                {
                    existing.State = CompletionHistoryStates.Synced;
                    existing.CanUndo = false;
                    if (!string.IsNullOrWhiteSpace(serverRecord.MinorCompletionSummary))
                    {
                        existing.MinorCompletionSummary = serverRecord.MinorCompletionSummary;
                    }
                    existing.BeforeTask = null;
                    existing.BeforeAffectedTasks.Clear();
                }
                continue;
            }

            serverRecord.State = CompletionHistoryStates.Synced;
            serverRecord.CanUndo = false;
            serverRecord.BeforeTask = null;
            _completionHistoryRecords.Add(serverRecord);
        }

        RebuildCompletionHistory();
    }

    private async Task PersistTaskStateAsync()
    {
        RefreshTaskSyncState();
        await _fileStore.SaveTasksAsync(_tasks);
        await _fileStore.SaveTaskSyncQueueAsync(_taskSyncQueue);
        await _fileStore.SaveCompletionHistoryAsync(_completionHistoryRecords);
        RebuildCompletionHistory();
    }

    private async Task BuildSecondaryViewsAfterStartupAsync()
    {
        try
        {
            // Let WPF complete the first Tasks layout before preparing hidden views.
            await Task.Delay(150);

            var taskSnapshot = _tasks.ToList();
            var secondaryData = await Task.Run(() => new SecondaryViewData(
                BuildPriorityRows(taskSnapshot, DateTime.Today),
                BuildTaskSummaryRows(taskSnapshot, DateTime.Today)));

            // A sync or edit may have replaced the task set while the snapshot was building.
            if (taskSnapshot.Count != _tasks.Count
                || !taskSnapshot.SequenceEqual(_tasks))
            {
                RebuildPriorityItems();
                RebuildTaskSummary();
                RebuildCompletionHistory();
            }
            else
            {
                ApplyPriorityRows(secondaryData.PriorityRows);
                ApplyTaskSummaryRows(secondaryData.SummaryRows);
                RebuildCompletionHistory();
            }

            AreSecondaryViewsReady = true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to prepare secondary task views", ex);
            Message = $"Tasks loaded, but secondary views failed: {ex.Message}";
        }
    }

    private void RebuildCompletionHistory()
    {
        foreach (var record in _completionHistoryRecords)
        {
            record.CanUndo = CanUndoCompletion(record);
        }

        UpdateMonthlyHistoryCounts();

        var monthEnd = _historyMonth.AddMonths(1);

        ApplyCompletionHistoryRows(_completionHistoryRecords
            .Where(record => record.CompletedDate.Date >= _historyMonth && record.CompletedDate.Date < monthEnd)
            .OrderByDescending(record => record.RecordedAt)
            .ThenByDescending(record => record.CompletedDate));
    }

    private void UpdateMonthlyHistoryCounts()
    {
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthEnd = currentMonth.AddMonths(1);
        var counts = _completionHistoryRecords
            .Where(record => record.State != CompletionHistoryStates.Undone
                && record.CompletedDate.Date >= currentMonth
                && record.CompletedDate.Date < monthEnd
                && !string.IsNullOrWhiteSpace(record.TaskId))
            .GroupBy(record => record.TaskId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var task in _tasks)
        {
            task.SetMonthlyHistoryCount(counts.GetValueOrDefault(task.TaskId));
        }
    }

    private void ApplyCompletionHistoryRows(IEnumerable<CompletionHistoryRecord> rows)
    {
        _completionHistoryItems.Clear();
        foreach (var row in rows)
        {
            _completionHistoryItems.Add(row);
        }

        OnPropertyChanged(nameof(HistoryViewDisplay));
        OnPropertyChanged(nameof(HasCompletionHistoryItems));
        UndoCompletionCommand.RaiseCanExecuteChanged();
    }

    private void RebuildPriorityItems()
    {
        ApplyPriorityRows(BuildPriorityRows(_tasks, DateTime.Today));
    }

    private static PriorityRows BuildPriorityRows(IEnumerable<SheetTask> tasks, DateTime today)
    {
        var active = tasks.Where(task => IsActiveViewTask(task) && !task.Completed && !IsTaskSnoozedForToday(task)).ToList();
        var expired = active
            .Where(task => task.ExpiredDate?.Date <= today)
            .OrderByDescending(task => task.Level)
            .ThenBy(task => task.DayLeft)
            .ThenBy(task => task.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(task => task.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(task => task.Task, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var warning = active
            .Where(task => task.ExpiredDate?.Date > today && task.WarningDate?.Date <= today)
            .OrderByDescending(task => task.Level)
            .ThenBy(task => task.DayLeft)
            .ThenBy(task => task.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(task => task.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(task => task.Task, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new PriorityRows(expired, warning);
    }

    private void ApplyPriorityRows(PriorityRows rows)
    {
        var selectedTaskId = SelectedPriorityTask?.TaskId;
        _expiredPriorityItems.Clear();
        foreach (var task in rows.Expired) _expiredPriorityItems.Add(task);
        _warningPriorityItems.Clear();
        foreach (var task in rows.Warning) _warningPriorityItems.Add(task);
        var refreshedExpired = selectedTaskId is null
            ? null
            : _expiredPriorityItems.FirstOrDefault(task => string.Equals(task.TaskId, selectedTaskId, StringComparison.OrdinalIgnoreCase));
        var refreshedWarning = selectedTaskId is null
            ? null
            : _warningPriorityItems.FirstOrDefault(task => string.Equals(task.TaskId, selectedTaskId, StringComparison.OrdinalIgnoreCase));
        _selectedExpiredPriorityTask = refreshedExpired;
        _selectedWarningPriorityTask = refreshedWarning;
        OnPropertyChanged(nameof(SelectedExpiredPriorityTask));
        OnPropertyChanged(nameof(SelectedWarningPriorityTask));
        SelectedPriorityTask = refreshedExpired ?? refreshedWarning;
        OnPropertyChanged(nameof(PriorityViewDisplay));
        OnPropertyChanged(nameof(HasExpiredPriorityItems));
        OnPropertyChanged(nameof(HasWarningPriorityItems));
    }

    private bool CanUndoCompletion(CompletionHistoryRecord record)
    {
        if (record.State != CompletionHistoryStates.Pending || record.BeforeTask is null)
        {
            return false;
        }

        var mutation = _taskSyncQueue.FirstOrDefault(item =>
            item.OperationId == record.OperationId
            && item.OperationType == TaskMutationTypes.Complete
            && item.State == TaskMutationStates.Pending);
        if (mutation is null)
        {
            return false;
        }

        var affectedTaskIds = record.BeforeAffectedTasks
            .Select(item => item.TaskId)
            .Append(mutation.TaskId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return !_taskSyncQueue.Any(item =>
            affectedTaskIds.Contains(item.TaskId)
            && item.QueuedAt > mutation.QueuedAt);
    }

    private async Task UndoCompletionAsync(CompletionHistoryRecord record)
    {
        var confirmation = MessageBox.Show(
            $"Undo this unsynced completion?\n\n{record.Category} / {record.Type} / {record.Task}\n{record.CompletedDate:dd MMM yyyy}",
            "Confirm Undo Completion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await _taskSyncGate.WaitAsync();
        try
        {
            if (!CanUndoCompletion(record) || record.BeforeTask is null)
            {
                Message = "Undo is no longer available because this completion synced or the task changed afterward.";
                RebuildCompletionHistory();
                return;
            }

            var mutation = _taskSyncQueue.First(item => item.OperationId == record.OperationId);
            var task = _tasks.FirstOrDefault(item =>
                string.Equals(item.TaskId, record.TaskId, StringComparison.OrdinalIgnoreCase));
            if (task is null)
            {
                Message = "Undo could not restore the task because it is missing from the local cache.";
                return;
            }

            CopyTask(record.BeforeTask, task);
            foreach (var beforeAffected in record.BeforeAffectedTasks)
            {
                var affected = _tasks.FirstOrDefault(item => string.Equals(
                    item.TaskId,
                    beforeAffected.TaskId,
                    StringComparison.OrdinalIgnoreCase));
                if (affected is not null)
                {
                    CopyTask(beforeAffected, affected);
                }
            }
            _taskSyncQueue.Remove(mutation);
            record.State = CompletionHistoryStates.Undone;
            record.CanUndo = false;
            await PersistTaskStateAsync();
            RebuildFilterLists();
            RebuildTaskSummary();
            RebuildPriorityItems();
            RefreshTaskRelationships();
            RebuildPausedTasks();
            TasksView.Refresh();
            Message = $"Undid completion for '{record.Task}'.";
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to undo completion {record.RecordId}", ex);
            Message = $"Undo failed: {ex.Message}. Log: {AppPaths.CurrentWarningErrorLogPath}";
        }
        finally
        {
            _taskSyncGate.Release();
        }
    }

    private void MarkCompletionHistorySynced(string operationId)
    {
        var record = _completionHistoryRecords.FirstOrDefault(item => item.OperationId == operationId);
        if (record is null)
        {
            return;
        }

        record.State = CompletionHistoryStates.Synced;
        record.CanUndo = false;
        record.BeforeTask = null;
        record.BeforeAffectedTasks.Clear();
    }

    private void MarkCompletionHistoryConflict(string operationId)
    {
        var record = _completionHistoryRecords.FirstOrDefault(item => item.OperationId == operationId);
        if (record is null) return;
        record.State = CompletionHistoryStates.Conflict;
        record.CanUndo = false;
    }

    private void SeedLegacyCompletionHistory(IEnumerable<SheetTask> tasks)
    {
        foreach (var task in tasks
            .Where(item => item.GridLastExecutedDate is not null)
            .OrderByDescending(item => item.GridLastExecutedDate)
            .Take(10))
        {
            var completedDate = task.GridLastExecutedDate!.Value;
            _completionHistoryRecords.Add(new CompletionHistoryRecord
            {
                TaskId = task.TaskId,
                CompletedDate = completedDate.Date,
                RecordedAt = new DateTimeOffset(completedDate),
                Category = task.Category,
                Type = task.Type,
                Task = task.Task,
                Remark = task.Remark,
                State = CompletionHistoryStates.Synced
            });
        }
    }

    private async void CompletionUploadTimer_Tick(object? sender, EventArgs e)
    {
        var dueTaskIds = _taskSyncQueue
            .Where(item => item.OperationType == TaskMutationTypes.Complete
                && item.State == TaskMutationStates.Pending
                && item.UploadAfter is not null
                && item.UploadAfter <= DateTimeOffset.Now)
            .Select(item => item.TaskId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var taskId in dueTaskIds)
        {
            try
            {
                await ProcessPendingMutationsAsync(taskId);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Scheduled completion upload failed for task {taskId}", ex);
            }
        }
    }

    private void RefreshTaskSyncState()
    {
        foreach (var task in _tasks)
        {
            task.SyncState = _taskSyncQueue.Any(item => item.TaskId == task.TaskId && item.State == TaskMutationStates.Conflict)
                ? TaskSyncStates.Conflict
                : _taskSyncQueue.Any(item => item.TaskId == task.TaskId) || IsTaskWaitingOnCompound(task)
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

    private static void CopyServerManagedState(SheetTask source, SheetTask target)
    {
        target.Revision = source.Revision;
        target.UpdatedAt = source.UpdatedAt;
        target.ExpiredDate = source.ExpiredDate;
        target.WarningDate = source.WarningDate;
        target.PredecessorTaskId = source.PredecessorTaskId;
        target.IsLinkedUnlocked = source.IsLinkedUnlocked;
        target.LinkedActivationDate = source.LinkedActivationDate;
        target.Paused = source.Paused;
        target.ResumeDate = source.ResumeDate;
        target.LastLifeSyncOperationId = source.LastLifeSyncOperationId;
        target.NotifyCalculatedFieldsChanged();
    }

    private static void CopyTask(SheetTask source, SheetTask target)
    {
        target.TaskId = source.TaskId;
        target.Revision = source.Revision;
        target.UpdatedAt = source.UpdatedAt;
        target.Archived = source.Archived;
        target.Level = source.Level is >= 1 and <= 5 ? source.Level : 1;
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
        target.PredecessorTaskId = source.PredecessorTaskId;
        target.IsLinkedUnlocked = source.IsLinkedUnlocked;
        target.LinkedActivationDate = source.LinkedActivationDate;
        target.Paused = source.Paused;
        target.ResumeDate = source.ResumeDate;
        target.MinorTasks = source.MinorTasks.Select(CloneMinorTask).ToList();
        target.NotifyCalculatedFieldsChanged();
    }

    private static MinorTask CloneMinorTask(MinorTask source) => new()
    {
        MinorTaskId = source.MinorTaskId,
        ParentTaskId = source.ParentTaskId,
        Name = source.Name,
        IntervalValue = source.IntervalValue,
        IntervalUnit = source.IntervalUnit,
        LatestCompletionDate = source.LatestCompletionDate,
        DueDate = source.DueDate,
        Order = source.Order,
        Archived = source.Archived
    };

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

    private static void RestoreMissingLinkedDates(SheetTask task)
    {
        if (task.GridLastExecutedDate is not DateTime anchor) return;
        if (task.ExpiredDate is null && task.ExpiredValue > 0)
        {
            task.ExpiredDate = AddTaskInterval(anchor.Date, task.ExpiredValue, task.ExpiredUnit);
        }
        if (task.WarningDate is null && task.WarningValue >= 0)
        {
            task.WarningDate = AddTaskInterval(anchor.Date, task.WarningValue, task.WarningUnit);
        }
    }

    private void RebuildTaskSummary()
    {
        ApplyTaskSummaryRows(BuildTaskSummaryRows(_tasks, DateTime.Today));
    }

    private static TaskSummaryRows BuildTaskSummaryRows(IEnumerable<SheetTask> tasks, DateTime today)
    {
        var availableTasks = tasks
            .Where(task => IsActiveViewTask(task) && !task.Completed && task.Alert)
            .ToList();
        var activeTasks = availableTasks.Where(task => !IsTaskSnoozedForToday(task)).ToList();

        var expired = activeTasks
                .Where(task => task.ExpiredDate?.Date <= today)
                .OrderBy(task => task.ExpiredDate)
                .ThenBy(task => task.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(task => task.Type, StringComparer.OrdinalIgnoreCase)
                .ThenBy(task => task.Task, StringComparer.OrdinalIgnoreCase)
                .Select(task => new TaskSummaryItem(task, "Expired / Overdue"))
                .ToList();

        var warning = activeTasks
                .Where(task => task.ExpiredDate?.Date > today && task.WarningDate?.Date <= today)
                .OrderBy(task => task.WarningDate)
                .ThenBy(task => task.ExpiredDate)
                .ThenBy(task => task.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(task => task.Type, StringComparer.OrdinalIgnoreCase)
                .ThenBy(task => task.Task, StringComparer.OrdinalIgnoreCase)
                .Select(task => new TaskSummaryItem(task, "Warning"))
                .ToList();

        var snoozed = availableTasks
                .Where(IsTaskSnoozedForToday)
                .OrderBy(task => task.SnoozeUntil)
                .ThenBy(task => task.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(task => task.Type, StringComparer.OrdinalIgnoreCase)
                .ThenBy(task => task.Task, StringComparer.OrdinalIgnoreCase)
                .Select(task => new TaskSummaryItem(task, "Snoozed"))
                .ToList();

        return new TaskSummaryRows(expired, warning, snoozed);
    }

    private void ApplyTaskSummaryRows(TaskSummaryRows rows)
    {
        ExpiredTaskSummaryItems = new ObservableCollection<TaskSummaryItem>(rows.Expired);
        WarningTaskSummaryItems = new ObservableCollection<TaskSummaryItem>(rows.Warning);
        SnoozedTaskSummaryItems = new ObservableCollection<TaskSummaryItem>(rows.Snoozed);

        if (SelectedTaskSummaryItem is not null)
        {
            var refreshedSelection = ExpiredTaskSummaryItems
                .Concat(WarningTaskSummaryItems)
                .Concat(SnoozedTaskSummaryItems)
                .FirstOrDefault(item => ReferenceEquals(item.TaskItem, SelectedTaskSummaryItem.TaskItem));
            switch (refreshedSelection?.Group)
            {
                case "Expired / Overdue":
                    SelectedExpiredTaskSummaryItem = refreshedSelection;
                    break;
                case "Warning":
                    SelectedWarningTaskSummaryItem = refreshedSelection;
                    break;
                case "Snoozed":
                    SelectedSnoozedTaskSummaryItem = refreshedSelection;
                    break;
                default:
                    SelectedTaskSummaryItem = null;
                    SelectedExpiredTaskSummaryItem = null;
                    SelectedWarningTaskSummaryItem = null;
                    SelectedSnoozedTaskSummaryItem = null;
                    break;
            }
        }

        OnPropertyChanged(nameof(HasExpiredTaskSummaryItems));
        OnPropertyChanged(nameof(HasWarningTaskSummaryItems));
        OnPropertyChanged(nameof(HasSnoozedTaskSummaryItems));
        OnPropertyChanged(nameof(TaskSummaryDisplay));
        OnPropertyChanged(nameof(DailySummaryViewDisplay));
        RefreshCommands();
    }

    private sealed record TaskSummaryRows(
        List<TaskSummaryItem> Expired,
        List<TaskSummaryItem> Warning,
        List<TaskSummaryItem> Snoozed);

    private sealed record SecondaryViewData(
        PriorityRows PriorityRows,
        TaskSummaryRows SummaryRows);

    private sealed record PriorityRows(
        List<SheetTask> Expired,
        List<SheetTask> Warning);

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

    private bool CanManageTask(SheetTask task)
    {
        return !task.Archived && !IsTaskInConflict(task) && !IsTaskWaitingOnCompound(task);
    }

    private bool IsTaskWaitingOnCompound(SheetTask task)
    {
        return _completionHistoryRecords.Any(record =>
            record.State == CompletionHistoryStates.Pending
            && record.BeforeAffectedTasks.Any(before => string.Equals(
                before.TaskId,
                task.TaskId,
                StringComparison.OrdinalIgnoreCase)));
    }

    private void ApplyTaskSort()
    {
        using (TasksView.DeferRefresh())
        {
            TasksView.SortDescriptions.Clear();
            TasksView.SortDescriptions.Add(new SortDescription(nameof(SheetTask.HierarchyOrder), ListSortDirection.Ascending));
        }
    }

    private void RebuildFilterLists()
    {
        var visibleTasks = _tasks.Where(IsTaskInSavedFilter).ToList();
        Categories = BuildFilterList(visibleTasks.Select(task => task.Category));
        Types = BuildFilterList(visibleTasks.Select(task => task.Type));
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

        if (!IsTaskInSavedFilter(task)) return false;
        if ((SelectedSavedFilter?.FilterId ?? TaskFilterIds.Default) == TaskFilterIds.Default)
        {
            return MatchesConventionalTaskFilters(task);
        }
        var root = FindRootTask(task);
        return new[] { root }.Concat(GetDescendants(root.TaskId))
            .Any(item => IsTaskInSavedFilter(item) && MatchesConventionalTaskFilters(item));
    }

    private bool MatchesConventionalTaskFilters(SheetTask task) =>
        Matches(_categoryFilter, task.Category)
        && Matches(_typeFilter, task.Type)
        && MatchesStatus(_statusFilter, task)
        && (string.IsNullOrWhiteSpace(_taskSearchText)
            || task.Task.Contains(_taskSearchText.Trim(), StringComparison.OrdinalIgnoreCase));

    private bool IsTaskInSavedFilter(SheetTask task)
    {
        if (task.Archived) return false;
        var filterId = SelectedSavedFilter?.FilterId ?? TaskFilterIds.Default;
        if (filterId == TaskFilterIds.All) return true;
        if (filterId == TaskFilterIds.Default) return IsActiveViewTask(task);
        return !task.IsEffectivelyPaused
            && !task.IsLinkedLocked
            && SelectedSavedFilter!.TaskIds.Contains(task.TaskId, StringComparer.OrdinalIgnoreCase);
    }

    private static bool Matches(string filter, string value)
    {
        return filter == AllFilter || string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsActiveViewTask(SheetTask task)
    {
        return !task.Archived && !task.IsEffectivelyPaused && !task.IsLinkedLocked;
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

    private static string ToTitleCase(string value)
    {
        var culture = CultureInfo.CurrentCulture;
        return culture.TextInfo.ToTitleCase(value.Trim().ToLower(culture));
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

    private bool CanUseNetwork() => !IsBusy;

    private bool CanOpenSecondaryView() => AreSecondaryViewsReady;

    private bool CanMutateSelectedTask()
    {
        return SelectedTask is not null
            && !SelectedTask.Archived
            && !SelectedTask.IsEffectivelyPaused
            && !SelectedTask.IsLinkedLocked
            && !IsTaskInConflict(SelectedTask)
            && !IsTaskWaitingOnCompound(SelectedTask)
            && !string.IsNullOrWhiteSpace(SelectedTask.TaskId);
    }

    private bool CanEditSelectedTask()
    {
        return SelectedTask is not null
            && !SelectedTask.Archived
            && !IsTaskInConflict(SelectedTask)
            && !IsTaskWaitingOnCompound(SelectedTask)
            && !string.IsNullOrWhiteSpace(SelectedTask.TaskId);
    }

    private bool CanPauseSelectedTask()
    {
        return CanEditSelectedTask() && SelectedTask?.IsEffectivelyPaused == false;
    }

    private bool HasPauseTargetTask() => _pauseTargetTask is not null;

    private bool CanOpenSelectedTaskSidebar()
    {
        return SelectedTask is not null && !SelectedTask.Archived;
    }

    private bool HasSelectedPriorityTask() => SelectedPriorityTask is not null;

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
            && !IsTaskWaitingOnCompound(SelectedTaskSummaryItem.TaskItem)
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
            && !IsTaskWaitingOnCompound(SelectedTaskSummaryItem.TaskItem)
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
        PauseSelectedTaskCommand.RaiseCanExecuteChanged();
        SavePauseCommand.RaiseCanExecuteChanged();
        ResumeTaskCommand.RaiseCanExecuteChanged();
        EditPausedTaskCommand.RaiseCanExecuteChanged();
        EditLinkedTaskCommand.RaiseCanExecuteChanged();
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
        ToggleAllTaskDetailsCommand.RaiseCanExecuteChanged();
        OpenPriorityDetailCommand.RaiseCanExecuteChanged();
        UndoCompletionCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

public enum MainViewKind
{
    Tasks,
    Priority,
    DailySummary,
    History
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

    public string SeverityBrush => Group switch
    {
        "Expired / Overdue" => "#B42318",
        "Snoozed" => "#2F6F9F",
        _ => "#B7791F"
    };

    public string SeverityBackground => Group switch
    {
        "Expired / Overdue" => "#FFF1F0",
        "Snoozed" => "#EEF6FC",
        _ => "#FFF8DB"
    };

    public string SeverityBorder => Group switch
    {
        "Expired / Overdue" => "#F3B4AE",
        "Snoozed" => "#A9CBE3",
        _ => "#EBCB73"
    };

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

    public Visibility AlertVisibility => TaskItem.Alert ? Visibility.Visible : Visibility.Collapsed;

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
