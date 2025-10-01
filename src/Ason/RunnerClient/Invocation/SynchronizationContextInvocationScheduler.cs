namespace Ason.Invocation;

internal sealed class SynchronizationContextInvocationScheduler : IInvocationScheduler {
    readonly SynchronizationContext _context;
    public SynchronizationContextInvocationScheduler(SynchronizationContext context) { _context = context; }

    public Task<T> InvokeAsync<T>(Func<Task<T>> func) {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _context.Post(async _ => {
            try { var result = await func().ConfigureAwait(false); tcs.TrySetResult(result); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }, null);
        return tcs.Task;
    }

    public Task InvokeAsync(Func<Task> func) {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _context.Post(async _ => {
            try { await func().ConfigureAwait(false); tcs.TrySetResult(null); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }, null);
        return tcs.Task;
    }
}
