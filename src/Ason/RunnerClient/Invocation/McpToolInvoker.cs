using System.Collections.Concurrent;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ModelContextProtocol.Client;

namespace Ason.Invocation;

internal sealed class McpToolInvoker : IMcpToolInvoker {
    readonly ConcurrentDictionary<string, IMcpClient> _clients;
    readonly JsonSerializerOptions _jsonOptions;

    public McpToolInvoker(ConcurrentDictionary<string, IMcpClient> clients, JsonSerializerOptions options) { _clients = clients; _jsonOptions = options; }

    public async Task<object?> InvokeAsync(string server, string tool, IDictionary<string, object?>? arguments) {
        if (!_clients.TryGetValue(server, out var client)) throw new InvalidOperationException($"MCP server not registered: {server}");
        var dict = arguments is null ? new Dictionary<string, object?>() : new Dictionary<string, object?>(arguments);
        var callResult = await client.CallToolAsync(tool, dict).ConfigureAwait(false);
        if (callResult?.Content is { Count: > 0 }) {
            var structured = callResult.Content.FirstOrDefault(c => string.Equals(c.Type, "structured", StringComparison.OrdinalIgnoreCase));
            if (structured is not null) {
                try {
                    var json = JsonSerializer.Serialize(structured, _jsonOptions);
                    return JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
                } catch { }
            }
            var textBlock = callResult.Content.FirstOrDefault(c => c.Type == "text");
            if (textBlock is not null) {
                var textProp = textBlock.GetType().GetProperty("Text");
                if (textProp != null) return textProp.GetValue(textBlock) as string;
            }
            return callResult.Content.Select(c => new { c.Type, Value = c.ToString() }).ToArray();
        }
        return null;
    }
}
