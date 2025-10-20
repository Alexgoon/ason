using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Ason.Client.Execution;
using Ason.Invocation;
using Ason.CodeGen;
using System.Threading.Channels;

namespace Ason;

internal readonly record struct ExecOutcome(bool Success, string? RawResult, string? ErrorMessage, string? ExecutedScript, int Attempts);
public sealed record OrchestrationResult(bool Success, string Route, string? ResponseText, string? RawResult, string? GeneratedScript, int Attempts);

public class AsonClient : IChatClient {
    readonly Kernel _scriptKernel;
    readonly Kernel _receptionKernel;
    readonly Kernel _explainerKernel;

    readonly RunnerClient _runner;
    Task _runnerStart = Task.CompletedTask;
    string? _proxies;
    string? _signatures;
    Task _proxyAugmentationTask = Task.CompletedTask; // waits for dynamic MCP code if any

    ChatCompletionAgent? _scriptAgent;
    ChatCompletionAgent? _receptionAgent;
    ChatCompletionAgent? _explainerAgent;
    RootOperator _rootOperator;

    readonly AsonClientOptions _options;
    readonly OperatorsLibrary _operatorsLibrary;

    readonly IScriptRepairExecutor _repairExecutor;
    readonly IScriptValidator _validator;
    readonly IResultExplainer _resultExplainer;

    internal static AsonClient? CurrentInstance;

    public event EventHandler<AsonLogEventArgs>? Log;
    public event EventHandler<RunnerMethodInvokingEventArgs>? MethodInvoking;

    ChatHistoryAgentThread? _agentThread;

    public int MaxScriptFixAttempts { get; } = 2;
    public IChatCompletionService DefaultChatCompletion { get; }
    public IChatCompletionService ScriptChatCompletion { get; }
    public IChatCompletionService ReceptionChatCompletion { get; }
    public IChatCompletionService ExplainerChatCompletion { get; }

    internal Kernel ReceptionKernel => _receptionKernel;

    string? _consolidatedUserTask;

    public AsonClient(
        IChatCompletionService defaultChatCompletion,
        RootOperator rootOperator,
        OperatorsLibrary operators, AsonClientOptions? options = null) : this(defaultChatCompletion, rootOperator, operators, options, null, null, null) { }

    internal AsonClient(
        IChatCompletionService defaultChatCompletion,
        RootOperator rootOperator,
        OperatorsLibrary operators,
        AsonClientOptions? options,
        IScriptRepairExecutor? repairExecutor,
        IScriptValidator? validator,
        IResultExplainer? resultExplainer) {
        _options = options ?? new AsonClientOptions();
        _operatorsLibrary = operators ?? throw new ArgumentNullException(nameof(operators));
        MaxScriptFixAttempts = _options.MaxFixAttempts;

        DefaultChatCompletion = defaultChatCompletion;
        ScriptChatCompletion = _options.ScriptChatCompletion ?? defaultChatCompletion;
        ReceptionChatCompletion = _options.ReceptionChatCompletion ?? defaultChatCompletion;
        ExplainerChatCompletion = _options.ExplainerChatCompletion ?? defaultChatCompletion;

        _rootOperator = rootOperator;
        if (_operatorsLibrary.HasExtractor && !_rootOperator.OperatorInstances.Values.Any(o => o is ExtractionOperator)) {
            var extractor = new ExtractionOperator();
            _rootOperator.OperatorInstances.TryAdd(extractor.Handle, extractor);
        }

        _scriptKernel = BuildKernel(ScriptChatCompletion);
        _receptionKernel = BuildKernel(ReceptionChatCompletion);
        _explainerKernel = BuildKernel(ExplainerChatCompletion);

        _runner = new RunnerClient(rootOperator.OperatorInstances, SynchronizationContext.Current) { Mode = _options.ExecutionMode };
        if (!string.IsNullOrWhiteSpace(_options.RunnerExecutablePath)) {
            _runner.RunnerExecutablePath = _options.RunnerExecutablePath;
        }
        SetupCommonLogging();

        _repairExecutor = repairExecutor ?? new ScriptRepairExecutor();
        _validator = validator ?? new KeywordScriptValidator(_options.ForbiddenScriptKeywords);
        _resultExplainer = resultExplainer ?? new ResultExplainer();

        _runner.MethodInvoking += (s, e) => {
            e.UserTask ??= _agentThread?.ChatHistory.Where(m => m.Role == AuthorRole.User).LastOrDefault()?.Content;
            MethodInvoking?.Invoke(this, e);
        };

        CurrentInstance = this;

        BuildInitialProxyLayer();

        if (_options.UseRemoteRunner) {
            if (!string.IsNullOrWhiteSpace(_options.RemoteRunnerBaseUrl)) {
                _ = EnableRemoteRunnerAsync(_options.RemoteRunnerBaseUrl, _options.StopLocalRunnerWhenEnablingRemote, _options.RemoteRunnerDockerImage);
            }
            else {
                throw new ArgumentException("When UseRemoteRunner is true, you must provide a Remote runner base URL. Make sure your server is configured by calling RemoteRunnerServiceExtensions.AddRemoteScriptRunner and RemoteRunnerServiceExtensions.MapRemoteScriptRunner, then set the server URL in RemoteRunnerBaseUrl.", nameof(options));
            }
        }
    }

    void BuildInitialProxyLayer() {
        var snapshot = _operatorsLibrary;

        _proxyAugmentationTask = snapshot.BuildTask.ContinueWith(t => {
            if (t.Status == TaskStatus.RanToCompletion) {
                var (rt, sig, cache) = t.Result;
                IOperatorMethodCache effectiveCache = cache;
                if (_options.AdditionalMethodFilter != null)
                    effectiveCache = new FilteringMethodCache(cache, _options.AdditionalMethodFilter);
                _runner.MethodCache = effectiveCache;

                if (snapshot.McpClients is { Count: > 0 }) {
                    foreach (var client in snapshot.McpClients) {
                        try { if (client != null) _runner.RegisterMcpClient(client); }
                        catch (Exception ex) { OnLog(LogLevel.Error, $"Failed registering MCP client '{client?.ServerInfo?.Name}'", ex); }
                    }
                }

                var instanceDecl = BuildExistingOperatorVariableDeclarations();
                _proxies = rt + instanceDecl;
                _signatures = sig + instanceDecl;
                InitAgents();
            }
            else if (t.IsFaulted) {
                OnLog(LogLevel.Error, "Proxy build failed", t.Exception);
            }
        }, TaskContinuationOptions.ExecuteSynchronously);

        if (!_options.UseRemoteRunner)
            _runnerStart = _runner.StartProcessAsync();
    }

    public async Task EnableRemoteRunnerAsync(string baseUrl, bool stopLocalIfRunning = true, string? dockerImage = null, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("Base URL required", nameof(baseUrl));
        if (stopLocalIfRunning && !_runner.UseRemote) {
            try { await _runner.StopAsync().ConfigureAwait(false); } catch { }
        }
        if (dockerImage is not null) _runner.DockerImage = dockerImage;
        _runner.UseRemote = true;
        _runner.RemoteUrl = baseUrl.TrimEnd('/');
        _runnerStart = _runner.StartProcessAsync();
        await _runnerStart.ConfigureAwait(false);
    }

    void SetupCommonLogging() {
        _runner.Log += (s, e) => {
            var msg = string.IsNullOrEmpty(e.Source) ? e.Message : $"[{e.Source}] {e.Message}";
            if (!string.IsNullOrEmpty(e.Exception)) msg += Environment.NewLine + e.Exception;
            OnLog(e.Level, msg);
        };
    }

    string BuildExistingOperatorVariableDeclarations() {
        var sb = new StringBuilder();
        sb.AppendLine();
        var typeInstanceCount = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var instance in _rootOperator.OperatorInstances.Values) {
            var type = instance.GetType();
            if (type == typeof(RootOperator)) continue;
            var typeName = type.Name;
            if (!typeInstanceCount.TryGetValue(typeName, out var count)) count = 0;
            string baseVar = char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
            string varName = count == 0 ? baseVar : baseVar + count.ToString();
            typeInstanceCount[typeName] = count + 1;
            string proxyName = typeName;
            bool isRootDerived = typeof(RootOperator).IsAssignableFrom(type) && type != typeof(RootOperator);
            string ctor = isRootDerived ? $"new {proxyName}()" : $"new {proxyName}(\"{(instance as OperatorBase)?.Handle ?? typeName}\")";
            sb.AppendLine($"{proxyName} {varName} = {ctor};");
        }
        sb.AppendLine();
        return sb.ToString();
    }

    Kernel BuildKernel(IChatCompletionService chat) {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chat);
        return builder.Build();
    }

    string BuildReceptionInstructions() {
        if (!string.IsNullOrWhiteSpace(_options.ReceptionInstructions)) return _options.ReceptionInstructions;
        return AgentPrompts.ReceptionAgentTemplate;
    }

    void InitAgents() {
        _receptionAgent = CreateAgent(
            "Reception",
            BuildReceptionInstructions(),
            _receptionKernel);
        _scriptAgent = CreateAgent(
            "ScriptGenerator",
            _options.ScriptInstructions ?? string.Format(AgentPrompts.ScriptAgentTemplate, _signatures ?? string.Empty),
            _scriptKernel);
        _explainerAgent = CreateAgent(
            "Explainer",
            _options.ExplainerInstructions ?? AgentPrompts.ExplainerAgentTemplate,
            _explainerKernel);
    }

    protected virtual ChatCompletionAgent CreateAgent(string name, string instructions, Kernel kernel) =>
        new ChatCompletionAgent { Name = name, Instructions = instructions, Kernel = kernel };

    async Task EnsureReadyAsync() {
        await _proxyAugmentationTask.ConfigureAwait(false);
        await _runnerStart.ConfigureAwait(false);
        if (_scriptAgent is null || _receptionAgent is null || _explainerAgent is null || string.IsNullOrWhiteSpace(_proxies)) {
            throw new InvalidOperationException("Proxies not initialized.");
        }
    }

    enum RouteKind { Script, Answer }

    async Task<(RouteKind route, string payload)> DecideRouteAdvanceAsync(string userTask, bool skipAnswer, ChatCompletionAgent? receptionAgent, ChatHistoryAgentThread thread, Action<LogLevel, string, Exception?> log, CancellationToken ct) {
        if (skipAnswer) { log(LogLevel.Information, "Skipping ReceptionAgent; routing directly to ScriptAgent.", null); _consolidatedUserTask = userTask; return (RouteKind.Script, userTask); }
        if (receptionAgent is null) { _consolidatedUserTask ??= userTask; return (RouteKind.Script, userTask); }
        var sb = new StringBuilder();
        try {
            var messages = new[] { new ChatMessageContent(AuthorRole.User, userTask) };
            OnLog(LogLevel.Information, $"ReceptionAgent input: {userTask}");
            await foreach (var item in receptionAgent.InvokeAsync(messages, thread: thread, options: null, cancellationToken: ct).ConfigureAwait(false)) {
                var part = item.Message?.Content; if (!string.IsNullOrWhiteSpace(part)) sb.Append(part);
            }
            var decisionRaw = sb.ToString();
            var trimmed = decisionRaw.Trim();

            OnLog(LogLevel.Information, $"ReceptionAgent completed output: {trimmed}");
            if (string.IsNullOrWhiteSpace(trimmed)) { _consolidatedUserTask = userTask; return (RouteKind.Script, userTask); }
            if (trimmed.StartsWith("script", StringComparison.OrdinalIgnoreCase)) {
                string synthesized = ExtractTaskBlock(trimmed) ?? userTask;
                _consolidatedUserTask = synthesized;
                return (RouteKind.Script, synthesized);
            }
            _consolidatedUserTask = null;
            return (RouteKind.Answer, trimmed);
        }
        catch (Exception ex) { log(LogLevel.Error, "ReceptionAgent routing error", ex); throw; }
    }

    static string? ExtractTaskBlock(string receptionAgentOutput) {
        int start = receptionAgentOutput.IndexOf("<task>", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        int end = receptionAgentOutput.IndexOf("</task>", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0) return null;
        int innerStart = start + "<task>".Length;
        var inner = receptionAgentOutput.Substring(innerStart, end - innerStart).Trim();
        return string.IsNullOrWhiteSpace(inner) ? null : inner;
    }

    async Task<OrchestrationResult> ExecuteScriptRouteNonStreamingAsync(string task, ChatHistoryAgentThread thread, bool skipExplainer, CancellationToken cancellationToken) {
        var effectiveTask = _consolidatedUserTask ?? task;
        var outcome = await TryExecuteWithRepairsAsync(effectiveTask, cancellationToken).ConfigureAwait(false);
        if (!outcome.Success && outcome.ErrorMessage?.StartsWith("Cannot", StringComparison.OrdinalIgnoreCase) == true) {
            thread.ChatHistory.AddAssistantMessage(outcome.ErrorMessage);
            return new OrchestrationResult(false, "script", outcome.ErrorMessage, null, outcome.ExecutedScript, outcome.Attempts);
        }
        if (!outcome.Success) {
            var msg = outcome.ErrorMessage ?? "Task could not be executed.";
            thread.ChatHistory.AddAssistantMessage(msg);
            return new OrchestrationResult(false, "script", msg, null, outcome.ExecutedScript, outcome.Attempts);
        }
        if (string.IsNullOrEmpty(outcome.RawResult)) return new OrchestrationResult(true, "script", "Task completed", null, outcome.ExecutedScript, outcome.Attempts);
        if (skipExplainer) { thread.ChatHistory.AddAssistantMessage(outcome.RawResult!); return new OrchestrationResult(true, "script", outcome.RawResult, outcome.RawResult, outcome.ExecutedScript, outcome.Attempts); }
        string explanation = await _resultExplainer.ExplainAsync(effectiveTask, outcome.RawResult!, _explainerAgent!, (lvl, msg, ex) => OnLog(lvl, msg, ex), cancellationToken).ConfigureAwait(false);
        var trimmed = explanation.Trim(); thread.ChatHistory.AddAssistantMessage(trimmed);
        return new OrchestrationResult(true, "script", trimmed, outcome.RawResult, outcome.ExecutedScript, outcome.Attempts);
    }

    async IAsyncEnumerable<ChatResponseUpdate> StreamScriptRouteAsync(string task, ChatHistoryAgentThread thread, bool skipExplainer, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken) {
        var effectiveTask = _consolidatedUserTask ?? task;
        var outcome = await TryExecuteWithRepairsAsync(effectiveTask, cancellationToken);
        if (!outcome.Success && outcome.ErrorMessage?.StartsWith("Cannot", StringComparison.OrdinalIgnoreCase) == true) { yield return new ChatResponseUpdate(ChatRole.Assistant, outcome.ErrorMessage); yield break; }
        if (!outcome.Success) { var msg = outcome.ErrorMessage ?? "Task could not be executed."; yield return new ChatResponseUpdate(ChatRole.Assistant, msg); yield break; }
        if (string.IsNullOrEmpty(outcome.RawResult)) { yield return new ChatResponseUpdate(ChatRole.Assistant, "Task completed"); yield break; }
        if (skipExplainer) { thread.ChatHistory.AddAssistantMessage(outcome.RawResult!); yield return new ChatResponseUpdate(ChatRole.Assistant, outcome.RawResult); yield break; }
        var explainerInput = "<task>\n" + effectiveTask + "\n</task>\n<result>\n" + outcome.RawResult + "\n</result>";
        var sbExplainer = new StringBuilder();
        var explainerMessages = new[] { new ChatMessageContent(AuthorRole.User, explainerInput) };
        await foreach (var item in _explainerAgent!.InvokeStreamingAsync(explainerMessages, thread: null, options: null, cancellationToken)) {
            var part = item.Message?.Content; if (part is null) continue; sbExplainer.Append(part); if (part.Length > 0) yield return new ChatResponseUpdate(ChatRole.Assistant, part);
        }
        var full = sbExplainer.ToString(); if (!string.IsNullOrEmpty(full)) thread.ChatHistory.AddAssistantMessage(full);
    }

    async Task<OrchestrationResult> SendDetailedAsync(ChatHistoryAgentThread thread, CancellationToken cancellationToken = default) {
        // Offload entire orchestration to background thread so caller's (UI) sync context is not blocked
        return await Task.Run(async () => {
            string userTask = ExtractLatestUserMessage(thread) ?? string.Empty;
            bool skipAnswer = _options.SkipReceptionAgent;
            bool skipExplainer = _options.SkipExplainerAgent;
            var (route, payload) = await DecideRouteAdvanceAsync(userTask, skipAnswer, _receptionAgent, thread, (lvl, msg, ex) => OnLog(lvl, msg, ex), cancellationToken).ConfigureAwait(false);
            if (route == RouteKind.Answer) { thread.ChatHistory.AddAssistantMessage(payload); return new OrchestrationResult(true, "answer", payload, null, null, 1); }
            // payload may already be synthesized task if script route selected
            var effectiveTask = _consolidatedUserTask ?? userTask;
            return await ExecuteScriptRouteNonStreamingAsync(effectiveTask, thread, skipExplainer, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ExecuteScriptDirectAsync(string script, bool validate = true) {
        if (string.IsNullOrWhiteSpace(script)) return string.Empty;
        // Entire operation offloaded so any networking / remote runner waits do not sit on UI thread
        return await Task.Run(async () => {
            await EnsureReadyAsync().ConfigureAwait(false);
            if (validate) {
                var validationError = _validator.Validate(script);
                if (validationError is not null) return string.Empty;
            }
            string code = _proxies + "\n" + script;
            try {
                var execResult = await _runner.ExecuteAsync(code).ConfigureAwait(false);
                string raw = execResult?.ToString() ?? string.Empty;
                OnLog(LogLevel.Information, $"Direct script execution success. RawResult={raw}");
                return raw;
            }
            catch (Exception ex) {
                OnLog(LogLevel.Error, "Direct script execution error", ex);
                return "Error";
            }
        }).ConfigureAwait(false);
    }

    public async Task<string> SendAsync(string message, CancellationToken cancellationToken = default) {
        var response = await ((IChatClient)this).GetResponseAsync(new[] { new ChatMessage(ChatRole.User, message) }, options: default, cancellationToken);
        return response.ToString();
    }

    public async Task<string> SendAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default) {
        var response = await ((IChatClient)this).GetResponseAsync(messages, options: default, cancellationToken);
        return response.ToString();
    }

    public async IAsyncEnumerable<string> SendStreamingAsync(string message, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
        await foreach (var update in ((IChatClient)this).GetStreamingResponseAsync(new[] { new ChatMessage(ChatRole.User, message) }, options: default, cancellationToken)) {
            var text = ExtractText(update);
            if (!string.IsNullOrEmpty(text)) yield return text;
        }
    }

    public async IAsyncEnumerable<string> SendStreamingAsync(IEnumerable<ChatMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
        await foreach (var update in ((IChatClient)this).GetStreamingResponseAsync(messages, options: default, cancellationToken)) {
            var text = ExtractText(update);
            if (!string.IsNullOrEmpty(text)) yield return text;
        }
    }

    static string GetMessageText(ChatMessage msg) {
        if (msg.Contents is null || msg.Contents.Count == 0) return msg.ToString() ?? string.Empty;
        return string.Concat(msg.Contents.OfType<Microsoft.Extensions.AI.TextContent>().Select(t => t.Text));
    }

    static string ExtractText(ChatResponseUpdate update) {
        if (update.Contents is { Count: > 0 }) return string.Concat(update.Contents.OfType<Microsoft.Extensions.AI.TextContent>().Select(c => c.Text));
        return update.ToString() ?? string.Empty;
    }

    string? ExtractLatestUserMessage(ChatHistoryAgentThread thread) => thread.ChatHistory.Where(m => m.Role == AuthorRole.User).LastOrDefault()?.Content;

    static string ConvertMessagesToString(IEnumerable<ChatMessage> messages) {
        var sb = new StringBuilder();
        foreach (var m in messages) {
            if(m.Role == ChatRole.System)
                continue;
            var text = GetMessageText(m);
            sb.Append('[').Append(m.Role).Append("] ");
            if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text); else sb.AppendLine("<empty>");
        }
        return sb.ToString().TrimEnd();
    }

    async Task<ExecOutcome> TryExecuteWithRepairsAsync(string userTask, CancellationToken ct) {
        await _proxyAugmentationTask.ConfigureAwait(false);
        await _runnerStart.ConfigureAwait(false);
        return await _repairExecutor.ExecuteWithRepairsAsync(
            userTask,
            MaxScriptFixAttempts,
            _proxies,
            _scriptAgent!,
            _runner,
            _validator,
            (lvl, msg, ex) => OnLog(lvl, msg, ex),
            ct);
    }

    (ChatHistoryAgentThread thread, string userTask) PrepareThreadFromMessages(IEnumerable<ChatMessage> messages) {
        var history = new ChatHistory();
        foreach (var msg in messages) {
            if (msg.Role == ChatRole.System)
                continue;
            var text = GetMessageText(msg);
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (msg.Role == ChatRole.User) history.AddUserMessage(text);
            else if (msg.Role == ChatRole.Assistant) history.AddAssistantMessage(text);
        }
        var thread = new ChatHistoryAgentThread(history);
        _agentThread = thread;
        string userTask = history.Where(m => m.Role == AuthorRole.User).LastOrDefault()?.Content ?? string.Empty;
        return (thread, userTask);
    }

    async Task<ChatResponse> IChatClient.GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken) {
        await EnsureReadyAsync().ConfigureAwait(false);
        var (thread, _) = PrepareThreadFromMessages(messages);
        var result = await SendDetailedAsync(thread, cancellationToken);
        string text = result.ResponseText ?? (result.Success ? "Task completed" : "Task could not be executed");
        return new List<ChatResponseUpdate> { new(ChatRole.Assistant, text) }.ToChatResponse();
    }

    async IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken) {
        await foreach (var update in InternalStreaming(messages, cancellationToken)) yield return update;
    }

    async IAsyncEnumerable<ChatResponseUpdate> InternalStreaming(IEnumerable<ChatMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken) {
        // Execute the heavy orchestration logic on a background thread so that the UI thread is never blocked
        var channel = Channel.CreateUnbounded<ChatResponseUpdate>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        _ = Task.Run(async () => {
            try {
                await ExecuteInternalStreamingCoreAsync(messages, channel.Writer, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { OnLog(LogLevel.Error, "InternalStreaming background error", ex); }
            finally { channel.Writer.TryComplete(); }
        }, CancellationToken.None);

        while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)) {
            while (channel.Reader.TryRead(out var update)) yield return update;
        }
    }

    private async Task ExecuteInternalStreamingCoreAsync(IEnumerable<ChatMessage> messages, ChannelWriter<ChatResponseUpdate> writer, CancellationToken cancellationToken) {
        await EnsureReadyAsync().ConfigureAwait(false);
        var (thread, userTask) = PrepareThreadFromMessages(messages);
        bool skipAnswer = _options.SkipReceptionAgent;
        bool skipExplainer = _options.SkipExplainerAgent;
        bool proceedToScript = false;
        _consolidatedUserTask = null;
        if (skipAnswer) { OnLog(LogLevel.Information, "Skipping ReceptionAgent; routing directly to ScriptAgent."); _consolidatedUserTask = userTask; proceedToScript = true; }
        else {
            var sb = new StringBuilder();
            bool bufferingPossibleScript = true;
            bool collectingTaskBlock = false;

            OnLog(LogLevel.Information, $"ReceptionAgent input:\n{ConvertMessagesToString(messages)}");
            await foreach (var item in _receptionAgent!.InvokeStreamingAsync(thread: thread, options: null, cancellationToken).ConfigureAwait(false)) {
                var part = item.Message?.Content;
                if (part is null) continue;
                sb.Append(part);
                var currentFull = sb.ToString();
                var currentTrimmedStart = currentFull.TrimStart();

                if (bufferingPossibleScript) {
                    if (currentTrimmedStart.Equals("script", StringComparison.OrdinalIgnoreCase)) {
                        proceedToScript = true;
                        bufferingPossibleScript = false;
                        collectingTaskBlock = true;
                        continue;
                    }
                    if ("script".StartsWith(currentTrimmedStart, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    await writer.WriteAsync(new ChatResponseUpdate(ChatRole.Assistant, currentFull), cancellationToken).ConfigureAwait(false);
                    bufferingPossibleScript = false;
                }
                else if (collectingTaskBlock) {
                    if (currentFull.IndexOf("</task>", StringComparison.OrdinalIgnoreCase) >= 0) break;
                    continue;
                }
                else {
                    await writer.WriteAsync(new ChatResponseUpdate(ChatRole.Assistant, part), cancellationToken).ConfigureAwait(false);
                }
            }
            var all = sb.ToString();
            OnLog(LogLevel.Information, $"ReceptionAgent completed output: {all}");
            if (proceedToScript) {

                _consolidatedUserTask = ExtractTaskBlock(all) ?? userTask;
            }
            else {

                var full = all.Trim();
                if (string.IsNullOrWhiteSpace(full)) { proceedToScript = true; _consolidatedUserTask = userTask; }
                else if (!full.Equals("script", StringComparison.OrdinalIgnoreCase)) {
                    if (!thread.ChatHistory.Any(m => m.Role == AuthorRole.Assistant && m.Content == full)) thread.ChatHistory.AddAssistantMessage(full);
                    return;
                }
            }
        }
        if (proceedToScript) {
            var effectiveTask = _consolidatedUserTask ?? userTask;
            await foreach (var u in StreamScriptRouteAsync(effectiveTask, thread, skipExplainer, cancellationToken).ConfigureAwait(false)) {
                await writer.WriteAsync(u, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    void OnLog(LogLevel level, string message, Exception? exception = null) => Log?.Invoke(this, new AsonLogEventArgs(level, message, exception?.ToString(), nameof(AsonClient)));

    object? IChatClient.GetService(Type serviceType, object? serviceKey) => serviceType.IsInstanceOfType(this) ? this : null;

    void IDisposable.Dispose() { try { _runner.StopAsync().GetAwaiter().GetResult(); } catch { } }
}
