using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LifeSyncTaskClient.Models;
using LifeSyncTaskClient.ViewModels;

namespace LifeSyncTaskClient;

public partial class MainWindow : Window
{
    private const double CompactTasksGridWidth = 1120;
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTasksGridResponsiveColumns(ActualWidth);
        await _viewModel.InitializeAsync();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTasksGridResponsiveColumns(e.NewSize.Width);
    }

    private void UpdateTasksGridResponsiveColumns(double windowWidth)
    {
        var isCompact = windowWidth < CompactTasksGridWidth;
        var detailColumnVisibility = isCompact
            ? Visibility.Collapsed
            : Visibility.Visible;

        LastExecutedDateColumn.Visibility = detailColumnVisibility;
        RemarkColumn.Visibility = detailColumnVisibility;
        TaskNameColumn.Width = isCompact
            ? new DataGridLength(1, DataGridLengthUnitType.Star)
            : new DataGridLength(205);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        _viewModel.CloseAllPopupsAndSidebars();
        ClearKeyboardFocus();
        e.Handled = true;
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

    private void TasksGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource) is not null)
        {
            return;
        }

        _viewModel.ClearTaskSelection();
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

    private void TasksGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        e.Row.DetailsVisibility = e.Row.Item is SheetTask { IsExpanded: true }
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void TaskExpander_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle || toggle.DataContext is not SheetTask task)
        {
            return;
        }

        task.IsExpanded = toggle.IsChecked == true;
        if (FindAncestor<DataGridRow>(toggle) is { } row)
        {
            row.DetailsVisibility = task.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        }
        _viewModel.NotifyTaskExpansionChanged();
        e.Handled = true;
    }

    private void PriorityGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource) is null
            || !_viewModel.OpenPriorityDetailCommand.CanExecute(null))
        {
            return;
        }

        _viewModel.OpenPriorityDetailCommand.Execute(null);
        e.Handled = true;
    }

    private void TaskSummaryList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        TaskSummaryScrollViewer.ScrollToVerticalOffset(TaskSummaryScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
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
