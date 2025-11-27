namespace Corsinvest.Fx.Defer;

internal sealed class DeferredAction(Action action) : IDisposable
{
    private Action? _action = action;

    public void Dispose()
    {
        // Thread-safe: Exchange ensures only one thread executes the action
        var action = Interlocked.Exchange(ref _action, null);
        if (action == null) { return; }

        try
        {
            action();
        }
        catch
        {
            // Suppress exceptions to allow other defers to execute
        }
    }
}
