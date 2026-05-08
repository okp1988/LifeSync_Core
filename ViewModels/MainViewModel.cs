using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
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
    private readonly ObservableCollection<TrackItem> _trackItems = [];
    private AppConfig _config = new();
    private TrackOptions _trackOptions = new();
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
    private bool _isLoadingTasks;
    private bool _isMarkingComplete;
    private bool _isTaskSidebarOpen;
    private bool _isTrackDetailSidebarOpen;
    private bool _isTrackRecordSidebarOpen;
    private string _message = "Ready";
    private string _selectedRemarkDraft = string.Empty;
    private DateTime? _completionDate = DateTime.Today;
    private decimal _trackActionQuantity = 1;
    private DateTime? _trackActionDate = DateTime.Today;
    private string _trackActionLocation = string.Empty;
    private string _trackActionRemark = string.Empty;
    private ObservableCollection<string> _categories = [AllFilter];
    private ObservableCollection<string> _types = [AllFilter];
    private ObservableCollection<string> _trackCategories = [AllFilter];
    private ObservableCollection<string> _trackCategorySuggestions = [];
    private ObservableCollection<string> _trackRemarks = [];
    private ObservableCollection<string> _trackRemarkSuggestions = [];
    private bool _isTrackCategoryDropDownOpen;
    private bool _isTrackRemarkDropDownOpen;

    public MainViewModel()
    {
        TasksView = CollectionViewSource.GetDefaultView(_tasks);
        TasksView.Filter = FilterTask;
        TrackItemsView = CollectionViewSource.GetDefaultView(_trackItems);
        TrackItemsView.Filter = FilterTrackItem;
        ApplyDefaultSort();
        ApplyTrackDefaultSort();

        RequestTasksCommand = new RelayCommand(RequestTasksAsync, CanUseNetwork);
        RefreshCalculatedFieldsCommand = new RelayCommand(RefreshCalculatedFieldsAsync);
        MarkCompleteCommand = new RelayCommand(MarkCompleteAsync, CanMutateSelectedTask);
        ClearFiltersCommand = new RelayCommand(ClearFiltersAsync);
        CloseTaskSidebarCommand = new RelayCommand(CloseTaskSidebarAsync);
        NewTrackItemCommand = new RelayCommand(NewTrackItemAsync);
        SaveTrackItemsCommand = new RelayCommand(SaveTrackItemsAsync);
        RemoveTrackItemCommand = new RelayCommand(RemoveTrackItemAsync, CanMutateSelectedTrackItem);
        RemoveTrackHistoryCommand = new RelayCommand(RemoveTrackHistoryAsync, CanRemoveTrackHistory);
        AddTrackOwnedCommand = new RelayCommand(AddTrackOwnedAsync, CanAddTrackOwned);
        UseTrackQuantityCommand = new RelayCommand(UseTrackQuantityAsync, CanUseTrackQuantity);
        ReturnTrackQuantityCommand = new RelayCommand(ReturnTrackQuantityAsync, CanReturnTrackQuantity);
        RecordTrackChangeCommand = new RelayCommand(RecordTrackChangeAsync, CanRecordTrackChange);
        EditTrackItemCommand = new RelayCommand(EditTrackItemAsync, CanMutateSelectedTrackItem);
        OpenTrackRecordSidebarCommand = new RelayCommand(OpenTrackRecordSidebarAsync, CanMutateSelectedTrackItem);
        CloseTrackSidebarCommand = new RelayCommand(CloseTrackSidebarAsync);
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

    public string[] Statuses { get; } = [AllFilter, "Normal", "Warning", "Expired", "Completed"];
    public string[] DayLeftFilters { get; } = [AllFilter, "Overdue", "Due today", "Next 7 days", "More than 7 days"];

    public RelayCommand RequestTasksCommand { get; }
    public RelayCommand RefreshCalculatedFieldsCommand { get; }
    public RelayCommand MarkCompleteCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand CloseTaskSidebarCommand { get; }
    public RelayCommand NewTrackItemCommand { get; }
    public RelayCommand SaveTrackItemsCommand { get; }
    public RelayCommand RemoveTrackItemCommand { get; }
    public RelayCommand RemoveTrackHistoryCommand { get; }
    public RelayCommand AddTrackOwnedCommand { get; }
    public RelayCommand UseTrackQuantityCommand { get; }
    public RelayCommand ReturnTrackQuantityCommand { get; }
    public RelayCommand RecordTrackChangeCommand { get; }
    public RelayCommand EditTrackItemCommand { get; }
    public RelayCommand OpenTrackRecordSidebarCommand { get; }
    public RelayCommand CloseTrackSidebarCommand { get; }

    public string[] TrackTypes { get; } = [TrackItemTypes.QuantityUsage, TrackItemTypes.ChangeCycle];
    public string[] TrackTypeFilters { get; } = [AllFilter, TrackItemTypes.QuantityUsage, TrackItemTypes.ChangeCycle];
    public string[] ChangeUnits { get; } = [ChangeIntervalUnits.Days, ChangeIntervalUnits.Weeks, ChangeIntervalUnits.Months, ChangeIntervalUnits.Years];

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
            IsTaskSidebarOpen = value is not null;
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
            TrackActionLocation = value?.CurrentUseLocation ?? string.Empty;
            TrackActionRemark = string.Empty;
            SelectedTrackHistory = null;
            OnPropertyChanged();
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
            TrackItemsView.Refresh();
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
            TrackItemsView.Refresh();
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
            var cachedTrackItems = await _fileStore.LoadTrackItemsAsync();
            ReplaceTrackItems(cachedTrackItems);
            _trackOptions = await _fileStore.LoadTrackOptionsAsync();
            TrackCategories = BuildFilterList(_trackOptions.Categories);
            TrackRemarks = new ObservableCollection<string>(_trackOptions.Remarks.Where(remark => !string.IsNullOrWhiteSpace(remark)));
            ResetFilters();
            AppLogger.Info($"Loaded {cachedTasks.Count} cached task(s) from {AppPaths.TaskCachePath}");
            AppLogger.Info($"Loaded {cachedTrackItems.Count} tracked item(s) from {AppPaths.TrackItemsPath}");
            Message = cachedTasks.Count == 0
                ? "No cached tasks. Press Request to retrieve from Google Sheet."
                : $"Loaded {cachedTasks.Count} task(s) and {cachedTrackItems.Count} tracked item(s).";
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
        var completionDate = CompletionDate ?? DateTime.Today;
        var remark = SelectedRemarkDraft;
        var config = new AppConfig
        {
            GoogleAppsScriptUrl = _config.GoogleAppsScriptUrl,
            ApiKey = _config.ApiKey,
            LogRetentionDays = _config.LogRetentionDays
        };

        IsMarkingComplete = true;
        Message = "Marking complete...";

        try
        {
            await SaveConfigAsync();

            taskToComplete.Completed = true;
            taskToComplete.Remark = remark;
            taskToComplete.LastExecutedDate = completionDate;
            taskToComplete.NotifyCalculatedFieldsChanged();
            SelectedTask = null;
            IsTaskSidebarOpen = false;
            SelectedRemarkDraft = string.Empty;
            await _fileStore.SaveTasksAsync(_tasks);
            RebuildFilterLists();
            TasksView.Refresh();
            AppLogger.Info($"Completed row {taskToComplete.RowNumber} and kept it in local cache as completed");
            Message = "Marked Completed locally. Sending to Google Sheet in the background; press Request when you want to refresh.";
            _ = CompleteInBackgroundAsync(config, taskToComplete, completionDate, remark);
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

    private async Task CompleteInBackgroundAsync(AppConfig config, SheetTask task, DateTime completionDate, string remark)
    {
        try
        {
            await _sheetClient.CompleteAsync(config, task, completionDate, remark, CancellationToken.None);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed background network/API call while marking complete", ex);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Message = $"Failed network/API call while marking complete: {ex.Message}. Log: {AppPaths.CurrentWarningErrorLogPath}";
            });
        }
    }

    private Task CloseTaskSidebarAsync()
    {
        IsTaskSidebarOpen = false;
        SelectedTask = null;
        SelectedRemarkDraft = string.Empty;
        return Task.CompletedTask;
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
        TrackItemsView.Refresh();
        await SaveTrackItemsCoreAsync($"Removed record for {SelectedTrackItem.Name}.");
    }

    private async Task AddTrackOwnedAsync()
    {
        if (SelectedTrackItem is null || TrackActionQuantity <= 0)
        {
            return;
        }

        SelectedTrackItem.CurrentUseLocation = TrackActionLocation;
        await SaveNewTrackRemarkAsync(TrackActionRemark);
        AddTrackHistory(SelectedTrackItem, "Owned", TrackActionQuantity);
        SelectedTrackItem.NotifyCalculatedFieldsChanged();
        TrackItemsView.Refresh();
        await SaveTrackItemsCoreAsync($"Added {TrackActionQuantity} owned for {SelectedTrackItem.Name}.");
    }

    private async Task UseTrackQuantityAsync()
    {
        if (SelectedTrackItem is null || TrackActionQuantity <= 0)
        {
            return;
        }

        if (TrackActionQuantity > SelectedTrackItem.LeftQuantity)
        {
            Message = $"Cannot use {TrackActionQuantity}; only {SelectedTrackItem.LeftQuantity} left.";
            return;
        }

        SelectedTrackItem.CurrentUseLocation = TrackActionLocation;
        SelectedTrackItem.StartUseDate ??= TrackActionDate ?? DateTime.Today;
        await SaveNewTrackRemarkAsync(TrackActionRemark);
        AddTrackHistory(SelectedTrackItem, "Used", TrackActionQuantity);
        SelectedTrackItem.NotifyCalculatedFieldsChanged();
        TrackItemsView.Refresh();
        await SaveTrackItemsCoreAsync($"Recorded {TrackActionQuantity} used for {SelectedTrackItem.Name}.");
    }

    private async Task ReturnTrackQuantityAsync()
    {
        if (SelectedTrackItem is null || TrackActionQuantity <= 0)
        {
            return;
        }

        if (TrackActionQuantity > SelectedTrackItem.UsedQuantity)
        {
            Message = $"Cannot put back {TrackActionQuantity}; only {SelectedTrackItem.UsedQuantity} used.";
            return;
        }

        await SaveNewTrackRemarkAsync(TrackActionRemark);
        AddTrackHistory(SelectedTrackItem, "Put Back", TrackActionQuantity);
        SelectedTrackItem.NotifyCalculatedFieldsChanged();
        TrackItemsView.Refresh();
        await SaveTrackItemsCoreAsync($"Put back {TrackActionQuantity} for {SelectedTrackItem.Name}.");
    }

    private async Task RecordTrackChangeAsync()
    {
        if (SelectedTrackItem is null)
        {
            return;
        }

        var changedDate = TrackActionDate ?? DateTime.Today;
        SelectedTrackItem.StartUseDate = changedDate;
        SelectedTrackItem.CurrentUseLocation = TrackActionLocation;
        await SaveNewTrackRemarkAsync(TrackActionRemark);
        AddTrackHistory(SelectedTrackItem, "Changed", 1);
        SelectedTrackItem.NotifyCalculatedFieldsChanged();
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
            UpdateTrackRemarkSuggestions(TrackActionRemark);
        }

        return Task.CompletedTask;
    }

    private Task CloseTrackSidebarAsync()
    {
        IsTrackDetailSidebarOpen = false;
        IsTrackRecordSidebarOpen = false;
        TrackItemDraft = null;
        _isNewTrackItemDraft = false;
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

    private void ReplaceTrackItems(IEnumerable<TrackItem> items)
    {
        _trackItems.Clear();
        foreach (var item in items)
        {
            item.RecalculateQuantitiesFromHistory();
            item.NotifyCalculatedFieldsChanged();
            _trackItems.Add(item);
        }

        RebuildTrackCategoryFilter();
        TrackItemsView.Refresh();
    }

    private async Task SaveTrackItemsCoreAsync(string successMessage)
    {
        try
        {
            await _fileStore.SaveTrackItemsAsync(_trackItems);
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

    private void AddTrackHistory(TrackItem item, string action, decimal quantity)
    {
        item.History.Insert(0, new TrackItemHistory
        {
            Date = TrackActionDate ?? DateTime.Today,
            Action = action,
            Quantity = quantity,
            Location = TrackActionLocation,
            Remark = TrackActionRemark
        });

        item.RecalculateQuantitiesFromHistory();
        item.NotifyCalculatedFieldsChanged();
        TrackActionRemark = string.Empty;
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
            Notes = source.Notes,
            History = new ObservableTrackHistory(source.History.Select(CloneTrackHistory))
        };
    }

    private static TrackItemHistory CloneTrackHistory(TrackItemHistory source)
    {
        return new TrackItemHistory
        {
            Date = source.Date,
            Action = source.Action,
            Quantity = source.Quantity,
            Location = source.Location,
            Remark = source.Remark
        };
    }

    private static void CopyTrackItem(TrackItem source, TrackItem target)
    {
        target.Name = source.Name;
        target.Category = source.Category;
        target.TrackType = source.TrackType;
        target.ChangeEvery = source.ChangeEvery;
        target.ChangeUnit = source.ChangeUnit;
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

        return Matches(CategoryFilter, task.Category)
            && Matches(TypeFilter, task.Type)
            && Matches(StatusFilter, task.Status)
            && MatchesDayLeft(task.DayLeft);
    }

    private bool FilterTrackItem(object item)
    {
        if (item is not TrackItem trackItem)
        {
            return false;
        }

        return Matches(TrackCategoryFilter, trackItem.Category)
            && Matches(TrackTypeFilter, trackItem.TrackType);
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
        return date.ToString("dd MMM yyyy");
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
        return !IsBusy && SelectedTask is not null && !SelectedTask.Completed;
    }

    private bool CanMutateSelectedTrackItem()
    {
        return SelectedTrackItem is not null;
    }

    private bool CanRemoveTrackHistory()
    {
        return SelectedTrackItem is not null && SelectedTrackHistory is not null;
    }

    private bool CanAddTrackOwned()
    {
        return SelectedTrackItem is not null && TrackActionQuantity > 0;
    }

    private bool CanUseTrackQuantity()
    {
        return SelectedTrackItem is not null
            && TrackActionQuantity > 0
            && TrackActionQuantity <= SelectedTrackItem.LeftQuantity;
    }

    private bool CanReturnTrackQuantity()
    {
        return SelectedTrackItem is not null
            && TrackActionQuantity > 0
            && TrackActionQuantity <= SelectedTrackItem.UsedQuantity;
    }

    private bool CanRecordTrackChange()
    {
        return SelectedTrackItem is not null && SelectedTrackItem.IsChangeCycle;
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
        RemoveTrackItemCommand.RaiseCanExecuteChanged();
        RemoveTrackHistoryCommand.RaiseCanExecuteChanged();
        AddTrackOwnedCommand.RaiseCanExecuteChanged();
        UseTrackQuantityCommand.RaiseCanExecuteChanged();
        ReturnTrackQuantityCommand.RaiseCanExecuteChanged();
        RecordTrackChangeCommand.RaiseCanExecuteChanged();
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
            or nameof(TrackItem.ChangeUnit))
        {
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
}
