using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
using LifeSyncTaskClient.ViewModels;

namespace LifeSyncTaskClient;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly Dictionary<DataGridColumn, DataGridLength> _defaultColumnWidths = [];

    public MainWindow()
    {
        InitializeComponent();
        CaptureDefaultColumnWidths();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateTrackHistoryColumnVisibility();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        UpdateTrackHistoryColumnVisibility();
        ResetAllGridColumnWidths();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.HasSelectedTrackItemExpiry))
        {
            UpdateTrackHistoryColumnVisibility();
        }
    }

    private void UpdateTrackHistoryColumnVisibility()
    {
        TrackHistoryExpiryColumn.Visibility = _viewModel.HasSelectedTrackItemExpiry
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        _viewModel.CloseAllPopupsAndSidebars();
        ClearKeyboardFocus();
        ResetAllGridColumnWidths();
        e.Handled = true;
    }

    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != MainTabs)
        {
            return;
        }

        _viewModel.CloseAllPopupsAndSidebars();
        ClearKeyboardFocus();
        ResetAllGridColumnWidths();
    }

    private void DataGrid_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(ResetAllGridColumnWidths, DispatcherPriority.Background);
        }
    }

    private void DatePicker_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is DatePicker datePicker)
        {
            DataObject.RemovePastingHandler(datePicker, DatePicker_Pasting);
            DataObject.AddPastingHandler(datePicker, DatePicker_Pasting);
        }
    }

    private void DatePicker_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = true;
    }

    private void DatePicker_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (FindAncestor<DatePickerTextBox>((DependencyObject)e.OriginalSource) is null)
        {
            return;
        }

        if (e.Key is Key.Back or Key.Delete or Key.Space)
        {
            e.Handled = true;
        }
    }

    private void DatePicker_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
    }

    private void TasksGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (!string.Equals(e.Column.Header?.ToString(), "Day Left", StringComparison.Ordinal))
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;
        var nextDirection = GetNextSortDirection(e.Column.SortDirection);
        ClearSortDirections(TasksGrid);
        e.Column.SortDirection = nextDirection;
        _viewModel.ApplyTaskDayLeftSort(nextDirection);
    }

    private void TrackItemsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (!string.Equals(e.Column.Header?.ToString(), "Left", StringComparison.Ordinal))
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;
        var nextDirection = GetNextSortDirection(e.Column.SortDirection);
        ClearSortDirections(TrackItemsGrid);
        e.Column.SortDirection = nextDirection;
        _viewModel.ApplyTrackLeftSort(nextDirection);
    }

    private void TrackItemsGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.OpenTrackRecordSidebarCommand.CanExecute(null))
        {
            return;
        }

        _viewModel.OpenTrackRecordSidebarCommand.Execute(null);
    }

    private void TasksGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource) is not null)
        {
            return;
        }

        _viewModel.ClearTaskSelection();
        _viewModel.CloseTrackSidebarCommand.Execute(null);
    }

    private void TasksGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource) is null)
        {
            return;
        }

        if (!_viewModel.OpenSelectedTaskSidebarCommand.CanExecute(null))
        {
            return;
        }

        _viewModel.OpenSelectedTaskSidebarCommand.Execute(null);
        e.Handled = true;
    }

    private void TaskSummaryList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        TaskSummaryScrollViewer.ScrollToVerticalOffset(TaskSummaryScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void TrackItemsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource) is not null)
        {
            return;
        }

        _viewModel.ClearTrackSelection();
    }

    private void TrackHistoryGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource) is not null)
        {
            return;
        }

        _viewModel.ClearSelectedTrackHistory();
    }

    private void CaptureDefaultColumnWidths()
    {
        foreach (var grid in EnumerateManagedGrids())
        {
            foreach (var column in grid.Columns)
            {
                _defaultColumnWidths[column] = column.Width;
            }
        }
    }

    private void ResetAllGridColumnWidths()
    {
        foreach (var (column, width) in _defaultColumnWidths)
        {
            column.Width = width;
        }
    }

    private void ResetAllGridState()
    {
        foreach (var grid in EnumerateManagedGrids())
        {
            ClearSortDirections(grid);
        }

        ResetAllGridColumnWidths();
    }

    private static ListSortDirection? GetNextSortDirection(ListSortDirection? currentDirection)
    {
        return currentDirection switch
        {
            null => ListSortDirection.Ascending,
            ListSortDirection.Ascending => ListSortDirection.Descending,
            _ => null
        };
    }

    private static void ClearSortDirections(DataGrid grid)
    {
        foreach (var column in grid.Columns)
        {
            column.SortDirection = null;
        }
    }

    private IEnumerable<DataGrid> EnumerateManagedGrids()
    {
        yield return TasksGrid;
        yield return TrackItemsGrid;
        yield return TrackHistoryGrid;
    }

    private void ClearKeyboardFocus()
    {
        Keyboard.ClearFocus();
        FocusManager.SetFocusedElement(this, null);
        Focus();
    }

    private static T? FindAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
