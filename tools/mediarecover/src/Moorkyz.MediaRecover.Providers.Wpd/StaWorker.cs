using System.Collections.Concurrent;

namespace Moorkyz.Tools.MediaRecover.Providers.Wpd;

internal sealed class StaWorker : IDisposable
{
    private readonly BlockingCollection<Action> actions = new();
    private readonly Thread thread;
    public StaWorker()
    {
        thread = new Thread(() => { foreach (var action in actions.GetConsumingEnumerable()) action(); }) { IsBackground = true, Name = "MediaRecover WPD STA" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
    }
    public Task<T> Invoke<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        actions.Add(() => { try { completion.SetResult(action()); } catch (Exception e) { completion.SetException(e); } });
        return completion.Task;
    }
    public void Dispose()
    {
        actions.CompleteAdding();
        if (thread.Join(TimeSpan.FromSeconds(5))) actions.Dispose();
    }
}
