using System.Text.Json;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Ason;

namespace Ason.Tests.Infrastructure;

// Lightweight fake runner facade used only in tests where script execution pipeline is simulated
internal sealed class FakeRunnerClientFacade {
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    public List<string> ExecutedScripts { get; } = new();
    public ConcurrentDictionary<string, OperatorBase> Map { get; }
    public FakeRunnerClientFacade(ConcurrentDictionary<string, OperatorBase> map) { Map = map; }

    public Task<JsonElement?> ExecuteAsync(string code) {
        ExecutedScripts.Add(code);
        var m = Regex.Match(code, @"return\s+([0-9]+)\s*;");
        if (m.Success) {
            int val = int.Parse(m.Groups[1].Value);
            return Task.FromResult<JsonElement?>(JsonSerializer.SerializeToElement(val, _json));
        }
        return Task.FromResult<JsonElement?>(null);
    }
}

