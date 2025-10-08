using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using AsonRunner;
using Ason; // for RunnerClient

namespace AsonRemoteRunner;

internal sealed class InProcessRunnerSession : ScriptRunnerSession {
    readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };
    IClientProxy? _client;
    readonly SemaphoreSlim _gate = new(1, 1);
    private RunnerClient? _runnerClient;

    public InProcessRunnerSession(string id, ILogger logger) : base(id, logger) { }
    public override void Start(IClientProxy caller) { _client = caller; }

    public void AttachRunnerClient(RunnerClient runnerClient) => _runnerClient = runnerClient ?? throw new ArgumentNullException(nameof(runnerClient));

    public override async Task SendAsync(string line) {
        Touch();
        if (string.IsNullOrWhiteSpace(line)) return;
        try {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            var id = root.GetProperty("id").GetString();
            if (type == "exec") {
                var code = root.GetProperty("code").GetString() ?? string.Empty;
                await LogAsync("Information", $"Exec received. Id={id}, length={code.Length}");
                _ = Task.Run(() => HandleExecAsync(id!, code));
            }
        } catch (Exception ex) {
            await LogAsync("Error", "In-process runner parse failed: " + ex.Message, ex);
        }
    }

    async Task HandleExecAsync(string id, string code) {
        try {
            await LogAsync("Information", $"Evaluating script. Id={id}");
            var bridge = new InProcBridge(line => _client!.SendAsync("OnRunnerMessage", line), _json, this);
            var result = await ScriptExecutor.EvaluateAsync(code, bridge).ConfigureAwait(false);
            JsonElement? payload = null;
            if (result is not null) payload = JsonSerializer.SerializeToElement(result, _json);
            var obj = new { id, type = "execResult", result = payload };
            var json = JsonSerializer.Serialize(obj, _json);
            await _client!.SendAsync("OnRunnerMessage", json);
            await LogAsync("Information", $"Exec completed. Id={id}");
        } catch (Exception ex) {
            await LogAsync("Error", $"Exec failed. Id={id}", ex);
            var err = new { id, type = "execResult", error = ex.ToString() };
            var json = JsonSerializer.Serialize(err, _json);
            await _client!.SendAsync("OnRunnerMessage", json);
        }
    }

    internal Task LogAsync(string level, string message, Exception? ex = null, [System.Runtime.CompilerServices.CallerMemberName] string? source = null) {
        var log = new { id = Guid.NewGuid().ToString("N"), type = "log", level, message, source, exception = ex?.ToString() };
        var json = JsonSerializer.Serialize(log, _json);
        return _client!.SendAsync("OnRunnerMessage", json);
    }

    public override ValueTask DisposeAsync() { _gate.Dispose(); return ValueTask.CompletedTask; }

    private sealed class InProcBridge : AsonHostInterop.IHostBridge {
        readonly Func<string, Task> _send; readonly JsonSerializerOptions _json; readonly InProcessRunnerSession _session;
        public InProcBridge(Func<string, Task> send, JsonSerializerOptions json, InProcessRunnerSession session) { _send = send; _json = json; _session = session; }
        public Task LogAsync(string level, string message) => _session.LogAsync(level, message);

        public async Task<T?> InvokeAsync<T>(string target, string method, object?[]? args = null, string? handleId = null) {
            var runner = _session._runnerClient ?? throw new InvalidOperationException("RunnerClient not attached to InProcessRunnerSession. Call AttachRunnerClient.");
            return await runner.InvokeOperatorAsync<T>(target, method, handleId, args ?? Array.Empty<object?>()).ConfigureAwait(false);
        }

        public async Task<T?> InvokeMcpAsync<T>(string server, string tool, IDictionary<string, object?>? arguments = null) {
            var runner = _session._runnerClient ?? throw new InvalidOperationException("RunnerClient not attached to InProcessRunnerSession. Call AttachRunnerClient.");
            return await runner.InvokeMcpToolAsync<T>(server, tool, arguments).ConfigureAwait(false);
        }
    }
}
