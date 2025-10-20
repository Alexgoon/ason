using System.Text.Json;

namespace Ason;
public static class JsonElementExtensions {
    public static string? TryGetWithFallback(this JsonElement element, string name, string? fallback = null) {
        return element.TryGetProperty(name, out var v) ? v.GetString() : fallback;
    }
    public static object? DeserializeToObject(this JsonElement element) {
        return element.ValueKind switch {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : (element.TryGetDouble(out var d) ? d : null),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element
        };
    }
}
