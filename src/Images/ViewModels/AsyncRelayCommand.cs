using System.Windows.Input;
using Images.Services;
using Microsoft.Extensions.Logging;

namespace Images.ViewModels;

public sealed class CommandFaultedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private int _isExecuting;

    public static event EventHandler<CommandFaultedEventArgs>? CommandFaulted;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : new Predicate<object?>(_ => canExecute()))
    {
    }

    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) =>
        Volatile.Read(ref _isExecuting) == 0 && (_canExecute?.Invoke(parameter) ?? true);

    public void Execute(object? parameter)
    {
        if (_canExecute?.Invoke(parameter) == false) return;
        if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0) return;

        CommandManager.InvalidateRequerySuggested();
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
        catch (Exception ex)
        {
            var handler = CommandFaulted;
            if (handler is not null)
                handler(this, new CommandFaultedEventArgs(ex));
            else
                // No live fault handler (e.g. the view model was disposed during shutdown while a
                // command was still in flight). Rethrowing here escapes an async-void continuation
                // and hard-crashes the process; log and observe the fault instead.
                Log.For<AsyncRelayCommand>().LogError(ex, "Async command faulted with no fault handler subscribed.");
        }
        finally
        {
            Interlocked.Exchange(ref _isExecuting, 0);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public void Raise() => CommandManager.InvalidateRequerySuggested();
}
