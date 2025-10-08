namespace Ason.Invocation;

internal interface IMcpToolInvoker {
    Task<object?> InvokeAsync(string server, string tool, IDictionary<string, object?>? arguments);
}
