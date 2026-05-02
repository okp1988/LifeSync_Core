using System.Windows;
using System.Windows.Input;
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
        if (e.Key != Key.Escape || !_viewModel.IsTaskSidebarOpen || !_viewModel.CloseTaskSidebarCommand.CanExecute(null))
        {
            return;
        }

        _viewModel.CloseTaskSidebarCommand.Execute(null);
        e.Handled = true;
    }
}
