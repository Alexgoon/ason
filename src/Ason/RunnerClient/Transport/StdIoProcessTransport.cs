using AsonRunner;
using Microsoft.Extensions.Logging;

namespace Ason.Transport;

internal sealed class StdIoProcessTransport : IRunnerTransport, IAsyncDisposable {
    readonly ExecutionMode _mode; readonly string _dockerImage; readonly ScriptRunnerProcessHost _host;
    public bool IsStarted { get; private set; }
    public event Action<string>? LineReceived;
    public event Action<string>? Closed;

    public StdIoProcessTransport(ExecutionMode mode, string dockerImage, string? runnerExecutablePath = null) {
        _mode = mode; _dockerImage = dockerImage;
        _host = new ScriptRunnerProcessHost(_mode, _dockerImage, logger: null, runnerExecutablePath);
        _host.LineReceived += line => { if (!string.IsNullOrWhiteSpace(line)) LineReceived?.Invoke(line); return Task.CompletedTask; };
        _host.ProcessExited += reason => { Closed?.Invoke(reason); return Task.CompletedTask; };
    }

    public async Task StartAsync() { if (IsStarted) return; await _host.StartAsync().ConfigureAwait(false); IsStarted = true; }

    public Task SendAsync(string jsonLine) {
        if (!IsStarted) throw new InvalidOperationException("Transport not started");
        return _host.SendLineAsync(jsonLine);
    }

    public async Task StopAsync() { if (!IsStarted) return; await _host.DisposeAsync(); IsStarted = false; }

    public async ValueTask DisposeAsync() { await StopAsync(); }
}
