using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace AsonRemoteRunner;

public sealed class ScriptRunnerSessionManager {
    readonly ConcurrentDictionary<string, ScriptRunnerSession> _sessions = new();
    readonly IOptions<RemoteScriptRunnerOptions> _options;
    public ScriptRunnerSessionManager(IOptions<RemoteScriptRunnerOptions> options) { _options = options; }

    public ScriptRunnerSession Create(string connectionId, AsonRunner.ExecutionMode mode, string? dockerImage, ILogger logger) {
        ScriptRunnerSession session = mode == AsonRunner.ExecutionMode.InProcess
            ? new InProcessRunnerSession(connectionId, logger)
            : new ProcessRunnerSession(connectionId, mode, dockerImage, logger);
        if (!_sessions.TryAdd(connectionId, session)) {
            session.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw new InvalidOperationException("Session already exists for connection " + connectionId);
        }
        return session;
    }

    public bool TryGet(string connectionId, out ScriptRunnerSession session) => _sessions.TryGetValue(connectionId, out session!);
    public async Task RemoveAsync(string connectionId) { if (_sessions.TryRemove(connectionId, out var s)) await s.DisposeAsync(); }
    public IEnumerable<ScriptRunnerSession> All => _sessions.Values;
    public TimeSpan IdleTimeout => _options.Value.IdleTimeout;
}

internal sealed class SessionCleanupService : BackgroundService {
    readonly ScriptRunnerSessionManager _manager; readonly ILogger<SessionCleanupService> _logger;
    public SessionCleanupService(ScriptRunnerSessionManager manager, ILogger<SessionCleanupService> logger) { _manager = manager; _logger = logger; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                var cutoff = DateTime.UtcNow - _manager.IdleTimeout;
                foreach (var s in _manager.All)
                    if (s.LastActivityUtc < cutoff) {
                        _logger.LogInformation("Disposing idle ScriptRunner session {Id}", s.ConnectionId);
                        await _manager.RemoveAsync(s.ConnectionId);
                    }
            } catch (Exception ex) { _logger.LogError(ex, "Cleanup loop error"); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

public sealed class RemoteScriptRunnerOptions {
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

public static class RemoteRunnerServiceExtensions {
    public static IServiceCollection AddAsonScriptRunner(this IServiceCollection services, Action<RemoteScriptRunnerOptions>? configure = null) {
        services.AddSignalR();
        if (configure != null) services.Configure(configure); else services.Configure<RemoteScriptRunnerOptions>(_ => { });
        services.AddSingleton<ScriptRunnerSessionManager>();
        services.AddHostedService<SessionCleanupService>();
        return services;
    }

    public static HubEndpointConventionBuilder MapAson(this IEndpointRouteBuilder endpoints, string pattern = "/scriptRunnerHub", bool requireAuthorization = false) {
        var builder = endpoints.MapHub<ScriptRunnerHub>(pattern);
        if (requireAuthorization) builder.RequireAuthorization();
        return builder;
    }
}