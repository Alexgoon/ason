namespace Ason.Invocation;

internal interface IOperatorInvoker {
    Task<object?> InvokeAsync(string target, string method, string? handleId, object?[] args);
}
