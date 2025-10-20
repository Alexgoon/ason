using System.Threading.Tasks;

namespace Ason.Invocation;

internal interface IInvocationScheduler {
    Task<T> InvokeAsync<T>(Func<Task<T>> func);
    Task InvokeAsync(Func<Task> func);
}
