using System.Windows.Input;

namespace Images.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : new Predicate<object?>(_ => canExecute()))
    {
    }

    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        ExecuteAsync(parameter);
    }

    private async void ExecuteAsync(object? parameter)
    {
        try
        {
            await _execute(parameter);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public void Raise() => CommandManager.InvalidateRequerySuggested();
}
