using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.ComponentModel; // CancelEventArgs
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client; // MCP
using AsonRunner;
using Ason.Transport;
using Ason.Invocation;
using AsonRunner.Protocol;

namespace Ason;
public sealed class RunnerClient(ConcurrentDictionary<string, OperatorBase> handleToObject, SynchronizationContext? synchronizationContext) { // changed to public
    public readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    readonly List<Assembly> _assemblies = new() { typeof(ProxySerializer).Assembly };
    readonly SynchronizationContext? _capturedContext = synchronizationContext;
    IInvocationScheduler _scheduler = new PassthroughInvocationScheduler();
    IOperatorInvoker? _operatorInvoker;
    IMcpToolInvoker? _mcpInvoker;

    IOperatorMethodCache? _methodCache; // provided externally when ready
    IOperatorMethodCache? _invokerCache; // cache used to build current operator invoker
    public IOperatorMethodCache? MethodCache { get => _methodCache; set { _methodCache = value; _operatorInvoker = null; } }

    public readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement?>> PendingExecutions = new();
    public readonly CancellationTokenSource CancellationTokenSource = new();

    readonly ConcurrentDictionary<string, IMcpClient> _mcpClients = new(StringComparer.OrdinalIgnoreCase);

    public ExecutionMode Mode { get; set; } = ExecutionMode.ExternalProcess;
    public bool UseRemote { get; set; } = false;
    public string? RemoteUrl { get; set; }
    public string DockerImage { get; set; } = DockerInfo.DockerImageString;
    public string? RunnerExecutablePath { get; set; } // new override path

    IRunnerTransport? _transport;
    bool _started;

    public event EventHandler<AsonLogEventArgs>? Log;
    public event EventHandler<RunnerMethodInvokingEventArgs>? MethodInvoking;

    void DebugLog(string msg) => RaiseLogEvent(LogLevel.Debug, msg, source: nameof(RunnerClient));

    public void RaiseLogEvent(LogLevel level, string message, string? exception = null, string? source = null) =>
        Log?.Invoke(this, new AsonLogEventArgs(level, message, exception, source));

    public void RegisterMcpClient(IMcpClient client) {
        string name = client.ServerInfo.Name;
        _mcpClients[name] = client ?? throw new ArgumentNullException(nameof(client));
        RaiseLogEvent(LogLevel.Information, $"MCP client registered: {name}", source: nameof(RunnerClient));
    }

    public async Task StartProcessAsync() {
        if (Mode == ExecutionMode.InProcess && !UseRemote) return; // in-process execution has no external transport
        if (_started && _transport is { IsStarted: true }) return;
        _started = true;

        _transport = UseRemote
            ? new SignalRTransport(RemoteUrl?.TrimEnd('/') ?? throw new InvalidOperationException("RemoteUrl must be set when UseRemote=true"), Mode, DockerImage, (lvl,msg,ex,src)=>RaiseLogEvent(lvl,msg,ex,src))
            : new StdIoProcessTransport(Mode, DockerImage, RunnerExecutablePath);

        _transport.LineReceived += OnTransportLine;
        _transport.Closed += reason => RaiseLogEvent(LogLevel.Information, $"Runner transport closed: {reason}");

        await _transport.StartAsync().ConfigureAwait(false);
        DebugLog($"Transport started (UseRemote={UseRemote}, Mode={Mode})");
    }

    void EnsureInvokers() {
        if (_operatorInvoker != null && ReferenceEquals(_invokerCache, _methodCache)) return;
        if (_methodCache == null) throw new InvalidOperationException("Method cache not initialized yet");
        InitializeInvokers();
    }

    void InitializeInvokers() {
        _scheduler = _capturedContext != null ? new SynchronizationContextInvocationScheduler(_capturedContext) : new PassthroughInvocationScheduler();
        _invokerCache = _methodCache ?? throw new InvalidOperationException("Method cache not set");
        _operatorInvoker = new OperatorInvoker(handleToObject, _scheduler, JsonOptions, _invokerCache);
        _mcpInvoker = new McpToolInvoker(_mcpClients, JsonOptions);
    }

    // In-process host entry points
    internal Task<object?> InternalInvokeOperatorAsync(string target, string method, string? handleId, object?[] args) {
        EnsureInvokers();
        return _operatorInvoker!.InvokeAsync(target, method, handleId, args);
    }
    internal Task<object?> InternalInvokeMcpAsync(string server, string tool, IDictionary<string, object?>? args) {
        if (_mcpInvoker == null) InitializeInvokers();
        return _mcpInvoker!.InvokeAsync(server, tool, args);
    }

    // Public strongly-typed wrappers for external bridges (RemoteRunner scenario)
    public async Task<T?> InvokeOperatorAsync<T>(string target, string method, string? handleId, object?[]? args = null) {
        object? result = await InternalInvokeOperatorAsync(target, method, handleId, args ?? Array.Empty<object?>()).ConfigureAwait(false);
        if (result == null) return default;
        if (typeof(T) == typeof(object)) return (T)result;
        if (result is JsonElement je && typeof(T) == typeof(JsonElement)) return (T)(object)je;
        if (typeof(T) == typeof(string) && result is string s) return (T)(object)s;
        var jsonElem = JsonSerializer.SerializeToElement(result, JsonOptions);
        if (typeof(T) == typeof(JsonElement)) return (T)(object)jsonElem;
        return jsonElem.Deserialize<T>(JsonOptions)!;
    }
    public async Task<T?> InvokeMcpToolAsync<T>(string server, string tool, IDictionary<string, object?>? arguments = null) {
        object? result = await InternalInvokeMcpAsync(server, tool, arguments).ConfigureAwait(false);
        if (result == null) return default;
        if (result is JsonElement je && typeof(T) == typeof(JsonElement)) return (T)(object)je;
        if (typeof(T) == typeof(string) && result is string s) return (T)(object)s;
        var jsonElem = JsonSerializer.SerializeToElement(result, JsonOptions);
        if (typeof(T) == typeof(JsonElement)) return (T)(object)jsonElem;
        return jsonElem.Deserialize<T>(JsonOptions)!;
    }

    // Transport line handler -> typed protocol message
    void OnTransportLine(string line) {
        if (string.IsNullOrWhiteSpace(line)) return;
        DebugLog($"RX: {Truncate(line, 300)}");
        IRunnerMessage? msg = null;
        try { msg = RunnerMessageSerializer.Deserialize(line); }
        catch (Exception ex) { RaiseLogEvent(LogLevel.Error, "Protocol deserialize error", ex.ToString()); }
        if (msg is null) return;
        _ = HandleMessageAsync(msg);
    }

    async Task HandleMessageAsync(IRunnerMessage msg) {
        switch (msg) {
            case LogMessage log: HandleLog(log); break;
            case ExecResult exec: HandleExecResult(exec); break;
            case InvokeRequest invoke: await HandleInvokeRequestAsync(invoke).ConfigureAwait(false); break;
            case McpInvokeRequest mcpInvoke: await HandleMcpInvokeRequestAsync(mcpInvoke).ConfigureAwait(false); break;
            case InvokeResult: break; // not used yet
            default: RaiseLogEvent(LogLevel.Warning, $"Unknown message type: {msg.Type}"); break;
        }
    }

    void HandleExecResult(ExecResult exec) {
        if (PendingExecutions.TryRemove(exec.Id, out var tcs)) {
            if (!string.IsNullOrEmpty(exec.Error)) tcs.TrySetException(new Exception(exec.Error));
            else if (exec.Result is JsonElement je) tcs.TrySetResult(je.Clone());
            else if (exec.Result is null) tcs.TrySetResult(null);
            else tcs.TrySetResult(JsonSerializer.SerializeToElement(exec.Result, JsonOptions));
            DebugLog($"ExecResult completed Id={exec.Id} error={(exec.Error!=null)}");
        }
    }

    void HandleLog(LogMessage log) {
        var lvl = LogHelper.ParseLogLevel(log.Level ?? "Information");
        RaiseLogEvent(lvl, log.Message ?? string.Empty, log.Exception, log.Source);
    }

    bool RaiseMethodInvoking(RunnerMethodInvokingEventArgs args) {
        try { MethodInvoking?.Invoke(this, args); }
        catch (Exception ex) { RaiseLogEvent(LogLevel.Error, "MethodInvoking handler error", ex.ToString(), nameof(RunnerClient)); }
        return args.Cancel;
    }

    async Task HandleInvokeRequestAsync(InvokeRequest invoke) {
        object?[] args = invoke.Args ?? Array.Empty<object?>();
        if (RaiseMethodInvoking(new RunnerMethodInvokingEventArgs("operator", target: invoke.Target, method: invoke.Method, handleId: invoke.HandleId, arguments: args))) {
            await SendMessageAsync(new InvokeResult(invoke.Id, null, "Task was cancelled")).ConfigureAwait(false);
            return;
        }
        try {
            object? result = await InternalInvokeOperatorAsync(invoke.Target, invoke.Method, invoke.HandleId, args).ConfigureAwait(false);
            object? payload = result is null ? null : JsonSerializer.SerializeToElement(result, JsonOptions);
            await SendMessageAsync(new InvokeResult(invoke.Id, payload)).ConfigureAwait(false);
        } catch (Exception ex) {
            await SendMessageAsync(new InvokeResult(invoke.Id, null, ex.ToString())).ConfigureAwait(false);
        }
    }

    async Task HandleMcpInvokeRequestAsync(McpInvokeRequest mcpInvoke) {
        IDictionary<string, object?>? arguments = mcpInvoke.Arguments;
        if (RaiseMethodInvoking(new RunnerMethodInvokingEventArgs("mcp", server: mcpInvoke.Server, tool: mcpInvoke.Tool, argumentsDict: arguments))) {
            await SendMessageAsync(new InvokeResult(mcpInvoke.Id, null, "Task was cancelled")). ConfigureAwait(false);
            return;
        }
        try {
            var result = await InternalInvokeMcpAsync(mcpInvoke.Server, mcpInvoke.Tool, arguments).ConfigureAwait(false);
            object? payload = result is null ? null : JsonSerializer.SerializeToElement(result, JsonOptions);
            await SendMessageAsync(new InvokeResult(mcpInvoke.Id, payload)).ConfigureAwait(false);
        } catch (Exception ex) {
            await SendMessageAsync(new InvokeResult(mcpInvoke.Id, null, ex.ToString())).ConfigureAwait(false);
        }
    }

    async Task SendMessageAsync(IRunnerMessage message) {
        if (Mode == ExecutionMode.InProcess && !UseRemote)
            throw new InvalidOperationException("SendMessageAsync should not be used for in-process mode");
        if (_transport == null || !_transport.IsStarted) throw new InvalidOperationException("Transport not started");
        var json = RunnerMessageSerializer.Serialize(message);
        DebugLog($"TX: {json}");
        await _transport.SendAsync(json).ConfigureAwait(false);
    }

    public async Task StopAsync() {
        try { CancellationTokenSource.Cancel(); } catch { }
        if (_transport != null) { try { await _transport.StopAsync().ConfigureAwait(false); } catch { } _transport = null; }
    }

    public void RegisterAssemblies(params Assembly[] assemblies) {
        if (assemblies == null || assemblies.Length == 0) return;
        foreach (var asm in assemblies) if (asm != null && !_assemblies.Contains(asm)) _assemblies.Add(asm);
    }

    // Existing API retained for backward compatibility (no cancellation parameter)
    public Task<JsonElement?> ExecuteAsync(string code) => ExecuteAsync(code, CancellationToken.None);

    // New cancellation-aware execution entry point
    public async Task<JsonElement?> ExecuteAsync(string code, CancellationToken ct) {
        if (UseRemote && _transport == null) await StartProcessAsync().ConfigureAwait(false);
        if (!UseRemote && Mode != ExecutionMode.InProcess && _transport == null) await StartProcessAsync().ConfigureAwait(false);

        if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);

        if (Mode == ExecutionMode.InProcess && !UseRemote) {
            return await ExecuteInProcessAsync(code, ct).ConfigureAwait(false);
        }
        string id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingExecutions[id] = tcs;
        await SendMessageAsync(new ExecRequest(id, code)).ConfigureAwait(false);

        using (ct.Register(() => {
            // Best-effort: if cancellation occurs before completion, fault the TCS so caller observes OCE.
            if (PendingExecutions.TryRemove(id, out var pending)) {
                pending.TrySetException(new OperationCanceledException(ct));
            }
        })) {
            return await tcs.Task.ConfigureAwait(false);
        }
    }

    async Task<JsonElement?> ExecuteInProcessAsync(string code) => await ExecuteInProcessAsync(code, CancellationToken.None);

    async Task<JsonElement?> ExecuteInProcessAsync(string code, CancellationToken ct) {
        EnsureInvokers();
        var host = new InProcHost(this);
        var result = await ScriptExecutor.EvaluateAsync(code, host, ct).ConfigureAwait(false);
        if (result is null) return null;
        return JsonSerializer.SerializeToElement(result, JsonOptions);
    }

    static string Truncate(string value, int max) => value.Length <= max ? value : value.Substring(0, max) + "...";
}

internal sealed class EmptyMethodCache : IOperatorMethodCache {
    public OperatorMethodEntry GetOrAddClosedGeneric(OperatorMethodEntry openEntry, Type[] typeArguments) => openEntry; // no-op
    public bool TryGet(Type declaringType, string name, int argCount, out OperatorMethodEntry entry) { entry = null!; return false; }
}

public sealed class RunnerMethodInvokingEventArgs : CancelEventArgs {
    public string InvocationKind { get; }
    public string? Target { get; }
    public string? Method { get; }
    public string? HandleId { get; }
    public string? Server { get; }
    public string? Tool { get; }
    public object? ArgumentsObject { get; }
    public object?[]? ArgumentsArray { get; }
    public string? UserTask { get; set; }
    public RunnerMethodInvokingEventArgs(string invocationKind, string? target = null, string? method = null, string? handleId = null, string? server = null, string? tool = null, object?[]? arguments = null, IDictionary<string, object?>? argumentsDict = null) {
        InvocationKind = invocationKind;
        Target = target;
        Method = method;
        HandleId = handleId;
        Server = server;
        Tool = tool;
        ArgumentsArray = arguments;
        ArgumentsObject = argumentsDict;
    }
    public IReadOnlyList<object?>? GetArguments() => ArgumentsArray;
    public IDictionary<string, object?>? GetArgumentsDictionary() => ArgumentsObject as IDictionary<string, object?>;
}