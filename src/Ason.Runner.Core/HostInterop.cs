namespace AsonHostInterop;
public interface IHostBridge {
    Task<T?> InvokeAsync<T>(string target, string method, object?[]? args = null, string? handleId = null);
    Task<T?> InvokeMcpAsync<T>(string server, string tool, IDictionary<string, object?>? arguments = null);
    Task LogAsync(string level, string message);
}
