using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LifeSyncTaskClient.ViewModels;

namespace LifeSyncTaskClient;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (_viewModel.IsTaskSidebarOpen && _viewModel.CloseTaskSidebarCommand.CanExecute(null))
        {
            _viewModel.CloseTaskSidebarCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if ((_viewModel.IsTrackDetailSidebarOpen || _viewModel.IsTrackRecordSidebarOpen)
            && _viewModel.CloseTrackSidebarCommand.CanExecute(null))
        {
            _viewModel.CloseTrackSidebarCommand.Execute(null);
            e.Handled = true;
        }
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
