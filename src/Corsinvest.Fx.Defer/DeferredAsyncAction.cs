namespace Corsinvest.Fx.Defer;

internal sealed class DeferredAsyncAction(Func<Task> asyncAction) : IAsyncDisposable
{
    private Func<Task>? _asyncAction = asyncAction;

    public async ValueTask DisposeAsync()
    {
        // Thread-safe: Exchange ensures only one thread executes the async action
        var asyncAction = Interlocked.Exchange(ref _asyncAction, null);
        if (asyncAction == null) { return; }

        try
        {
            await asyncAction().ConfigureAwait(false);
        }
        catch
        {
            // Suppress exceptions to allow other defers to execute
        }
    }
}