using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AsonRemoteRunner;

public abstract class ScriptRunnerSession : IAsyncDisposable {
    protected readonly ILogger _logger;
    public string ConnectionId { get; }
    public DateTime LastActivityUtc { get; protected set; } = DateTime.UtcNow;

    protected ScriptRunnerSession(string connectionId, ILogger logger) {
        ConnectionId = connectionId;
        _logger = logger;
    }

    public abstract void Start(IClientProxy caller);
    public abstract Task SendAsync(string line);
    public abstract ValueTask DisposeAsync();

    protected void Touch() => LastActivityUtc = DateTime.UtcNow;
}
