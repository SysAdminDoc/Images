using Images.ViewModels;

namespace Images.Tests;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task ExecuteAsync_OperationCanceledException_IsSilentlySwallowed()
    {
        var faulted = false;
        void OnFault(object? s, CommandFaultedEventArgs e) => faulted = true;
        AsyncRelayCommand.CommandFaulted += OnFault;
        try
        {
            var cmd = new AsyncRelayCommand(() => throw new OperationCanceledException());
            cmd.Execute(null);
            await Task.Delay(50);
            Assert.False(faulted);
        }
        finally
        {
            AsyncRelayCommand.CommandFaulted -= OnFault;
        }
    }

    [Fact]
    public async Task ExecuteAsync_NonCancellationException_RaisesCommandFaulted()
    {
        Exception? captured = null;
        void OnFault(object? s, CommandFaultedEventArgs e) => captured = e.Exception;
        AsyncRelayCommand.CommandFaulted += OnFault;
        try
        {
            var cmd = new AsyncRelayCommand(() => throw new InvalidOperationException("boom"));
            cmd.Execute(null);
            await Task.Delay(50);
            Assert.NotNull(captured);
            Assert.IsType<InvalidOperationException>(captured);
            Assert.Equal("boom", captured.Message);
        }
        finally
        {
            AsyncRelayCommand.CommandFaulted -= OnFault;
        }
    }

    [Fact]
    public void CanExecute_DefaultsToTrue()
    {
        var cmd = new AsyncRelayCommand(() => Task.CompletedTask);
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void CanExecute_RespectsPredicateWhenFalse()
    {
        var cmd = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
        Assert.False(cmd.CanExecute(null));
    }

    [Fact]
    public async Task Execute_WhenCanExecuteIsFalse_DoesNotInvoke()
    {
        var invoked = false;
        var cmd = new AsyncRelayCommand(() => { invoked = true; return Task.CompletedTask; }, () => false);
        cmd.Execute(null);
        await Task.Delay(50);
        Assert.False(invoked);
    }
}
