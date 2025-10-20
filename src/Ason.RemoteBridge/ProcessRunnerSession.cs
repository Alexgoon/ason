using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using AsonRunner;

namespace AsonRemoteRunner;

internal sealed class ProcessRunnerSession : ScriptRunnerSession {
    readonly ExecutionMode _mode; readonly string? _dockerImage; IClientProxy? _client; ScriptRunnerProcessHost? _host;
    public ProcessRunnerSession(string id, ExecutionMode mode, string? dockerImage, ILogger logger) : base(id, logger) { _mode = mode; _dockerImage = dockerImage; }
    public override void Start(IClientProxy caller) {
        _client = caller; if (_mode == ExecutionMode.InProcess) throw new InvalidOperationException();
        _host = new ScriptRunnerProcessHost(_mode, _dockerImage, _logger);
        _host.LineReceived += line => _client!.SendAsync("OnRunnerMessage", line);
        _host.ProcessExited += reason => _client!.SendAsync("OnRunnerClosed", reason);
        _ = _host.StartAsync();
    }
    public override async Task SendAsync(string line) { Touch(); if (_host == null) return; try { await _host.SendLineAsync(line); } catch (Exception ex) { _logger.LogError(ex, "Error writing to runner"); } }
    public override async ValueTask DisposeAsync() { if (_host != null) await _host.DisposeAsync(); }
}
