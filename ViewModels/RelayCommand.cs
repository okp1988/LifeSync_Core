using System.Windows.Input;

namespace LifeSyncTaskClient.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Func<bool>? _canExecute;
    private readonly Func<Task> _execute;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter) => await _execute();

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
