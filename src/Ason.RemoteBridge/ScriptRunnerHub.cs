using AsonRunner;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AsonRemoteRunner;

public sealed class ScriptRunnerHub : Hub {
    readonly ScriptRunnerSessionManager _manager; readonly ILogger<ScriptRunnerHub> _logger;
    public ScriptRunnerHub(ScriptRunnerSessionManager manager, ILogger<ScriptRunnerHub> logger) { _manager = manager; _logger = logger; }
    public override async Task OnDisconnectedAsync(Exception? exception) { await _manager.RemoveAsync(Context.ConnectionId); await base.OnDisconnectedAsync(exception); }
    public Task StartRunner(int mode, string? dockerImage) {
        var em = (ExecutionMode)mode; var session = _manager.Create(Context.ConnectionId, em, dockerImage, _logger); session.Start(Clients.Caller);
        _logger.LogInformation("Started runner for {Conn} mode={Mode}", Context.ConnectionId, em);
        return Task.CompletedTask;
    }
    public async Task Send(string line) { if (_manager.TryGet(Context.ConnectionId, out var s)) await s.SendAsync(line); }
}