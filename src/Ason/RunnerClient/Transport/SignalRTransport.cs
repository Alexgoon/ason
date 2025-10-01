using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using AsonRunner;

namespace Ason.Transport;

internal sealed class SignalRTransport : IRunnerTransport, IAsyncDisposable {
    readonly string _baseUrl; readonly ExecutionMode _mode; readonly string _dockerImage; readonly Action<LogLevel,string,string?,string?>? _onLog;
    HubConnection? _hub;
    public bool IsStarted { get; private set; }
    public event Action<string>? LineReceived;
    public event Action<string>? Closed;

    public SignalRTransport(string baseUrl, ExecutionMode mode, string dockerImage, Action<LogLevel,string,string?,string?>? onLog = null) {
        _baseUrl = baseUrl; _mode = mode; _dockerImage = dockerImage; _onLog = onLog;
    }

    public async Task StartAsync() {
        if (IsStarted) return;
        _hub = new HubConnectionBuilder()
            .WithUrl(_baseUrl.TrimEnd('/') + "/scriptRunnerHub")
            .WithAutomaticReconnect()
            .Build();
        _hub.On<string>("OnRunnerMessage", line => { if (!string.IsNullOrWhiteSpace(line)) LineReceived?.Invoke(line); });
        _hub.On<string>("OnRunnerClosed", reason => Closed?.Invoke(reason));
        await _hub.StartAsync().ConfigureAwait(false);
        await _hub.InvokeAsync("StartRunner", (int)_mode, _dockerImage).ConfigureAwait(false);
        _onLog?.Invoke(LogLevel.Information, "Connected to remote ScriptRunner", null, nameof(SignalRTransport));
        IsStarted = true;
    }

    public Task SendAsync(string jsonLine) {
        if (!IsStarted || _hub == null) throw new InvalidOperationException("SignalR transport not started");
        return _hub.InvokeAsync("Send", jsonLine);
    }

    public async Task StopAsync() {
        if (!IsStarted) return; IsStarted = false;
        if (_hub != null) { try { await _hub.DisposeAsync(); } catch { } finally { _hub = null; } }
    }

    public async ValueTask DisposeAsync() { await StopAsync(); }
}
