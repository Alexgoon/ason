using System.Text.Json;
using System.Text.Json.Serialization;
namespace AsonRunner.Protocol;
public static class RunnerMessageSerializer {
    static readonly JsonSerializerOptions Options;
    static RunnerMessageSerializer() {
        Options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false
        };
        Options.Converters.Add(new JsonStringEnumConverter());
    }
    public static string Serialize(IRunnerMessage message) => JsonSerializer.Serialize(message, message.GetType(), Options);
    public static IRunnerMessage? Deserialize(string json) {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("type", out var tProp)) return null;
        var type = tProp.GetString();
        if (string.IsNullOrEmpty(type)) return null;
        return type switch {
            "exec" => new ExecRequest(root.GetProperty("id").GetString()!, root.GetProperty("code").GetString() ?? string.Empty),
            "execResult" => new ExecResult(root.GetProperty("id").GetString()!, root.TryGetProperty("result", out var r) ? (object?)r.Clone() : null, root.TryGetProperty("error", out var e) ? e.GetString() : null),
            "invoke" => new InvokeRequest(root.GetProperty("id").GetString()!, root.GetProperty("target").GetString()!, root.GetProperty("method").GetString()!,
                root.TryGetProperty("args", out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Select(x => (object?)x.Deserialize<object>(Options)).ToArray() : Array.Empty<object?>(),
                root.TryGetProperty("handleId", out var h) ? h.GetString() : null),
            "invokeResult" => new InvokeResult(root.GetProperty("id").GetString()!, root.TryGetProperty("result", out var ir) ? (object?)ir.Clone() : null, root.TryGetProperty("error", out var ie) ? ie.GetString() : null),
            "invokeMcp" => new McpInvokeRequest(root.GetProperty("id").GetString()!, root.GetProperty("server").GetString()!, root.GetProperty("tool").GetString()!,
                root.TryGetProperty("arguments", out var arg) && arg.ValueKind == JsonValueKind.Object ? arg.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Deserialize<object>(Options)) : null),
            "log" => new LogMessage(root.GetProperty("id").GetString()!, root.TryGetProperty("level", out var lv) ? lv.GetString() ?? "Information" : "Information", root.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty, root.TryGetProperty("source", out var s) ? s.GetString() : null, root.TryGetProperty("exception", out var ex) ? ex.GetString() : null),
            _ => null
        };
    }
}
