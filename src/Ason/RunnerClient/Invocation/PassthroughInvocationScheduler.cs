namespace Ason.Invocation;

internal sealed class PassthroughInvocationScheduler : IInvocationScheduler {
    public Task<T> InvokeAsync<T>(Func<Task<T>> func) => func();
    public Task InvokeAsync(Func<Task> func) => func();
}
