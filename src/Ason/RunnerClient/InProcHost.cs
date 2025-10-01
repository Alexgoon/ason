using AsonHostInterop;
using System.Text.Json;

namespace Ason;
internal sealed class InProcHost : IHostBridge {
    readonly RunnerClient _owner;
    public InProcHost(RunnerClient owner) { _owner = owner; }

    public Task LogAsync(string level, string message) {
        var lvl = LogHelper.ParseLogLevel(level);
        _owner.RaiseLogEvent(lvl, message);
        return Task.CompletedTask;
    }
    public async Task<T?> InvokeAsync<T>(string target, string method, object?[]? args = null, string? handleId = null) {
        object?[] callArgs = args ?? Array.Empty<object?>();
        // Use internal operator invoker via public method wrapper (new abstraction)
        object? result = await _owner.InternalInvokeOperatorAsync(target, method, handleId, callArgs).ConfigureAwait(false);
        if (result == null) return default;
        if (typeof(T) == typeof(object)) return (T)result;
        if (result is JsonElement je && typeof(T) == typeof(JsonElement)) return (T)(object)je;
        if (typeof(T) == typeof(string) && result is string s) return (T)(object)s;
        var json = JsonSerializer.SerializeToElement(result, _owner.JsonOptions);
        if (typeof(T) == typeof(JsonElement)) return (T)(object)json;
        return json.Deserialize<T>(_owner.JsonOptions)!;
    }

    public async Task<T?> InvokeMcpAsync<T>(string server, string tool, IDictionary<string, object?>? arguments = null) {
        var result = await _owner.InternalInvokeMcpAsync(server, tool, arguments).ConfigureAwait(false);
        if (result == null) return default;
        if (result is JsonElement je && typeof(T) == typeof(JsonElement)) return (T)(object)je;
        if (typeof(T) == typeof(string) && result is string s) return (T)(object)s;
        var json = JsonSerializer.SerializeToElement(result, _owner.JsonOptions);
        if (typeof(T) == typeof(JsonElement)) return (T)(object)json;
        return json.Deserialize<T>(_owner.JsonOptions)!;
    }
}
