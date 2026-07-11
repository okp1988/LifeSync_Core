using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    private const string TrackHomeAttention = "Attention";
    private const string TrackHomeAllItems = "All Items";
    private const string TrackHomeCategories = "Categories";
    private const string TrackActionNone = "";
    private const string TrackActionAddStock = "Add Stock";
    private const string TrackActionUseStock = "Use Stock";
    private const string TrackActionPutBack = "Put Back";
    private const string TrackActionChanged = "Changed";
    private readonly JsonFileStore _fileStore = new();
    private readonly GoogleSheetClient _sheetClient = new();
    private readonly TaskTrackerAlertExportService _taskTrackerExportService = new();
    private readonly ObservableCollection<SheetTask> _tasks = [];
    private readonly List<TaskMutation> _taskSyncQueue = [];
    private readonly SemaphoreSlim _taskSyncGate = new(1, 1);
    private readonly ObservableCollection<TrackItem> _trackItems = [];
    private readonly DispatcherTimer _checkinTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private AppConfig _config = new();
    private CheckinSettings _checkinSettings = new();
    private TrackOptions _trackOptions = new();
    private TrackSettings _trackSettings = new();
    private TrackSettings _trackSettingsDraft = new();
    private SheetTask? _selectedTask;
    private TrackItem? _selectedTrackItem;
    private TrackItem? _trackItemDraft;
    private bool _isNewTrackItemDraft;
    private TrackItemHistory? _selectedTrackHistory;
    private string _categoryFilter = AllFilter;
    private string _typeFilter = AllFilter;
    private string _statusFilter = AllFilter;
    private string _dayLeftFilter = AllFilter;
    private string _trackCategoryFilter = AllFilter;
    private string _trackTypeFilter = AllFilter;
    private string _selectedTrackHomeView = TrackHomeAttention;
    private string _trackActionMode = TrackActionNone;
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
    private bool _isTrackDetailSidebarOpen;
    private bool _isTrackRecordSidebarOpen;
    private string _message = "Ready";
    private bool _isCheckinSettingsOpen;
    private bool _isTrackSettingsOpen;
    private bool _isTrackRepairOpen;
    private bool _isUpdatingCheckinAllDays;
    private DateTime _lastCheckinDisplayRefreshDate = DateTime.MinValue;
    private DateTime _lastTaskCalculatedFieldsRefreshDate = DateTime.Today;
    private string _selectedRemarkDraft = string.Empty;
    private DateTime? _completionDate = DateTime.Today;
    private decimal _trackActionQuantity = 1;
    private DateTime? _trackActionDate = DateTime.Today;
    private string _trackActionLocation = string.Empty;
    private string _trackActionRemark = string.Empty;
    private DateTime? _trackActionExpiryDate;
    private string _selectedTrackBatchId = string.Empty;
    private ExpiryAlertSetting? _selectedExpiryAlert;
    private bool _isSortingExpiryAlerts;
    private ObservableCollection<string> _categories = [AllFilter];
    private ObservableCollection<string> _types = [AllFilter];
    private ObservableCollection<string> _trackCategories = [AllFilter];
    private ObservableCollection<string> _trackCategorySuggestions = [];
    private ObservableCollection<string> _trackRemarks = [];
    private ObservableCollection<string> _trackRemarkSuggestions = [];
    private bool _isTrackCategoryDropDownOpen;
    private bool _isTrackRemarkDropDownOpen;
    private ObservableCollection<TrackStockBatch> _trackStockBatches = [];
    private ObservableCollection<TrackStockBatch> _returnableTrackBatches = [];
    private ObservableCollection<TrackRepairIssue> _trackRepairIssues = [];
    private TrackRepairIssue? _selectedTrackRepairIssue;
    private ObservableCollection<TrackCategorySummary> _trackCategorySummaries = [];
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
        TrackItemsView = CollectionViewSource.GetDefaultView(_trackItems);
        TrackItemsView.Filter = FilterTrackItem;
        ApplyDefaultSort();
        ApplyTrackDefaultSort();

        RequestTasksCommand = new RelayCommand(RequestTasksAsync, CanUseNetwork);
        MarkCompleteCommand = new RelayCommand(MarkCompleteAsync, CanMutateSelectedTask);
        ExportSelectedTaskToTaskTrackerCommand = new RelayCommand(ExportSelectedTaskToTaskTrackerAsync, CanExportSelectedTaskToTaskTracker);
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
        OpenTrackSettingsCommand = new RelayCommand(OpenTrackSettingsAsync);
        SaveTrackSettingsCommand = new RelayCommand(SaveTrackSettingsAsync);
        CancelTrackSettingsCommand = new RelayCommand(CancelTrackSettingsAsync);
        AddExpiryAlertCommand = new RelayCommand(AddExpiryAlertAsync);
        RemoveExpiryAlertCommand = new RelayCommand(RemoveExpiryAlertAsync, CanRemoveExpiryAlert);
        OpenTrackRepairCommand = new RelayCommand(OpenTrackRepairAsync);
        AcceptTrackRepairIssueCommand = new RelayCommand(AcceptTrackRepairIssueAsync, CanAcceptTrackRepairIssue);
        AcceptAllTrackRepairIssuesCommand = new RelayCommand(AcceptAllTrackRepairIssuesAsync, HasSuggestedTrackRepairIssues);
        SkipTrackRepairIssueCommand = new RelayCommand(SkipTrackRepairIssueAsync, CanSkipTrackRepairIssue);
        SaveTrackRepairsCommand = new RelayCommand(SaveTrackRepairsAsync, HasAcceptedTrackRepairIssues);
        CancelTrackRepairCommand = new RelayCommand(CancelTrackRepairAsync);
        NewTrackItemCommand = new RelayCommand(NewTrackItemAsync);
        SaveTrackItemsCommand = new RelayCommand(SaveTrackItemsAsync);
        RemoveTrackItemCommand = new RelayCommand(RemoveTrackItemAsync, CanMutateSelectedTrackItem);
        RemoveTrackHistoryCommand = new RelayCommand(RemoveTrackHistoryAsync, CanRemoveTrackHistory);
        ExportSelectedTrackItemToTaskTrackerCommand = new RelayCommand(ExportSelectedTrackItemToTaskTrackerAsync, CanExportSelectedTrackItemToTaskTracker);
        AddTrackOwnedCommand = new RelayCommand(AddTrackOwnedAsync, CanAddTrackOwned);
        UseTrackQuantityCommand = new RelayCommand(UseTrackQuantityAsync, CanUseTrackQuantity);
        ReturnTrackQuantityCommand = new RelayCommand(ReturnTrackQuantityAsync, CanReturnTrackQuantity);
        RecordTrackChangeCommand = new RelayCommand(RecordTrackChangeAsync, CanRecordTrackChange);
        OpenAddStockActionCommand = new RelayCommand(OpenAddStockActionAsync, CanMutateSelectedQuantityTrackItem);
        OpenUseStockActionCommand = new RelayCommand(OpenUseStockActionAsync, CanMutateSelectedQuantityTrackItem);
        OpenPutBackActionCommand = new RelayCommand(OpenPutBackActionAsync, CanReturnAnyTrackQuantity);
        OpenChangedActionCommand = new RelayCommand(OpenChangedActionAsync, CanMutateSelectedChangeCycleTrackItem);
        SaveTrackActionCommand = new RelayCommand(SaveTrackActionAsync, CanSaveTrackAction);
        CancelTrackActionCommand = new RelayCommand(CancelTrackActionAsync);
        EditTrackItemCommand = new RelayCommand(EditTrackItemAsync, CanMutateSelectedTrackItem);
        OpenTrackRecordSidebarCommand = new RelayCommand(OpenTrackRecordSidebarAsync, CanMutateSelectedTrackItem);
        CloseTrackSidebarCommand = new RelayCommand(CloseTrackSidebarAsync);
        CheckinSettingsDraft = new ObservableCollection<CheckinDaySetting>();
        _checkinTimer.Tick += CheckinTimer_Tick;
    }

    public ICollectionView TasksView { get; }
    public ICollectionView TrackItemsView { get; }
    public ObservableCollection<TrackItem> TrackItems => _trackItems;
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
    public RelayCommand ExportSelectedTaskToTaskTrackerCommand { get; }
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
    public RelayCommand OpenTrackSettingsCommand { get; }
    public RelayCommand SaveTrackSettingsCommand { get; }
    public RelayCommand CancelTrackSettingsCommand { get; }
    public RelayCommand AddExpiryAlertCommand { get; }
    public RelayCommand RemoveExpiryAlertCommand { get; }
    public RelayCommand OpenTrackRepairCommand { get; }
    public RelayCommand AcceptTrackRepairIssueCommand { get; }
    public RelayCommand AcceptAllTrackRepairIssuesCommand { get; }
    public RelayCommand SkipTrackRepairIssueCommand { get; }
    public RelayCommand SaveTrackRepairsCommand { get; }
    public RelayCommand CancelTrackRepairCommand { get; }
    public RelayCommand NewTrackItemCommand { get; }
    public RelayCommand SaveTrackItemsCommand { get; }
    public RelayCommand RemoveTrackItemCommand { get; }
    public RelayCommand RemoveTrackHistoryCommand { get; }
    public RelayCommand ExportSelectedTrackItemToTaskTrackerCommand { get; }
    public RelayCommand AddTrackOwnedCommand { get; }
    public RelayCommand UseTrackQuantityCommand { get; }
    public RelayCommand ReturnTrackQuantityCommand { get; }
    public RelayCommand RecordTrackChangeCommand { get; }
    public RelayCommand OpenAddStockActionCommand { get; }
    public RelayCommand OpenUseStockActionCommand { get; }
    public RelayCommand OpenPutBackActionCommand { get; }
    public RelayCommand OpenChangedActionCommand { get; }
    public RelayCommand SaveTrackActionCommand { get; }
    public RelayCommand CancelTrackActionCommand { get; }
    public RelayCommand EditTrackItemCommand { get; }
    public RelayCommand OpenTrackRecordSidebarCommand { get; }
    public RelayCommand CloseTrackSidebarCommand { get; }

    public string[] TrackTypes { get; } = [TrackItemTypes.QuantityUsage, TrackItemTypes.ChangeCycle];
    public string[] TrackTypeFilters { get; } = [AllFilter, TrackItemTypes.QuantityUsage, TrackItemTypes.ChangeCycle];
    public string[] TrackHomeViews { get; } = [TrackHomeAttention, TrackHomeAllItems, TrackHomeCategories];
    public string[] TaskTrackerExportModeChoices { get; } = [Models.TaskTrackerExportModes.Off, Models.TaskTrackerExportModes.Prompt, Models.TaskTrackerExportModes.Auto];
    public string[] ChangeUnits { get; } = [ChangeIntervalUnits.Days, ChangeIntervalUnits.Weeks, ChangeIntervalUnits.Months, ChangeIntervalUnits.Years];
    public string[] TrackSettingColorChoices { get; } = [TrackSettingColors.Yellow, TrackSettingColors.Orange, TrackSettingColors.Red, TrackSettingColors.DarkGray, TrackSettingColors.Green, TrackSettingColors.Blue];
    public string[] ExpiryAlertUnitChoices { get; } = [ExpiryAlertUnits.Day, ExpiryAlertUnits.Week, ExpiryAlertUnits.Month, ExpiryAlertUnits.Year];

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

    public bool IsTrackSettingsOpen
    {
        get => _isTrackSettingsOpen;
        private set
        {
            if (_isTrackSettingsOpen == value)
            {
                return;
            }

            _isTrackSettingsOpen = value;
            OnPropertyChanged();
        }
    }

    public bool IsTrackRepairOpen
    {
        get => _isTrackRepairOpen;
        private set
        {
            if (_isTrackRepairOpen == value)
            {
                return;
            }

            _isTrackRepairOpen = value;
            OnPropertyChanged();
        }
    }

    public TrackSettings TrackSettingsDraft
    {
        get => _trackSettingsDraft;
        private set
        {
            DetachExpiryAlertHandlers(_trackSettingsDraft);
            _trackSettingsDraft = value;
            AttachExpiryAlertHandlers(_trackSettingsDraft);
            SortExpiryAlerts(_trackSettingsDraft.ExpiryAlerts);
            OnPropertyChanged();
        }
    }

    public ObservableCollection<TrackRepairIssue> TrackRepairIssues
    {
        get => _trackRepairIssues;
        private set
        {
            _trackRepairIssues = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasTrackRepairIssues));
            OnPropertyChanged(nameof(TrackRepairSummary));
            OnPropertyChanged(nameof(MaintenanceDisplay));
            RefreshCommands();
        }
    }

    public TrackRepairIssue? SelectedTrackRepairIssue
    {
        get => _selectedTrackRepairIssue;
        set
        {
            if (_selectedTrackRepairIssue == value)
            {
                return;
            }

            _selectedTrackRepairIssue = value;
            OnPropertyChanged();
            RefreshCommands();
        }
    }

    public bool HasTrackRepairIssues => TrackRepairIssues.Count > 0;

    public string TrackRepairSummary
    {
        get
        {
            if (TrackRepairIssues.Count == 0)
            {
                return "No old stock records need repair.";
            }

            var accepted = TrackRepairIssues.Count(issue => issue.IsAccepted);
            var skipped = TrackRepairIssues.Count(issue => issue.IsSkipped);
            return $"{TrackRepairIssues.Count} old stock record(s) need repair. {accepted} accepted, {skipped} skipped.";
        }
    }

    public ExpiryAlertSetting? SelectedExpiryAlert
    {
        get => _selectedExpiryAlert;
        set
        {
            if (_selectedExpiryAlert == value)
            {
                return;
            }

            _selectedExpiryAlert = value;
            OnPropertyChanged();
            RemoveExpiryAlertCommand.RaiseCanExecuteChanged();
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

    public ObservableCollection<string> TrackRemarks
    {
        get => _trackRemarks;
        private set
        {
            _trackRemarks = value;
            OnPropertyChanged();
            UpdateTrackRemarkSuggestions(TrackActionRemark);
        }
    }

    public ObservableCollection<string> TrackCategories
    {
        get => _trackCategories;
        private set
        {
            _trackCategories = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TrackCategoryChoices));
        }
    }

    public IEnumerable<string> TrackCategoryChoices => TrackCategories.Where(value => value != AllFilter);

    public ObservableCollection<string> TrackCategorySuggestions
    {
        get => _trackCategorySuggestions;
        private set
        {
            _trackCategorySuggestions = value;
            OnPropertyChanged();
        }
    }

    public bool IsTrackCategoryDropDownOpen
    {
        get => _isTrackCategoryDropDownOpen;
        set
        {
            if (_isTrackCategoryDropDownOpen == value)
            {
                return;
            }

            _isTrackCategoryDropDownOpen = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> TrackRemarkSuggestions
    {
        get => _trackRemarkSuggestions;
        private set
        {
            _trackRemarkSuggestions = value;
            OnPropertyChanged();
        }
    }

    public bool IsTrackRemarkDropDownOpen
    {
        get => _isTrackRemarkDropDownOpen;
        set
        {
            if (_isTrackRemarkDropDownOpen == value)
            {
                return;
            }

            _isTrackRemarkDropDownOpen = value;
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
            OnPropertyChanged(nameof(CanPromptExportSelectedTaskToTaskTracker));
            RefreshCommands();
        }
    }

    public TrackItem? SelectedTrackItem
    {
        get => _selectedTrackItem;
        set
        {
            if (_selectedTrackItem == value)
            {
                return;
            }

            if (_selectedTrackItem is not null)
            {
                _selectedTrackItem.PropertyChanged -= SelectedTrackItem_PropertyChanged;
            }

            _selectedTrackItem = value;
            if (_selectedTrackItem is not null)
            {
                _selectedTrackItem.PropertyChanged += SelectedTrackItem_PropertyChanged;
            }

            TrackActionQuantity = 1;
            TrackActionDate = DateTime.Today;
            TrackActionLocation = string.Empty;
            TrackActionRemark = string.Empty;
            TrackActionExpiryDate = null;
            SelectedTrackHistory = null;
            if (value is not null)
            {
                SortTrackHistoryDescending(value);
            }

            RefreshTrackStockBatches();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedQuantityTrackItem));
            OnPropertyChanged(nameof(HasSelectedChangeCycleTrackItem));
            OnPropertyChanged(nameof(HasSelectedTrackItemExpiry));
            OnPropertyChanged(nameof(SelectedTrackItemStatus));
            OnPropertyChanged(nameof(SelectedTrackItemSummary));
            OnPropertyChanged(nameof(CanPromptExportSelectedTrackItemToTaskTracker));
            IsTrackRecordSidebarOpen = value is not null && !IsTrackDetailSidebarOpen;
            RefreshCommands();
        }
    }

    public TrackItem? TrackItemDraft
    {
        get => _trackItemDraft;
        private set
        {
            if (_trackItemDraft is not null)
            {
                _trackItemDraft.PropertyChanged -= TrackItemDraft_PropertyChanged;
            }

            _trackItemDraft = value;
            if (_trackItemDraft is not null)
            {
                _trackItemDraft.PropertyChanged += TrackItemDraft_PropertyChanged;
            }

            UpdateTrackCategorySuggestions(value?.Category);
            OnPropertyChanged();
        }
    }

    public TrackItemHistory? SelectedTrackHistory
    {
        get => _selectedTrackHistory;
        set
        {
            if (_selectedTrackHistory == value)
            {
                return;
            }

            _selectedTrackHistory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedTrackHistory));
            RefreshCommands();
        }
    }

    public bool HasSelectedTrackHistory => SelectedTrackHistory is not null;

    public bool IsTaskTrackerTaskPromptMode => TaskTrackerTaskExportMode == Models.TaskTrackerExportModes.Prompt;

    public bool IsTaskTrackerTrackPromptMode => TaskTrackerTrackExportMode == Models.TaskTrackerExportModes.Prompt;

    public bool CanPromptExportSelectedTaskToTaskTracker => IsTaskTrackerTaskPromptMode && CanExportSelectedTaskToTaskTracker();

    public bool CanPromptExportSelectedTrackItemToTaskTracker => IsTaskTrackerTrackPromptMode && CanExportSelectedTrackItemToTaskTracker();

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

    public string TrackCategoryFilter
    {
        get => _trackCategoryFilter;
        set
        {
            value = NormalizeFilter(value);
            if (_trackCategoryFilter == value)
            {
                return;
            }

            _trackCategoryFilter = value;
            OnPropertyChanged();
            ApplyTrackFilters();
        }
    }

    public string TrackTypeFilter
    {
        get => _trackTypeFilter;
        set
        {
            value = NormalizeFilter(value);
            if (_trackTypeFilter == value)
            {
                return;
            }

            _trackTypeFilter = value;
            OnPropertyChanged();
            ApplyTrackFilters();
        }
    }

    public string SelectedTrackHomeView
    {
        get => _selectedTrackHomeView;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                value = TrackHomeAttention;
            }

            if (_selectedTrackHomeView == value)
            {
                return;
            }

            _selectedTrackHomeView = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTrackItemListView));
            OnPropertyChanged(nameof(IsTrackCategoryHomeView));
            OnPropertyChanged(nameof(TrackEmptyViewMessage));
            ApplyTrackFilters();
        }
    }

    public bool IsTrackItemListView => SelectedTrackHomeView != TrackHomeCategories;

    public bool IsTrackCategoryHomeView => SelectedTrackHomeView == TrackHomeCategories;

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

    public bool IsTrackDetailSidebarOpen
    {
        get => _isTrackDetailSidebarOpen;
        private set
        {
            if (_isTrackDetailSidebarOpen == value)
            {
                return;
            }

            _isTrackDetailSidebarOpen = value;
            OnPropertyChanged();
        }
    }

    public bool IsTrackRecordSidebarOpen
    {
        get => _isTrackRecordSidebarOpen;
        private set
        {
            if (_isTrackRecordSidebarOpen == value)
            {
                return;
            }

            _isTrackRecordSidebarOpen = value;
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

    public decimal TrackActionQuantity
    {
        get => _trackActionQuantity;
        set
        {
            var normalizedValue = Math.Max(0, value);
            if (_trackActionQuantity == normalizedValue)
            {
                return;
            }

            _trackActionQuantity = normalizedValue;
            OnPropertyChanged();
            RefreshCommands();
        }
    }

    public DateTime? TrackActionDate
    {
        get => _trackActionDate;
        set
        {
            if (_trackActionDate == value)
            {
                return;
            }

            _trackActionDate = value;
            OnPropertyChanged();
        }
    }

    public string TrackActionLocation
    {
        get => _trackActionLocation;
        set
        {
            if (_trackActionLocation == value)
            {
                return;
            }

            _trackActionLocation = value;
            OnPropertyChanged();
        }
    }

    public string TrackActionRemark
    {
        get => _trackActionRemark;
        set
        {
            if (_trackActionRemark == value)
            {
                return;
            }

            _trackActionRemark = value;
            OnPropertyChanged();
            UpdateTrackRemarkSuggestions(value);
        }
    }

    public DateTime? TrackActionExpiryDate
    {
        get => _trackActionExpiryDate;
        set
        {
            if (_trackActionExpiryDate == value)
            {
                return;
            }

            _trackActionExpiryDate = value;
            OnPropertyChanged();
            RefreshCommands();
        }
    }

    public string SelectedTrackBatchId
    {
        get => _selectedTrackBatchId;
        set
        {
            if (_selectedTrackBatchId == value)
            {
                return;
            }

            _selectedTrackBatchId = value;
            OnPropertyChanged();
            RefreshCommands();
        }
    }

    public ObservableCollection<TrackStockBatch> TrackStockBatches
    {
        get => _trackStockBatches;
        private set
        {
            _trackStockBatches = value;
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

    public string TaskTrackerTaskExportMode
    {
        get => NormalizeTaskTrackerExportMode(_config.TaskTrackerTaskExportMode);
        set
        {
            value = NormalizeTaskTrackerExportMode(value);
            if (_config.TaskTrackerTaskExportMode == value)
            {
                return;
            }

            _config.TaskTrackerTaskExportMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTaskTrackerTaskPromptMode));
            OnPropertyChanged(nameof(CanPromptExportSelectedTaskToTaskTracker));
            RefreshCommands();
            _ = SaveConfigAsync();
        }
    }

    public string TaskTrackerTrackExportMode
    {
        get => NormalizeTaskTrackerExportMode(_config.TaskTrackerTrackExportMode);
        set
        {
            value = NormalizeTaskTrackerExportMode(value);
            if (_config.TaskTrackerTrackExportMode == value)
            {
                return;
            }

            _config.TaskTrackerTrackExportMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTaskTrackerTrackPromptMode));
            OnPropertyChanged(nameof(CanPromptExportSelectedTrackItemToTaskTracker));
            RefreshCommands();
            _ = SaveConfigAsync();
        }
    }

    public string TaskTrackerExportPath
    {
        get => string.IsNullOrWhiteSpace(_config.TaskTrackerExportPath)
            ? AppPaths.TaskTrackerExportPath
            : _config.TaskTrackerExportPath;
        set
        {
            if (_config.TaskTrackerExportPath == value)
            {
                return;
            }

            _config.TaskTrackerExportPath = value;
            OnPropertyChanged();
            _ = SaveConfigAsync();
        }
    }

    public string TaskTrackerExePath
    {
        get => _config.TaskTrackerExePath;
        set
        {
            if (_config.TaskTrackerExePath == value)
            {
                return;
            }

            _config.TaskTrackerExePath = value;
            OnPropertyChanged();
            _ = SaveConfigAsync();
        }
    }

    public ObservableCollection<TrackStockBatch> ReturnableTrackBatches
    {
        get => _returnableTrackBatches;
        private set
        {
            _returnableTrackBatches = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsReturnBatchChoiceVisible));
        }
    }

    public bool HasSelectedQuantityTrackItem => SelectedTrackItem?.IsQuantityUsage == true;

    public bool HasSelectedChangeCycleTrackItem => SelectedTrackItem?.IsChangeCycle == true;

    public bool HasSelectedTrackItemExpiry => SelectedTrackItem?.HasExpiryDate == true;

    public ObservableCollection<TrackCategorySummary> TrackCategorySummaries
    {
        get => _trackCategorySummaries;
        private set
        {
            _trackCategorySummaries = value;
            OnPropertyChanged();
        }
    }

    public string MaintenanceDisplay => HasTrackRepairIssues
        ? $"Maintenance ({TrackRepairIssues.Count})"
        : "Maintenance";

    public string SelectedTrackItemStatus => SelectedTrackItem?.AlertStatus switch
    {
        null or "" => SelectedTrackItem?.IsQuantityUsage == true ? SelectedTrackItem.StockAlertLevel : string.Empty,
        var status => status
    };

    public string SelectedTrackItemSummary
    {
        get
        {
            if (SelectedTrackItem is null)
            {
                return string.Empty;
            }

            if (SelectedTrackItem.IsChangeCycle)
            {
                var nextDate = SelectedTrackItem.NextChangeDate?.ToString("dd MMM yyyy") ?? "not set";
                return $"Replace every {SelectedTrackItem.ChangeEvery} {SelectedTrackItem.ChangeUnit.ToLowerInvariant()}; next due {nextDate}.";
            }

            var expiry = SelectedTrackItem.ExpiryDate?.ToString("dd MMM yyyy");
            return expiry is null
                ? $"Owned {SelectedTrackItem.TotalQuantity}, used {SelectedTrackItem.UsedQuantity}, left {SelectedTrackItem.LeftQuantity}."
                : $"Owned {SelectedTrackItem.TotalQuantity}, used {SelectedTrackItem.UsedQuantity}, left {SelectedTrackItem.LeftQuantity}; nearest expiry {expiry}.";
        }
    }

    public bool IsTrackActionEditorOpen => !string.IsNullOrWhiteSpace(_trackActionMode);

    public string TrackActionTitle => _trackActionMode;

    public bool IsAddStockAction => _trackActionMode == TrackActionAddStock;

    public bool IsUseStockAction => _trackActionMode == TrackActionUseStock;

    public bool IsPutBackAction => _trackActionMode == TrackActionPutBack;

    public bool IsChangedAction => _trackActionMode == TrackActionChanged;

    public bool IsQuantityAction => IsAddStockAction || IsUseStockAction || IsPutBackAction;

    public bool IsTrackActionLocationVisible => IsAddStockAction || IsUseStockAction || IsPutBackAction;

    public bool IsTrackActionExpiryVisible => IsAddStockAction && SelectedTrackItem?.HasExpiryDate == true;

    public bool IsReturnBatchChoiceVisible => IsPutBackAction && ReturnableTrackBatches.Count > 1;

    public string TrackEmptyViewMessage => SelectedTrackHomeView == TrackHomeAttention
        ? "No inventory items need attention."
        : "No inventory items match the current filters.";

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
            NormalizeTaskTrackerConfig();
            _config.TaskTrackerTaskExportMode = Models.TaskTrackerExportModes.Off;
            _config.TaskTrackerTrackExportMode = Models.TaskTrackerExportModes.Off;

            AppLogger.PruneOldFiles(_config.LogRetentionDays);
            OnPropertyChanged(nameof(GoogleAppsScriptUrl));
            OnPropertyChanged(nameof(ApiKey));
            OnPropertyChanged(nameof(LogRetentionDays));
            OnPropertyChanged(nameof(TaskTrackerTaskExportMode));
            OnPropertyChanged(nameof(TaskTrackerTrackExportMode));
            OnPropertyChanged(nameof(TaskTrackerExportPath));
            OnPropertyChanged(nameof(TaskTrackerExePath));

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

    public void ClearTrackSelection()
    {
        SelectedTrackHistory = null;
        SelectedTrackItem = null;
        IsTrackDetailSidebarOpen = false;
        IsTrackRecordSidebarOpen = false;
        TrackItemDraft = null;
        _isNewTrackItemDraft = false;
    }

    public void ClearSelectedTrackHistory()
    {
        SelectedTrackHistory = null;
    }

    public void CloseAllPopupsAndSidebars()
    {
        ResetCheckinSettingsDraft();
        IsCheckinSettingsOpen = false;
        TrackSettingsDraft = NormalizeTrackSettings(CloneTrackSettings(_trackSettings));
        SelectedExpiryAlert = TrackSettingsDraft.ExpiryAlerts.FirstOrDefault();
        IsTrackSettingsOpen = false;
        IsTrackRepairOpen = false;
        IsTaskSummaryOpen = false;
        IsTaskEditorOpen = false;
        IsTaskConflictOpen = false;
        SelectedTaskSummaryItem = null;
        SelectedExpiredTaskSummaryItem = null;
        SelectedWarningTaskSummaryItem = null;
        ClearTaskSelection();
        ClearTrackSelection();
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

    public void ApplyTrackLeftSort(ListSortDirection? direction)
    {
        if (direction is null)
        {
            ApplyTrackDefaultSort();
        }
        else
        {
            TrackItemsView.SortDescriptions.Clear();
            TrackItemsView.SortDescriptions.Add(new SortDescription(nameof(TrackItem.LeftQuantity), direction.Value));
        }

        TrackItemsView.Refresh();
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

    private async Task NewTrackItemAsync()
    {
        TrackItemDraft = new TrackItem
        {
            Name = "New Item",
            TrackType = TrackItemTypes.QuantityUsage,
            ChangeEvery = 12,
            ChangeUnit = ChangeIntervalUnits.Months
        };

        _isNewTrackItemDraft = true;
        IsTrackRecordSidebarOpen = false;
        IsTrackDetailSidebarOpen = true;
        await Task.CompletedTask;
    }

    private async Task SaveTrackItemsAsync()
    {
        if (TrackItemDraft is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(TrackItemDraft.Name))
        {
            Message = "Tracked item name is required.";
            return;
        }

        if (_isNewTrackItemDraft)
        {
            _trackItems.Add(CloneTrackItem(TrackItemDraft));
            SelectedTrackItem = _trackItems[^1];
        }
        else if (SelectedTrackItem is not null)
        {
            CopyTrackItem(TrackItemDraft, SelectedTrackItem);
        }

        TrackItemsView.Refresh();
        IsTrackDetailSidebarOpen = false;
        TrackItemDraft = null;
        _isNewTrackItemDraft = false;
        await SaveNewTrackCategoryAsync(SelectedTrackItem?.Category);
        RebuildTrackCategoryFilter();
        await SaveTrackItemsCoreAsync($"Saved {_trackItems.Count} tracked item(s).");
    }

    private async Task RemoveTrackItemAsync()
    {
        if (SelectedTrackItem is null)
        {
            return;
        }

        var item = SelectedTrackItem;
        var result = MessageBox.Show(
            $"Remove tracked item '{item.Name}' and all its records?",
            "Confirm Remove",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _trackItems.Remove(item);
        SelectedTrackItem = null;
        IsTrackDetailSidebarOpen = false;
        IsTrackRecordSidebarOpen = false;
        RebuildTrackCategoryFilter();
        await SaveTrackItemsCoreAsync($"Removed tracked item: {item.Name}.");
    }

    private async Task RemoveTrackHistoryAsync()
    {
        if (SelectedTrackItem is null || SelectedTrackHistory is null)
        {
            return;
        }

        var result = MessageBox.Show(
            "Remove selected record from this item?",
            "Confirm Remove Record",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        SelectedTrackItem.History.Remove(SelectedTrackHistory);
        SelectedTrackHistory = null;
        SelectedTrackItem.RecalculateQuantitiesFromHistory();
        SelectedTrackItem.NotifyCalculatedFieldsChanged();
        RefreshTrackStockBatches();
        TrackItemsView.Refresh();
        await SaveTrackItemsCoreAsync($"Removed record for {SelectedTrackItem.Name}.");
    }

    private async Task ExportSelectedTrackItemToTaskTrackerAsync()
    {
        if (SelectedTrackItem is null)
        {
            return;
        }

        var requests = CreateTrackTaskTrackerRequests(SelectedTrackItem).ToList();
        if (requests.Count == 0)
        {
            Message = "Selected inventory item has no Task Tracker alert request ready.";
            return;
        }

        var exportedCount = 0;
        foreach (var request in requests)
        {
            if (await ExportTaskTrackerRequestAsync(request))
            {
                exportedCount++;
            }
        }

        Message = exportedCount == 0
            ? "Task Tracker inbox already has this inventory alert request."
            : $"Queued {exportedCount} Task Tracker inventory request(s).";
        if (exportedCount > 0)
        {
            TryOpenTaskTracker();
        }
    }

    private async Task AddTrackOwnedAsync()
    {
        if (SelectedTrackItem is null || TrackActionQuantity <= 0)
        {
            return;
        }

        if (SelectedTrackItem.HasExpiryDate && TrackActionExpiryDate is null)
        {
            Message = "Select expiry date before adding stock for this item.";
            return;
        }

        await SaveNewTrackRemarkAsync(TrackActionRemark);
        var batchNumber = SelectedTrackItem.History.Count(record => record.Action == "Owned") + 1;
        var batchId = $"batch-{batchNumber:000}-{Guid.NewGuid():N}";
        AddTrackHistory(
            SelectedTrackItem,
            "Owned",
            TrackActionQuantity,
            batchId: batchId,
            expiryDate: SelectedTrackItem.HasExpiryDate ? TrackActionExpiryDate : null);
        SelectedTrackItem.NotifyCalculatedFieldsChanged();
        RefreshTrackStockBatches();
        TrackItemsView.Refresh();
        await SaveTrackItemsCoreAsync($"Added {TrackActionQuantity} owned for {SelectedTrackItem.Name}.");
    }

    private async Task UseTrackQuantityAsync()
    {
        if (SelectedTrackItem is null || TrackActionQuantity <= 0)
        {
            return;
        }

        if (!SelectedTrackItem.IsQuantityUsage)
        {
            Message = "Use Stock is only available for quantity items.";
            return;
        }

        var allocations = AllocateStockBatches(TrackActionQuantity).ToList();
        if (allocations.Sum(allocation => allocation.Quantity) < TrackActionQuantity)
        {
            Message = $"Cannot use {TrackActionQuantity}; only {SelectedTrackItem.LeftQuantity} stock left.";
            return;
        }

        SelectedTrackItem.StartUseDate ??= TrackActionDate ?? DateTime.Today;
        await SaveNewTrackRemarkAsync(TrackActionRemark);
        var actionQuantity = TrackActionQuantity;
        foreach (var allocation in allocations)
        {
            AddTrackHistory(
                SelectedTrackItem,
                "Used",
                allocation.Quantity,
                sourceBatchId: allocation.Batch.BatchId,
                resetActionFields: false);
        }

        ResetTrackActionFields();
        SelectedTrackItem.NotifyCalculatedFieldsChanged();
        RefreshTrackStockBatches();
        TrackItemsView.Refresh();
        await SaveTrackItemsCoreAsync($"Recorded {actionQuantity} used for {SelectedTrackItem.Name}.");
    }

    private async Task ReturnTrackQuantityAsync()
    {
        if (SelectedTrackItem is null || TrackActionQuantity <= 0)
        {
            return;
        }

        if (!SelectedTrackItem.IsQuantityUsage)
        {
            Message = "Put Back is only available for quantity items.";
            return;
        }

        var selectedBatch = TrackStockBatches
            .Where(batch => batch.UsedQuantity - batch.ReturnedQuantity >= TrackActionQuantity)
            .OrderByDescending(batch => batch.OwnedDate)
            .ThenBy(batch => batch.Display)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(SelectedTrackBatchId))
        {
            selectedBatch = ReturnableTrackBatches.FirstOrDefault(batch => batch.BatchId == SelectedTrackBatchId);
        }

        var usedFromBatch = selectedBatch is null ? 0 : selectedBatch.UsedQuantity - selectedBatch.ReturnedQuantity;
        if (selectedBatch is null || TrackActionQuantity > usedFromBatch)
        {
            Message = $"Cannot put back {TrackActionQuantity}; only {usedFromBatch} returnable stock found.";
            return;
        }

        await SaveNewTrackRemarkAsync(TrackActionRemark);
        var actionQuantity = TrackActionQuantity;
        AddTrackHistory(SelectedTrackItem, "Put Back", TrackActionQuantity, sourceBatchId: selectedBatch.BatchId);
        SelectedTrackItem.NotifyCalculatedFieldsChanged();
        RefreshTrackStockBatches();
        TrackItemsView.Refresh();
        await SaveTrackItemsCoreAsync($"Put back {actionQuantity} for {SelectedTrackItem.Name}.");
    }

    private async Task RecordTrackChangeAsync()
    {
        if (SelectedTrackItem is null)
        {
            return;
        }

        var changedDate = TrackActionDate ?? DateTime.Today;
        SelectedTrackItem.StartUseDate = changedDate;
        await SaveNewTrackRemarkAsync(TrackActionRemark);
        AddTrackHistory(SelectedTrackItem, "Changed", 1);
        SelectedTrackItem.NotifyCalculatedFieldsChanged();
        RefreshTrackStockBatches();
        TrackItemsView.Refresh();
        await SaveTrackItemsCoreAsync($"Recorded change for {SelectedTrackItem.Name}.");
    }

    private Task EditTrackItemAsync()
    {
        if (SelectedTrackItem is not null)
        {
            TrackItemDraft = CloneTrackItem(SelectedTrackItem);
            _isNewTrackItemDraft = false;
            IsTrackRecordSidebarOpen = false;
            IsTrackDetailSidebarOpen = true;
        }

        return Task.CompletedTask;
    }

    private Task OpenTrackRecordSidebarAsync()
    {
        if (SelectedTrackItem is not null)
        {
            IsTrackDetailSidebarOpen = false;
            IsTrackRecordSidebarOpen = true;
            ClearTrackActionMode();
            UpdateTrackRemarkSuggestions(TrackActionRemark);
        }

        return Task.CompletedTask;
    }

    private Task OpenAddStockActionAsync()
    {
        SetTrackActionMode(TrackActionAddStock);
        return Task.CompletedTask;
    }

    private Task OpenUseStockActionAsync()
    {
        SetTrackActionMode(TrackActionUseStock);
        return Task.CompletedTask;
    }

    private Task OpenPutBackActionAsync()
    {
        SetTrackActionMode(TrackActionPutBack);
        SelectedTrackBatchId = ReturnableTrackBatches.FirstOrDefault()?.BatchId ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task OpenChangedActionAsync()
    {
        SetTrackActionMode(TrackActionChanged);
        return Task.CompletedTask;
    }

    private async Task SaveTrackActionAsync()
    {
        switch (_trackActionMode)
        {
            case TrackActionAddStock:
                await AddTrackOwnedAsync();
                break;
            case TrackActionUseStock:
                await UseTrackQuantityAsync();
                break;
            case TrackActionPutBack:
                await ReturnTrackQuantityAsync();
                break;
            case TrackActionChanged:
                await RecordTrackChangeAsync();
                break;
        }

        ClearTrackActionMode();
    }

    private Task CancelTrackActionAsync()
    {
        ClearTrackActionMode();
        ResetTrackActionFields();
        return Task.CompletedTask;
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

    private Task OpenTrackSettingsAsync()
    {
        TrackSettingsDraft = NormalizeTrackSettings(CloneTrackSettings(_trackSettings));
        SelectedExpiryAlert = TrackSettingsDraft.ExpiryAlerts.FirstOrDefault();
        IsTrackSettingsOpen = true;
        return Task.CompletedTask;
    }

    private async Task SaveTrackSettingsAsync()
    {
        _trackSettings = NormalizeTrackSettings(CloneTrackSettings(TrackSettingsDraft));
        TrackItem.ConfigureAlerts(_trackSettings);
        await _fileStore.SaveTrackSettingsAsync(_trackSettings);
        foreach (var item in _trackItems)
        {
            item.NotifyCalculatedFieldsChanged();
        }

        TrackItemsView.Refresh();
        IsTrackSettingsOpen = false;
        Message = "Saved track highlight and expiry alert settings.";
    }

    private Task CancelTrackSettingsAsync()
    {
        TrackSettingsDraft = NormalizeTrackSettings(CloneTrackSettings(_trackSettings));
        SelectedExpiryAlert = TrackSettingsDraft.ExpiryAlerts.FirstOrDefault();
        IsTrackSettingsOpen = false;
        return Task.CompletedTask;
    }

    private Task AddExpiryAlertAsync()
    {
        var alert = new ExpiryAlertSetting
        {
            Amount = 1,
            Unit = ExpiryAlertUnits.Month,
            Color = TrackSettingColors.Yellow
        };

        TrackSettingsDraft.ExpiryAlerts.Add(alert);
        SortExpiryAlerts(TrackSettingsDraft.ExpiryAlerts);
        SelectedExpiryAlert = alert;
        return Task.CompletedTask;
    }

    private Task RemoveExpiryAlertAsync()
    {
        if (SelectedExpiryAlert is null)
        {
            return Task.CompletedTask;
        }

        var removedIndex = TrackSettingsDraft.ExpiryAlerts.IndexOf(SelectedExpiryAlert);
        TrackSettingsDraft.ExpiryAlerts.Remove(SelectedExpiryAlert);
        SelectedExpiryAlert = TrackSettingsDraft.ExpiryAlerts.Count == 0
            ? null
            : TrackSettingsDraft.ExpiryAlerts[Math.Clamp(removedIndex, 0, TrackSettingsDraft.ExpiryAlerts.Count - 1)];
        return Task.CompletedTask;
    }

    private bool CanRemoveExpiryAlert()
    {
        return SelectedExpiryAlert is not null;
    }

    private Task OpenTrackRepairAsync()
    {
        RebuildTrackRepairIssues();
        SelectedTrackRepairIssue = TrackRepairIssues.FirstOrDefault();
        IsTrackRepairOpen = true;
        return Task.CompletedTask;
    }

    private Task AcceptTrackRepairIssueAsync()
    {
        if (SelectedTrackRepairIssue is null || !SelectedTrackRepairIssue.HasSuggestion)
        {
            return Task.CompletedTask;
        }

        SelectedTrackRepairIssue.Accept();
        OnPropertyChanged(nameof(TrackRepairSummary));
        RefreshCommands();
        return Task.CompletedTask;
    }

    private Task AcceptAllTrackRepairIssuesAsync()
    {
        foreach (var issue in TrackRepairIssues.Where(issue => issue.HasSuggestion))
        {
            issue.Accept();
        }

        OnPropertyChanged(nameof(TrackRepairSummary));
        RefreshCommands();
        return Task.CompletedTask;
    }

    private Task SkipTrackRepairIssueAsync()
    {
        if (SelectedTrackRepairIssue is null)
        {
            return Task.CompletedTask;
        }

        SelectedTrackRepairIssue.Skip();
        OnPropertyChanged(nameof(TrackRepairSummary));
        RefreshCommands();
        return Task.CompletedTask;
    }

    private async Task SaveTrackRepairsAsync()
    {
        var acceptedIssues = TrackRepairIssues
            .Where(issue => issue.IsAccepted && !string.IsNullOrWhiteSpace(issue.SelectedBatchId))
            .ToList();
        if (acceptedIssues.Count == 0)
        {
            Message = "No accepted repair rows to save.";
            return;
        }

        var validBatchIds = _trackItems
            .SelectMany(item => item.History)
            .Where(record => record.Action == "Owned" && !string.IsNullOrWhiteSpace(record.BatchId))
            .Select(record => record.BatchId)
            .Concat(acceptedIssues
                .Where(issue => issue.Action == "Owned")
                .Select(issue => issue.SelectedBatchId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var repairIssuesToApply = acceptedIssues
            .Where(issue => issue.Action == "Owned" || validBatchIds.Contains(issue.SelectedBatchId))
            .ToList();
        if (repairIssuesToApply.Count == 0)
        {
            Message = "Accept the matching Owned batch repair before saving Used or Put Back repairs.";
            return;
        }

        var backupPath = BackupTrackItemsBeforeRepair();
        foreach (var issue in repairIssuesToApply)
        {
            issue.Apply();
        }

        foreach (var item in _trackItems)
        {
            item.RecalculateQuantitiesFromHistory();
            item.NotifyCalculatedFieldsChanged();
        }

        await _fileStore.SaveTrackItemsAsync(_trackItems);
        RebuildTrackCategorySummaries();
        RebuildTrackRepairIssues();
        RefreshTrackStockBatches();
        TrackItemsView.Refresh();
        IsTrackRepairOpen = HasTrackRepairIssues;
        Message = $"Saved {repairIssuesToApply.Count} repair(s). Backup: {backupPath}";
    }

    private Task CancelTrackRepairAsync()
    {
        IsTrackRepairOpen = false;
        RebuildTrackRepairIssues();
        return Task.CompletedTask;
    }

    private bool CanAcceptTrackRepairIssue()
    {
        return SelectedTrackRepairIssue?.HasSuggestion == true;
    }

    private bool CanSkipTrackRepairIssue()
    {
        return SelectedTrackRepairIssue is not null;
    }

    private bool HasSuggestedTrackRepairIssues()
    {
        return TrackRepairIssues.Any(issue => issue.HasSuggestion);
    }

    private bool HasAcceptedTrackRepairIssues()
    {
        return TrackRepairIssues.Any(issue => issue.IsAccepted && !string.IsNullOrWhiteSpace(issue.SelectedBatchId));
    }

    private void AttachExpiryAlertHandlers(TrackSettings settings)
    {
        settings.ExpiryAlerts.CollectionChanged += ExpiryAlerts_CollectionChanged;
        foreach (var alert in settings.ExpiryAlerts)
        {
            alert.PropertyChanged += ExpiryAlert_PropertyChanged;
        }
    }

    private async Task ExportSelectedTaskToTaskTrackerAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var request = CreateTaskTrackerRequest(SelectedTask);
        if (request is null)
        {
            Message = "Selected task is not ready for Task Tracker export.";
            return;
        }

        var exported = await ExportTaskTrackerRequestAsync(request);
        Message = exported
            ? $"Task Tracker inbox request queued: {request.Title}"
            : "Task Tracker inbox already has this alert request.";
        if (exported)
        {
            TryOpenTaskTracker();
        }
    }

    private void DetachExpiryAlertHandlers(TrackSettings settings)
    {
        settings.ExpiryAlerts.CollectionChanged -= ExpiryAlerts_CollectionChanged;
        foreach (var alert in settings.ExpiryAlerts)
        {
            alert.PropertyChanged -= ExpiryAlert_PropertyChanged;
        }
    }

    private void ExpiryAlerts_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ExpiryAlertSetting alert in e.OldItems)
            {
                alert.PropertyChanged -= ExpiryAlert_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ExpiryAlertSetting alert in e.NewItems)
            {
                alert.PropertyChanged += ExpiryAlert_PropertyChanged;
            }
        }

        if (!_isSortingExpiryAlerts && sender is ObservableCollection<ExpiryAlertSetting> alerts)
        {
            SortExpiryAlerts(alerts);
        }
    }

    private void ExpiryAlert_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExpiryAlertSetting.Amount) or nameof(ExpiryAlertSetting.Unit) or nameof(ExpiryAlertSetting.Days))
        {
            SortExpiryAlerts(TrackSettingsDraft.ExpiryAlerts);
        }
    }

    private void SortExpiryAlerts(ObservableCollection<ExpiryAlertSetting> alerts)
    {
        if (_isSortingExpiryAlerts || alerts.Count < 2)
        {
            return;
        }

        _isSortingExpiryAlerts = true;
        try
        {
            var orderedAlerts = alerts
                .OrderByDescending(alert => alert.Days)
                .ThenBy(alert => alert.Unit, StringComparer.OrdinalIgnoreCase)
                .ThenBy(alert => alert.Amount)
                .ToList();

            for (var targetIndex = 0; targetIndex < orderedAlerts.Count; targetIndex++)
            {
                var currentIndex = alerts.IndexOf(orderedAlerts[targetIndex]);
                if (currentIndex != targetIndex)
                {
                    alerts.Move(currentIndex, targetIndex);
                }
            }
        }
        finally
        {
            _isSortingExpiryAlerts = false;
        }
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

    private async Task CheckAndNotifyTrackExpiryAsync()
    {
        var shownKeys = _trackSettings.ShownExpiryAlertKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newAlerts = new List<(string Key, TrackItem Item, TrackStockBatch Batch, DateTime ExpiryDate, string AlertText)>();

        foreach (var item in _trackItems)
        {
            if (!item.HasExpiryDate)
            {
                continue;
            }

            var batch = item.NearestExpiryBatch;
            var expiryDate = batch?.ExpiryDate?.Date;
            if (expiryDate is null)
            {
                continue;
            }

            var daysUntilExpiry = (expiryDate.Value - DateTime.Today).Days;
            var stockLifetimeDays = Math.Max(0, (expiryDate.Value - batch!.OwnedDate.Date).Days);
            var alertCode = GetExpiryAlertCode(daysUntilExpiry, stockLifetimeDays);
            if (string.IsNullOrWhiteSpace(alertCode))
            {
                continue;
            }

            var key = $"{item.Id}:{batch.BatchId}:{expiryDate:yyyyMMdd}:{alertCode}";
            if (shownKeys.Contains(key))
            {
                continue;
            }

            newAlerts.Add((key, item, batch, expiryDate.Value, DescribeExpiryAlert(alertCode, daysUntilExpiry)));
        }

        if (newAlerts.Count == 0)
        {
            return;
        }

        foreach (var alert in newAlerts)
        {
            _trackSettings.ShownExpiryAlertKeys.Add(alert.Key);
        }

        await _fileStore.SaveTrackSettingsAsync(_trackSettings);

        var lines = newAlerts
            .OrderBy(alert => alert.ExpiryDate)
            .ThenBy(alert => alert.Item.Name)
            .Take(12)
            .Select(alert => $"{alert.Item.Name}: {FormatDate(alert.ExpiryDate)} ({alert.AlertText}) - {alert.Batch.Display}");

        MessageBox.Show(
            string.Join(Environment.NewLine, lines),
            "Track Expiry Reminder",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Message = $"Track expiry reminder shown for {newAlerts.Count} item(s).";
    }

    private async Task AutoExportTaskTrackerAlertsAsync()
    {
        var exportedCount = 0;
        if (TaskTrackerTaskExportMode == Models.TaskTrackerExportModes.Auto)
        {
            foreach (var task in _tasks)
            {
                var request = CreateTaskTrackerRequest(task);
                if (request is not null && await ExportTaskTrackerRequestAsync(request))
                {
                    exportedCount++;
                }
            }
        }

        if (TaskTrackerTrackExportMode == Models.TaskTrackerExportModes.Auto)
        {
            foreach (var item in _trackItems)
            {
                foreach (var request in CreateTrackTaskTrackerRequests(item))
                {
                    if (await ExportTaskTrackerRequestAsync(request))
                    {
                        exportedCount++;
                    }
                }
            }
        }

        if (exportedCount > 0)
        {
            Message = $"Queued {exportedCount} Task Tracker alert request(s).";
        }
    }

    private async Task<bool> ExportTaskTrackerRequestAsync(TaskTrackerAlertRequest request)
    {
        _config.TaskTrackerExportedSourceKeys ??= [];
        if (_config.TaskTrackerExportedSourceKeys.Contains(request.SourceKey, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var exported = await _taskTrackerExportService.AppendRequestAsync(TaskTrackerExportPath, request);
        if (!exported)
        {
            AddTaskTrackerExportedSourceKey(request.SourceKey);
            await SaveConfigAsync();
            OnPropertyChanged(nameof(CanPromptExportSelectedTaskToTaskTracker));
            OnPropertyChanged(nameof(CanPromptExportSelectedTrackItemToTaskTracker));
            RefreshCommands();
            return false;
        }

        AddTaskTrackerExportedSourceKey(request.SourceKey);
        await SaveConfigAsync();
        OnPropertyChanged(nameof(CanPromptExportSelectedTaskToTaskTracker));
        OnPropertyChanged(nameof(CanPromptExportSelectedTrackItemToTaskTracker));
        RefreshCommands();
        return true;
    }

    private TaskTrackerAlertRequest? CreateTaskTrackerRequest(SheetTask task)
    {
        if (task.Completed)
        {
            return null;
        }

        var eligibleDate = GetTaskAlertEligibleDate(task);
        if (eligibleDate is null || eligibleDate.Value.Date > DateTime.Today)
        {
            return null;
        }

        var scheduledDate = (task.ExpiredDate ?? task.WarningDate ?? eligibleDate).GetValueOrDefault(DateTime.Today).Date;
        var sourceKey = CreateTaskSourceKey(task);
        return new TaskTrackerAlertRequest
        {
            RequestId = TaskTrackerAlertExportService.CreateRequestId(),
            SourceKey = sourceKey,
            SourceKind = "task",
            Title = task.Task,
            Remark = BuildTaskTrackerTaskRemark(task, eligibleDate.Value, scheduledDate),
            EligibleAt = eligibleDate.Value.Date,
            ScheduledAt = scheduledDate,
            NotificationRules = CreateSpecificNotificationRules(scheduledDate),
            Priority = task.Status == "Expired" ? "urgent" : "high",
            Metadata = new Dictionary<string, string>
            {
                ["category"] = task.Category,
                ["type"] = task.Type,
                ["task"] = task.Task,
                ["rowNumber"] = task.RowNumber.ToString(),
                ["warningDate"] = FormatMetadataDate(task.WarningDate),
                ["expiredDate"] = FormatMetadataDate(task.ExpiredDate),
                ["status"] = task.Status
            }
        };
    }

    private IEnumerable<TaskTrackerAlertRequest> CreateTrackTaskTrackerRequests(TrackItem item)
    {
        if (item.IsQuantityUsage && !string.IsNullOrWhiteSpace(item.StockAlertLevel))
        {
            yield return CreateTrackLowStockRequest(item);
        }

        if (item.IsQuantityUsage && item.HasExpiryDate && item.NearestExpiryBatch is { } batch && !string.IsNullOrWhiteSpace(item.ExpiryStatus))
        {
            yield return CreateTrackExpiryRequest(item, batch);
        }

        if (item.IsChangeCycle && item.NextChangeDate is { } nextChangeDate && item.DaysUntilChange is <= 0)
        {
            yield return CreateTrackReplacementRequest(item, nextChangeDate);
        }
    }

    private static TaskTrackerAlertRequest CreateTrackLowStockRequest(TrackItem item)
    {
        var sourceKey = $"lifesync-track-low-stock-{ShortId(item.Id)}-{NormalizeKeyPart(item.StockAlertLevel)}";
        return new TaskTrackerAlertRequest
        {
            RequestId = TaskTrackerAlertExportService.CreateRequestId(),
            SourceKey = sourceKey,
            SourceKind = "track-low-stock",
            Title = $"Restock {item.Name}",
            Remark = $"LifeSync TRACK low stock alert.{Environment.NewLine}Item: {item.Name}{Environment.NewLine}Category: {item.Category}{Environment.NewLine}Status: {item.StockAlertLevel}{Environment.NewLine}Owned: {item.TotalQuantity}{Environment.NewLine}Used: {item.UsedQuantity}{Environment.NewLine}Left: {item.LeftQuantity}",
            EligibleAt = DateTime.Today,
            ScheduledAt = DateTime.Now,
            NotificationRules = [],
            Priority = item.StockAlertLevel == "Out" ? "urgent" : "high",
            Metadata = TrackMetadata(item, "low-stock")
        };
    }

    private static TaskTrackerAlertRequest CreateTrackExpiryRequest(TrackItem item, TrackStockBatch batch)
    {
        var expiryDate = batch.ExpiryDate?.Date ?? DateTime.Today;
        var sourceKey = $"lifesync-track-expiry-{ShortId(item.Id)}-{NormalizeKeyPart(batch.BatchId)}-{expiryDate:yyyyMMdd}";
        var metadata = TrackMetadata(item, "expiry");
        metadata["batchId"] = batch.BatchId;
        metadata["expiryDate"] = FormatMetadataDate(expiryDate);
        return new TaskTrackerAlertRequest
        {
            RequestId = TaskTrackerAlertExportService.CreateRequestId(),
            SourceKey = sourceKey,
            SourceKind = "track-expiry",
            Title = $"Check expiring {item.Name}",
            Remark = $"LifeSync TRACK expiry alert.{Environment.NewLine}Item: {item.Name}{Environment.NewLine}Category: {item.Category}{Environment.NewLine}Status: {item.ExpiryStatus}{Environment.NewLine}Expiry: {FormatDate(expiryDate)}{Environment.NewLine}Batch: {batch.Display}",
            EligibleAt = DateTime.Today,
            ScheduledAt = expiryDate,
            NotificationRules = CreateSpecificNotificationRules(expiryDate),
            Priority = item.ExpiryStatus == "Expired" ? "urgent" : "high",
            Metadata = metadata
        };
    }

    private static TaskTrackerAlertRequest CreateTrackReplacementRequest(TrackItem item, DateTime nextChangeDate)
    {
        var dueDate = nextChangeDate.Date;
        var sourceKey = $"lifesync-track-replacement-{ShortId(item.Id)}-{dueDate:yyyyMMdd}";
        return new TaskTrackerAlertRequest
        {
            RequestId = TaskTrackerAlertExportService.CreateRequestId(),
            SourceKey = sourceKey,
            SourceKind = "track-replacement",
            Title = $"Replace {item.Name}",
            Remark = $"LifeSync TRACK replacement alert.{Environment.NewLine}Item: {item.Name}{Environment.NewLine}Category: {item.Category}{Environment.NewLine}Status: {item.ChangeStatus}{Environment.NewLine}Change every: {item.ChangeEvery} {item.ChangeUnit}{Environment.NewLine}Next replacement: {FormatDate(dueDate)}",
            EligibleAt = DateTime.Today,
            ScheduledAt = dueDate,
            NotificationRules = CreateSpecificNotificationRules(dueDate),
            Priority = item.DaysUntilChange is < 0 ? "urgent" : "high",
            Metadata = TrackMetadata(item, "replacement")
        };
    }

    private static Dictionary<string, string> TrackMetadata(TrackItem item, string alertKind)
    {
        return new Dictionary<string, string>
        {
            ["itemId"] = item.Id,
            ["itemName"] = item.Name,
            ["category"] = item.Category,
            ["trackType"] = item.TrackType,
            ["alertKind"] = alertKind,
            ["alertStatus"] = item.AlertStatus,
            ["stockAlertLevel"] = item.StockAlertLevel
        };
    }

    private static DateTime? GetTaskAlertEligibleDate(SheetTask task)
    {
        if (task.WarningDate is not null && task.WarningDate.Value.Date != task.ExpiredDate?.Date)
        {
            return task.WarningDate.Value.Date;
        }

        return (task.ExpiredDate ?? task.WarningDate)?.Date;
    }

    private static string CreateTaskSourceKey(SheetTask task)
    {
        var fingerprint = string.Join("|", "task", task.Category, task.Type, task.Task, FormatMetadataDate(task.WarningDate), FormatMetadataDate(task.ExpiredDate));
        return TaskTrackerAlertExportService.CreateHashedSourceKey("lifesync-task", fingerprint);
    }

    private static string BuildTaskTrackerTaskRemark(SheetTask task, DateTime eligibleDate, DateTime scheduledDate)
    {
        var parts = new[]
        {
            "LifeSync TASK alert.",
            $"Category: {task.Category}",
            $"Type: {task.Type}",
            $"Task: {task.Task}",
            $"Eligible: {FormatDate(eligibleDate)}",
            $"Scheduled: {FormatDate(scheduledDate)}",
            $"Warning Date: {FormatMetadataDate(task.WarningDate)}",
            $"Expired Date: {FormatMetadataDate(task.ExpiredDate)}",
            $"Sheet Row (metadata only): {task.RowNumber}",
            string.IsNullOrWhiteSpace(task.Remark) ? string.Empty : $"Remark: {task.Remark}"
        };

        return string.Join(Environment.NewLine, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static List<TaskTrackerNotificationRule> CreateSpecificNotificationRules(DateTime specificAt)
    {
        return
        [
            new TaskTrackerNotificationRule
            {
                Type = "specificTime",
                SpecificAt = specificAt
            }
        ];
    }

    private void TryOpenTaskTracker()
    {
        if (string.IsNullOrWhiteSpace(TaskTrackerExePath) || !File.Exists(TaskTrackerExePath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(TaskTrackerExePath)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Failed to open Task Tracker executable: {ex.Message}");
        }
    }

    private void NormalizeTaskTrackerConfig()
    {
        _config.TaskTrackerTaskExportMode = NormalizeTaskTrackerExportMode(_config.TaskTrackerTaskExportMode);
        _config.TaskTrackerTrackExportMode = NormalizeTaskTrackerExportMode(_config.TaskTrackerTrackExportMode);
        if (string.IsNullOrWhiteSpace(_config.TaskTrackerExportPath))
        {
            _config.TaskTrackerExportPath = AppPaths.TaskTrackerExportPath;
        }

        _config.TaskTrackerExportedSourceKeys = _config.TaskTrackerExportedSourceKeys
            ?? [];
        _config.TaskTrackerExportedSourceKeys = _config.TaskTrackerExportedSourceKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void AddTaskTrackerExportedSourceKey(string sourceKey)
    {
        _config.TaskTrackerExportedSourceKeys ??= [];
        if (!_config.TaskTrackerExportedSourceKeys.Contains(sourceKey, StringComparer.OrdinalIgnoreCase))
        {
            _config.TaskTrackerExportedSourceKeys.Add(sourceKey);
        }
    }

    private static string NormalizeTaskTrackerExportMode(string? value)
    {
        return value?.Trim() switch
        {
            Models.TaskTrackerExportModes.Prompt => Models.TaskTrackerExportModes.Prompt,
            Models.TaskTrackerExportModes.Auto => Models.TaskTrackerExportModes.Auto,
            _ => Models.TaskTrackerExportModes.Off
        };
    }

    private static string ShortId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : value.Length <= 8 ? value : value[..8];
    }

    private static string NormalizeKeyPart(string value)
    {
        var cleaned = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned;
    }

    private static string FormatMetadataDate(DateTime? date)
    {
        return date?.Date.ToString("yyyy-MM-dd") ?? string.Empty;
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

    private string GetExpiryAlertCode(int daysUntilExpiry, int? stockLifetimeDays = null)
    {
        if (daysUntilExpiry < 0)
        {
            return "expired";
        }

        var alert = _trackSettings.GetExpiryAlertForDays(daysUntilExpiry, stockLifetimeDays);
        return alert is null ? string.Empty : alert.Days.ToString();
    }

    private static string DescribeExpiryAlert(string alertCode, int daysUntilExpiry)
    {
        if (alertCode == "expired")
        {
            return "expired";
        }

        return daysUntilExpiry switch
        {
            0 => "expires today",
            1 => "1 day left",
            _ => $"{daysUntilExpiry} days left"
        };
    }

    private static TrackSettings CloneTrackSettings(TrackSettings source)
    {
        return new TrackSettings
        {
            LowStockThreshold = source.LowStockThreshold,
            CriticalStockThreshold = source.CriticalStockThreshold,
            OutStockThreshold = source.OutStockThreshold,
            LowStockColor = source.LowStockColor,
            CriticalStockColor = source.CriticalStockColor,
            OutStockColor = source.OutStockColor,
            ExpiryReminderText = source.ExpiryReminderText,
            ExpiryTwoMonthColor = source.ExpiryTwoMonthColor,
            ExpiryOneMonthColor = source.ExpiryOneMonthColor,
            ExpiryOneWeekColor = source.ExpiryOneWeekColor,
            ExpiryExpiredColor = source.ExpiryExpiredColor,
            ExpiryAlerts = new ObservableCollection<ExpiryAlertSetting>(source.ExpiryAlerts.Select(CloneExpiryAlertSetting)),
            ShownExpiryAlertKeys = source.ShownExpiryAlertKeys.ToList()
        };
    }

    private TrackSettings NormalizeTrackSettings(TrackSettings settings)
    {
        settings.LowStockColor = NormalizeTrackColor(settings.LowStockColor, TrackSettingColors.Yellow);
        settings.CriticalStockColor = NormalizeTrackColor(settings.CriticalStockColor, TrackSettingColors.Orange);
        settings.OutStockColor = NormalizeTrackColor(settings.OutStockColor, TrackSettingColors.Red);
        settings.ExpiryExpiredColor = NormalizeTrackColor(settings.ExpiryExpiredColor, TrackSettingColors.Red);

        if (settings.ExpiryAlerts.Count == 0)
        {
            foreach (var alert in BuildExpiryAlertsFromText(settings.ExpiryReminderText))
            {
                settings.ExpiryAlerts.Add(alert);
            }
        }

        foreach (var alert in settings.ExpiryAlerts)
        {
            alert.Color = NormalizeTrackColor(alert.Color, TrackSettingColors.Yellow);
            alert.Unit = NormalizeExpiryAlertUnit(alert.Unit);
            alert.Amount = Math.Max(1, alert.Amount);
        }

        SortExpiryAlerts(settings.ExpiryAlerts);
        return settings;
    }

    private static ExpiryAlertSetting CloneExpiryAlertSetting(ExpiryAlertSetting source)
    {
        return new ExpiryAlertSetting
        {
            Amount = source.Amount,
            Unit = source.Unit,
            Color = source.Color
        };
    }

    private static IEnumerable<ExpiryAlertSetting> BuildExpiryAlertsFromText(string reminderText)
    {
        foreach (var value in reminderText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalizedValue = value.Trim().ToLowerInvariant();
            var numberText = new string(normalizedValue.TakeWhile(char.IsDigit).ToArray());
            if (!int.TryParse(numberText, out var amount) || amount <= 0)
            {
                continue;
            }

            var unit = normalizedValue switch
            {
                var text when text.Contains("year") => ExpiryAlertUnits.Year,
                var text when text.Contains("month") => ExpiryAlertUnits.Month,
                var text when text.Contains("week") => ExpiryAlertUnits.Week,
                _ => ExpiryAlertUnits.Day
            };

            yield return new ExpiryAlertSetting
            {
                Amount = amount,
                Unit = unit,
                Color = unit switch
                {
                    ExpiryAlertUnits.Year => TrackSettingColors.Green,
                    ExpiryAlertUnits.Month => amount > 1 ? TrackSettingColors.Blue : TrackSettingColors.Yellow,
                    ExpiryAlertUnits.Week => TrackSettingColors.Orange,
                    _ => TrackSettingColors.Red
                }
            };
        }
    }

    private static string NormalizeTrackColor(string? value, string fallback)
    {
        return value?.Trim() switch
        {
            TrackSettingColors.Yellow or "#FFF6D8" => TrackSettingColors.Yellow,
            TrackSettingColors.Orange or "#FDEECC" => TrackSettingColors.Orange,
            TrackSettingColors.Red or "#FDECEC" => TrackSettingColors.Red,
            TrackSettingColors.DarkGray or "DarkGrey" or "Gray" or "Grey" => TrackSettingColors.DarkGray,
            TrackSettingColors.Green => TrackSettingColors.Green,
            TrackSettingColors.Blue or "#EEF4FF" => TrackSettingColors.Blue,
            _ => fallback
        };
    }

    private static string NormalizeExpiryAlertUnit(string? value)
    {
        return value?.Trim() switch
        {
            ExpiryAlertUnits.Day or "Days" => ExpiryAlertUnits.Day,
            ExpiryAlertUnits.Week or "Weeks" => ExpiryAlertUnits.Week,
            ExpiryAlertUnits.Month or "Months" => ExpiryAlertUnits.Month,
            ExpiryAlertUnits.Year or "Years" => ExpiryAlertUnits.Year,
            _ => ExpiryAlertUnits.Day
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

    private Task CloseTrackSidebarAsync()
    {
        IsTrackDetailSidebarOpen = false;
        IsTrackRecordSidebarOpen = false;
        TrackItemDraft = null;
        _isNewTrackItemDraft = false;
        return Task.CompletedTask;
    }

    private void RebuildTrackRepairIssues()
    {
        var issues = new ObservableCollection<TrackRepairIssue>();
        foreach (var item in _trackItems.Where(item => item.IsQuantityUsage))
        {
            foreach (var issue in BuildTrackRepairIssues(item))
            {
                issue.PropertyChanged += TrackRepairIssue_PropertyChanged;
                issues.Add(issue);
            }
        }

        TrackRepairIssues = issues;
        SelectedTrackRepairIssue = TrackRepairIssues.FirstOrDefault();
    }

    private IEnumerable<TrackRepairIssue> BuildTrackRepairIssues(TrackItem item)
    {
        var ownedRecords = item.History
            .Where(record => record.Action == "Owned")
            .OrderBy(record => record.Date)
            .ThenBy(record => record.RecordedAt)
            .ToList();

        var effectiveBatchIds = ownedRecords
            .Select((record, index) => new
            {
                Record = record,
                BatchId = string.IsNullOrWhiteSpace(record.BatchId)
                    ? CreateStableRepairBatchId(item, record, index)
                    : record.BatchId
            })
            .ToDictionary(value => value.Record, value => value.BatchId);

        foreach (var (record, index) in ownedRecords.Select((record, index) => (record, index)))
        {
            if (!string.IsNullOrWhiteSpace(record.BatchId))
            {
                continue;
            }

            var suggestedBatchId = effectiveBatchIds[record];
            var suggested = CreateRepairBatchChoice(index, record, suggestedBatchId);
            yield return new TrackRepairIssue(
                item,
                record,
                "Owned missing batch",
                "Create stock batch id",
                [suggested],
                suggested.BatchId);
        }

        var batches = ownedRecords
            .Select((record, index) => new RepairBatchState(index, record, effectiveBatchIds[record]))
            .ToList();

        foreach (var record in item.History.OrderBy(record => record.Date).ThenBy(record => record.RecordedAt))
        {
            if (record.Action == "Owned")
            {
                continue;
            }

            if (record.Action == "Used")
            {
                if (!string.IsNullOrWhiteSpace(record.SourceBatchId))
                {
                    var linkedBatch = batches.FirstOrDefault(batch => batch.BatchId == record.SourceBatchId);
                    if (linkedBatch is not null)
                    {
                        linkedBatch.UsedQuantity += record.Quantity;
                    }

                    continue;
                }

                var candidates = batches
                    .Where(batch => batch.RemainingQuantity >= record.Quantity)
                    .OrderBy(batch => batch.ExpiryDate ?? DateTime.MaxValue)
                    .ThenBy(batch => batch.OwnedDate)
                    .ThenBy(batch => batch.Index)
                    .Select(batch => batch.ToChoice())
                    .ToList();
                var selectedBatchId = candidates.FirstOrDefault()?.BatchId ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(selectedBatchId))
                {
                    batches.First(batch => batch.BatchId == selectedBatchId).UsedQuantity += record.Quantity;
                }

                yield return new TrackRepairIssue(
                    item,
                    record,
                    "Used missing source batch",
                    "Link used stock to batch",
                    candidates,
                    selectedBatchId);
                continue;
            }

            if (record.Action == "Put Back")
            {
                if (!string.IsNullOrWhiteSpace(record.SourceBatchId))
                {
                    var linkedBatch = batches.FirstOrDefault(batch => batch.BatchId == record.SourceBatchId);
                    if (linkedBatch is not null)
                    {
                        linkedBatch.ReturnedQuantity += record.Quantity;
                    }

                    continue;
                }

                var candidates = batches
                    .Where(batch => batch.ReturnableQuantity >= record.Quantity)
                    .OrderBy(batch => batch.ExpiryDate ?? DateTime.MaxValue)
                    .ThenBy(batch => batch.OwnedDate)
                    .ThenBy(batch => batch.Index)
                    .Select(batch => batch.ToChoice())
                    .ToList();
                var selectedBatchId = candidates.FirstOrDefault()?.BatchId ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(selectedBatchId))
                {
                    batches.First(batch => batch.BatchId == selectedBatchId).ReturnedQuantity += record.Quantity;
                }

                yield return new TrackRepairIssue(
                    item,
                    record,
                    "Put back missing source batch",
                    "Link returned stock to batch",
                    candidates,
                    selectedBatchId);
            }
        }
    }

    private void TrackRepairIssue_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(TrackRepairSummary));
        RefreshCommands();
    }

    private static string CreateStableRepairBatchId(TrackItem item, TrackItemHistory record, int index)
    {
        var itemId = string.IsNullOrWhiteSpace(item.Id) ? "item" : item.Id[..Math.Min(8, item.Id.Length)];
        return $"repair-{itemId}-{record.Date:yyyyMMdd}-{index + 1:000}";
    }

    private static TrackRepairBatchChoice CreateRepairBatchChoice(int index, TrackItemHistory record, string batchId)
    {
        var parts = new List<string>
        {
            $"Record {index + 1}",
            $"{record.Date:dd MMM yyyy}",
            $"{record.Quantity} stock"
        };

        if (record.ExpiryDate is not null)
        {
            parts.Add($"exp {record.ExpiryDate.Value:dd MMM yyyy}");
        }

        if (!string.IsNullOrWhiteSpace(record.Location))
        {
            parts.Add(record.Location);
        }

        if (!string.IsNullOrWhiteSpace(record.Remark))
        {
            parts.Add(record.Remark);
        }

        return new TrackRepairBatchChoice(batchId, string.Join(" | ", parts));
    }

    private static string BackupTrackItemsBeforeRepair()
    {
        var backupDirectory = Path.Combine(AppPaths.DataDirectory, $"backup-track-repair-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(backupDirectory);
        var backupPath = Path.Combine(backupDirectory, "track-items.json");
        File.Copy(AppPaths.TrackItemsPath, backupPath, overwrite: false);
        return backupPath;
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

    private void ApplyTrackFilters()
    {
        ApplyTrackDefaultSort();
        TrackItemsView.Refresh();
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
        NormalizeTaskTrackerConfig();
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
        target.TrackId = source.TrackId;
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

    private void ReplaceTrackItems(IEnumerable<TrackItem> items)
    {
        _trackItems.Clear();
        foreach (var item in items)
        {
            SortTrackHistoryDescending(item);
            item.RecalculateQuantitiesFromHistory();
            item.NotifyCalculatedFieldsChanged();
            _trackItems.Add(item);
        }

        RebuildTrackCategoryFilter();
        RebuildTrackCategorySummaries();
        TrackItemsView.Refresh();
    }

    private async Task SaveTrackItemsCoreAsync(string successMessage)
    {
        try
        {
            await _fileStore.SaveTrackItemsAsync(_trackItems);
            RebuildTrackCategorySummaries();
            RebuildTrackRepairIssues();
            AppLogger.Info($"Saved {_trackItems.Count} tracked item(s) to {AppPaths.TrackItemsPath}");
            Message = successMessage;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to save tracked items", ex);
            Message = $"Failed to save tracked items: {ex.Message}. Log: {AppPaths.CurrentWarningErrorLogPath}";
        }
        finally
        {
            RefreshCommands();
        }
    }

    private void AddTrackHistory(
        TrackItem item,
        string action,
        decimal quantity,
        string batchId = "",
        string sourceBatchId = "",
        DateTime? expiryDate = null,
        bool resetActionFields = true)
    {
        item.History.Add(new TrackItemHistory
        {
            Date = TrackActionDate ?? DateTime.Today,
            RecordedAt = DateTime.Now,
            Action = action,
            Quantity = quantity,
            Location = TrackActionLocation,
            Remark = TrackActionRemark,
            BatchId = batchId,
            SourceBatchId = sourceBatchId,
            ExpiryDate = expiryDate
        });

        SortTrackHistoryDescending(item);
        item.RecalculateQuantitiesFromHistory();
        item.NotifyCalculatedFieldsChanged();
        if (resetActionFields)
        {
            ResetTrackActionFields();
        }
    }

    private void SetTrackActionMode(string mode)
    {
        _trackActionMode = mode;
        TrackActionQuantity = 1;
        TrackActionDate = DateTime.Today;
        TrackActionLocation = string.Empty;
        TrackActionRemark = string.Empty;
        TrackActionExpiryDate = null;
        OnTrackActionModeChanged();
        UpdateTrackRemarkSuggestions(TrackActionRemark);
    }

    private void ClearTrackActionMode()
    {
        if (string.IsNullOrWhiteSpace(_trackActionMode))
        {
            return;
        }

        _trackActionMode = TrackActionNone;
        OnTrackActionModeChanged();
    }

    private void OnTrackActionModeChanged()
    {
        OnPropertyChanged(nameof(IsTrackActionEditorOpen));
        OnPropertyChanged(nameof(TrackActionTitle));
        OnPropertyChanged(nameof(IsAddStockAction));
        OnPropertyChanged(nameof(IsUseStockAction));
        OnPropertyChanged(nameof(IsPutBackAction));
        OnPropertyChanged(nameof(IsChangedAction));
        OnPropertyChanged(nameof(IsQuantityAction));
        OnPropertyChanged(nameof(IsTrackActionLocationVisible));
        OnPropertyChanged(nameof(IsTrackActionExpiryVisible));
        OnPropertyChanged(nameof(IsReturnBatchChoiceVisible));
        RefreshCommands();
    }

    private IEnumerable<(TrackStockBatch Batch, decimal Quantity)> AllocateStockBatches(decimal requestedQuantity)
    {
        var remainingQuantity = requestedQuantity;
        foreach (var batch in TrackStockBatches
            .Where(batch => batch.RemainingQuantity > 0)
            .OrderBy(batch => batch.ExpiryDate ?? DateTime.MaxValue)
            .ThenBy(batch => batch.OwnedDate)
            .ThenBy(batch => batch.Display))
        {
            if (remainingQuantity <= 0)
            {
                yield break;
            }

            var quantity = Math.Min(remainingQuantity, batch.RemainingQuantity);
            remainingQuantity -= quantity;
            yield return (batch, quantity);
        }
    }

    private void ResetTrackActionFields()
    {
        TrackActionQuantity = 1;
        TrackActionDate = DateTime.Today;
        TrackActionLocation = string.Empty;
        TrackActionRemark = string.Empty;
        TrackActionExpiryDate = null;
    }

    private void RefreshTrackStockBatches()
    {
        var batches = SelectedTrackItem?.BuildBatches()
            .Where(batch => batch.RemainingQuantity > 0 || batch.UsedQuantity - batch.ReturnedQuantity > 0)
            .OrderBy(batch => batch.ExpiryDate ?? DateTime.MaxValue)
            .ThenBy(batch => batch.Display)
            .ToList() ?? [];

        TrackStockBatches = new ObservableCollection<TrackStockBatch>(batches);
        ReturnableTrackBatches = new ObservableCollection<TrackStockBatch>(
            batches
                .Where(batch => batch.UsedQuantity - batch.ReturnedQuantity > 0)
                .OrderByDescending(batch => batch.OwnedDate)
                .ThenBy(batch => batch.Display));
        SelectedTrackBatchId = TrackStockBatches.Any(batch => batch.BatchId == SelectedTrackBatchId)
            ? SelectedTrackBatchId
            : TrackStockBatches.FirstOrDefault()?.BatchId ?? string.Empty;
    }

    private static void SortTrackHistoryDescending(TrackItem item)
    {
        if (item.History.Count < 2)
        {
            return;
        }

        var sortedHistory = item.History
            .OrderByDescending(record => record.Date)
            .ThenByDescending(record => record.RecordedAt)
            .ToList();

        item.History.Clear();
        foreach (var record in sortedHistory)
        {
            item.History.Add(record);
        }
    }

    private async Task SaveNewTrackCategoryAsync(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return;
        }

        if (_trackOptions.Categories.Contains(category, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _trackOptions.Categories.Add(category);
        await _fileStore.SaveTrackOptionsAsync(_trackOptions);
    }

    private async Task SaveNewTrackRemarkAsync(string? remark)
    {
        if (string.IsNullOrWhiteSpace(remark))
        {
            return;
        }

        if (_trackOptions.Remarks.Contains(remark, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _trackOptions.Remarks.Add(remark);
        TrackRemarks = new ObservableCollection<string>(_trackOptions.Remarks.Where(value => !string.IsNullOrWhiteSpace(value)));
        await _fileStore.SaveTrackOptionsAsync(_trackOptions);
    }

    private static TrackItem CloneTrackItem(TrackItem source)
    {
        return new TrackItem
        {
            Id = source.Id,
            Name = source.Name,
            Category = source.Category,
            TrackType = source.TrackType,
            TotalQuantity = source.TotalQuantity,
            UsedQuantity = source.UsedQuantity,
            IsReusable = source.IsReusable,
            CurrentUseLocation = source.CurrentUseLocation,
            StartUseDate = source.StartUseDate,
            ChangeEvery = source.ChangeEvery,
            ChangeUnit = source.ChangeUnit,
            HasExpiryDate = source.HasExpiryDate,
            ExpiryDate = source.ExpiryDate,
            ExpiryReminderText = source.ExpiryReminderText,
            Notes = source.Notes,
            History = new ObservableTrackHistory(source.History.Select(CloneTrackHistory))
        };
    }

    private static TrackItemHistory CloneTrackHistory(TrackItemHistory source)
    {
        return new TrackItemHistory
        {
            Date = source.Date,
            RecordedAt = source.RecordedAt,
            Action = source.Action,
            Quantity = source.Quantity,
            Location = source.Location,
            Remark = source.Remark,
            BatchId = source.BatchId,
            SourceBatchId = source.SourceBatchId,
            ExpiryDate = source.ExpiryDate
        };
    }

    private static void CopyTrackItem(TrackItem source, TrackItem target)
    {
        target.Name = source.Name;
        target.Category = source.Category;
        target.TrackType = source.TrackType;
        target.ChangeEvery = source.ChangeEvery;
        target.ChangeUnit = source.ChangeUnit;
        target.HasExpiryDate = source.HasExpiryDate;
        target.Notes = source.Notes;
        target.NotifyCalculatedFieldsChanged();
    }

    private void ApplyDefaultSort()
    {
        TasksView.SortDescriptions.Clear();
        TasksView.SortDescriptions.Add(new SortDescription(nameof(SheetTask.Category), ListSortDirection.Ascending));
        TasksView.SortDescriptions.Add(new SortDescription(nameof(SheetTask.Type), ListSortDirection.Ascending));
        TasksView.SortDescriptions.Add(new SortDescription(nameof(SheetTask.Task), ListSortDirection.Ascending));
    }

    private void ApplyTrackDefaultSort()
    {
        TrackItemsView.SortDescriptions.Clear();
        TrackItemsView.SortDescriptions.Add(new SortDescription(nameof(TrackItem.Category), ListSortDirection.Ascending));
        TrackItemsView.SortDescriptions.Add(new SortDescription(nameof(TrackItem.Name), ListSortDirection.Ascending));
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

    private void RebuildTrackCategoryFilter()
    {
        var values = TrackCategories
            .Where(value => value != AllFilter)
            .Concat(_trackItems.Select(item => item.Category));
        TrackCategories = BuildFilterList(values);
        UpdateTrackCategorySuggestions(TrackItemDraft?.Category);
        _trackCategoryFilter = TrackCategories.Contains(_trackCategoryFilter) ? _trackCategoryFilter : AllFilter;
        OnPropertyChanged(nameof(TrackCategoryFilter));
    }

    private void RebuildTrackCategorySummaries()
    {
        TrackCategorySummaries = new ObservableCollection<TrackCategorySummary>(
            _trackItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Category))
                .GroupBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TrackCategorySummary(
                    group.Key,
                    group.Count(),
                    group.Count(IsTrackAttentionItem))));
    }

    private void UpdateTrackCategorySuggestions(string? typedText)
    {
        var values = TrackCategories
            .Where(value => value != AllFilter);

        if (!string.IsNullOrWhiteSpace(typedText))
        {
            values = values.Where(value => value.StartsWith(typedText, StringComparison.OrdinalIgnoreCase));
        }

        TrackCategorySuggestions = new ObservableCollection<string>(values.Take(10));
        IsTrackCategoryDropDownOpen = IsTrackDetailSidebarOpen && TrackCategorySuggestions.Count > 0;
    }

    private void UpdateTrackRemarkSuggestions(string? typedText)
    {
        var values = TrackRemarks.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(typedText))
        {
            values = values.Where(value => value.StartsWith(typedText, StringComparison.OrdinalIgnoreCase));
        }

        TrackRemarkSuggestions = new ObservableCollection<string>(values.Take(10));
        IsTrackRemarkDropDownOpen = false;
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

    private bool FilterTrackItem(object item)
    {
        if (item is not TrackItem trackItem)
        {
            return false;
        }

        if (SelectedTrackHomeView == TrackHomeAttention && !IsTrackAttentionItem(trackItem))
        {
            return false;
        }

        return Matches(_trackCategoryFilter, trackItem.Category)
            && Matches(_trackTypeFilter, trackItem.TrackType);
    }

    private static bool IsTrackAttentionItem(TrackItem item)
    {
        return item.IsChangeCycle
            ? item.DaysUntilChange is <= 0
            : !string.IsNullOrWhiteSpace(item.StockAlertLevel) || !string.IsNullOrWhiteSpace(item.ExpiryStatus);
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

    private bool CanExportSelectedTaskToTaskTracker()
    {
        return SelectedTask is not null
            && CreateTaskTrackerRequest(SelectedTask) is not null
            && !_config.TaskTrackerExportedSourceKeys.Contains(CreateTaskSourceKey(SelectedTask), StringComparer.OrdinalIgnoreCase);
    }

    private bool CanMutateSelectedTrackItem()
    {
        return SelectedTrackItem is not null;
    }

    private bool CanExportSelectedTrackItemToTaskTracker()
    {
        return SelectedTrackItem is not null
            && CreateTrackTaskTrackerRequests(SelectedTrackItem)
                .Any(request => !_config.TaskTrackerExportedSourceKeys.Contains(request.SourceKey, StringComparer.OrdinalIgnoreCase));
    }

    private bool CanMutateSelectedQuantityTrackItem()
    {
        return SelectedTrackItem?.IsQuantityUsage == true;
    }

    private bool CanMutateSelectedChangeCycleTrackItem()
    {
        return SelectedTrackItem?.IsChangeCycle == true;
    }

    private bool CanRemoveTrackHistory()
    {
        return SelectedTrackItem is not null && SelectedTrackHistory is not null;
    }

    private bool CanAddTrackOwned()
    {
        return SelectedTrackItem is not null
            && SelectedTrackItem.IsQuantityUsage
            && TrackActionQuantity > 0;
    }

    private bool CanUseTrackQuantity()
    {
        return SelectedTrackItem is not null
            && SelectedTrackItem.IsQuantityUsage
            && TrackActionQuantity > 0
            && TrackStockBatches.Sum(batch => batch.RemainingQuantity) >= TrackActionQuantity;
    }

    private bool CanReturnTrackQuantity()
    {
        return SelectedTrackItem is not null
            && SelectedTrackItem.IsQuantityUsage
            && TrackActionQuantity > 0
            && TrackStockBatches.Any(batch =>
                TrackActionQuantity <= batch.UsedQuantity - batch.ReturnedQuantity);
    }

    private bool CanReturnAnyTrackQuantity()
    {
        return SelectedTrackItem?.IsQuantityUsage == true
            && ReturnableTrackBatches.Count > 0;
    }

    private bool CanRecordTrackChange()
    {
        return SelectedTrackItem is not null
            && SelectedTrackItem.IsChangeCycle;
    }

    private bool CanSaveTrackAction()
    {
        return _trackActionMode switch
        {
            TrackActionAddStock => CanAddTrackOwned(),
            TrackActionUseStock => CanUseTrackQuantity(),
            TrackActionPutBack => CanReturnTrackQuantity(),
            TrackActionChanged => CanRecordTrackChange(),
            _ => false
        };
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
        ExportSelectedTaskToTaskTrackerCommand.RaiseCanExecuteChanged();
        OpenSelectedTaskSummaryItemCommand.RaiseCanExecuteChanged();
        SnoozeTaskSummary1DayCommand.RaiseCanExecuteChanged();
        SnoozeTaskSummary3DaysCommand.RaiseCanExecuteChanged();
        SnoozeTaskSummary7DaysCommand.RaiseCanExecuteChanged();
        SnoozeTaskSummary14DaysCommand.RaiseCanExecuteChanged();
        SnoozeTaskSummary30DaysCommand.RaiseCanExecuteChanged();
        SnoozeTaskSummaryCustomCommand.RaiseCanExecuteChanged();
        ClearTaskSummarySnoozeCommand.RaiseCanExecuteChanged();
        OpenTrackRepairCommand.RaiseCanExecuteChanged();
        AcceptTrackRepairIssueCommand.RaiseCanExecuteChanged();
        AcceptAllTrackRepairIssuesCommand.RaiseCanExecuteChanged();
        SkipTrackRepairIssueCommand.RaiseCanExecuteChanged();
        SaveTrackRepairsCommand.RaiseCanExecuteChanged();
        RemoveTrackItemCommand.RaiseCanExecuteChanged();
        RemoveTrackHistoryCommand.RaiseCanExecuteChanged();
        ExportSelectedTrackItemToTaskTrackerCommand.RaiseCanExecuteChanged();
        AddTrackOwnedCommand.RaiseCanExecuteChanged();
        UseTrackQuantityCommand.RaiseCanExecuteChanged();
        ReturnTrackQuantityCommand.RaiseCanExecuteChanged();
        RecordTrackChangeCommand.RaiseCanExecuteChanged();
        OpenAddStockActionCommand.RaiseCanExecuteChanged();
        OpenUseStockActionCommand.RaiseCanExecuteChanged();
        OpenPutBackActionCommand.RaiseCanExecuteChanged();
        OpenChangedActionCommand.RaiseCanExecuteChanged();
        SaveTrackActionCommand.RaiseCanExecuteChanged();
        EditTrackItemCommand.RaiseCanExecuteChanged();
        OpenTrackRecordSidebarCommand.RaiseCanExecuteChanged();
    }

    private void SelectedTrackItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TrackItem.TrackType)
            or nameof(TrackItem.IsReusable)
            or nameof(TrackItem.TotalQuantity)
            or nameof(TrackItem.UsedQuantity)
            or nameof(TrackItem.StartUseDate)
            or nameof(TrackItem.ChangeEvery)
            or nameof(TrackItem.ChangeUnit)
            or nameof(TrackItem.HasExpiryDate)
            or nameof(TrackItem.ExpiryDate)
            or nameof(TrackItem.ExpiryReminderText))
        {
            OnPropertyChanged(nameof(HasSelectedQuantityTrackItem));
            OnPropertyChanged(nameof(HasSelectedChangeCycleTrackItem));
            OnPropertyChanged(nameof(HasSelectedTrackItemExpiry));
            RefreshCommands();
        }
    }

    private void TrackItemDraft_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrackItem.Category))
        {
            UpdateTrackCategorySuggestions(TrackItemDraft?.Category);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class RepairBatchState
    {
        public RepairBatchState(int index, TrackItemHistory record, string batchId)
        {
            Index = index;
            BatchId = batchId;
            OwnedDate = record.Date;
            ExpiryDate = record.ExpiryDate;
            OriginalQuantity = record.Quantity;
            Display = CreateRepairBatchChoice(index, record, batchId).Display;
        }

        public int Index { get; }
        public string BatchId { get; }
        public DateTime OwnedDate { get; }
        public DateTime? ExpiryDate { get; }
        public decimal OriginalQuantity { get; }
        public decimal UsedQuantity { get; set; }
        public decimal ReturnedQuantity { get; set; }
        public string Display { get; }
        public decimal RemainingQuantity => Math.Max(0, OriginalQuantity - UsedQuantity + ReturnedQuantity);
        public decimal ReturnableQuantity => Math.Max(0, UsedQuantity - ReturnedQuantity);

        public TrackRepairBatchChoice ToChoice() => new(BatchId, Display);
    }
}

public sealed class TrackRepairIssue : INotifyPropertyChanged
{
    private readonly TrackItemHistory _record;
    private bool _isAccepted;
    private bool _isSkipped;
    private string _selectedBatchId;

    public TrackRepairIssue(
        TrackItem item,
        TrackItemHistory record,
        string issueType,
        string repairAction,
        IEnumerable<TrackRepairBatchChoice> candidateBatches,
        string suggestedBatchId)
    {
        ItemName = item.Name;
        _record = record;
        IssueType = issueType;
        RepairAction = repairAction;
        CandidateBatches = new ObservableCollection<TrackRepairBatchChoice>(candidateBatches);
        SuggestedBatchId = suggestedBatchId;
        _selectedBatchId = suggestedBatchId;
    }

    public string ItemName { get; }
    public string IssueType { get; }
    public string RepairAction { get; }
    public DateTime Date => _record.Date;
    public string Action => _record.Action;
    public decimal Quantity => _record.Quantity;
    public string Remark => _record.Remark;
    public ObservableCollection<TrackRepairBatchChoice> CandidateBatches { get; }
    public string SuggestedBatchId { get; }
    public bool HasSuggestion => !string.IsNullOrWhiteSpace(SelectedBatchId);

    public string SelectedBatchId
    {
        get => _selectedBatchId;
        set
        {
            if (_selectedBatchId == value)
            {
                return;
            }

            _selectedBatchId = value;
            IsSkipped = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(HasSuggestion));
        }
    }

    public bool IsAccepted
    {
        get => _isAccepted;
        private set
        {
            if (_isAccepted == value)
            {
                return;
            }

            _isAccepted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusDisplay));
        }
    }

    public bool IsSkipped
    {
        get => _isSkipped;
        private set
        {
            if (_isSkipped == value)
            {
                return;
            }

            _isSkipped = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusDisplay));
        }
    }

    public string StatusDisplay => IsAccepted
        ? "Accepted"
        : IsSkipped
            ? "Skipped"
            : HasSuggestion
                ? "Suggested"
                : "Needs manual choice";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Accept()
    {
        if (!HasSuggestion)
        {
            return;
        }

        IsAccepted = true;
        IsSkipped = false;
    }

    public void Skip()
    {
        IsAccepted = false;
        IsSkipped = true;
    }

    public void Apply()
    {
        if (!IsAccepted || string.IsNullOrWhiteSpace(SelectedBatchId))
        {
            return;
        }

        if (_record.Action == "Owned")
        {
            _record.BatchId = SelectedBatchId;
            return;
        }

        _record.SourceBatchId = SelectedBatchId;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record TrackRepairBatchChoice(string BatchId, string Display);

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

public sealed record TrackCategorySummary(string Category, int ItemCount, int AlertCount);
