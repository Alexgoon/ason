using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Ason;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using Ason.Client.Execution;
using AsonRunner;
using Ason.CodeGen;

namespace Ason.Tests.Logging;

public class LoggingAndEventsTests {

    private static OperatorsLibrary BuildSnapshot() => new OperatorBuilder()
        .AddAssemblies(typeof(RootOperator).Assembly)
        .SetBaseFilter(mi => mi.GetCustomAttribute<AsonMethodAttribute>() != null)
        .Build();

    // Minimal chat service always returning provided queue entries
    private sealed class QueueChatService : IChatCompletionService {
        private readonly Queue<string> _answers = new();
        public QueueChatService(params string[] replies) { foreach (var r in replies) _answers.Enqueue(r); }
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();
        private string Next() => _answers.Count > 0 ? _answers.Dequeue() : "script";
        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default) {
            IReadOnlyList<ChatMessageContent> list = new List<ChatMessageContent> { new ChatMessageContent(AuthorRole.Assistant, Next()) };
            return Task.FromResult(list);
        }
        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var list = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
            foreach (var m in list) yield return new StreamingChatMessageContent(m.Role, m.Content);
        }
    }

    // Custom repair executor that executes a predetermined script referencing an operator to trigger MethodInvoking
    private sealed class TestRepairExecutor : IScriptRepairExecutor {
        private readonly string _script;
        public TestRepairExecutor(string script) { _script = script; }
        public async Task<ExecOutcome> ExecuteWithRepairsAsync(string userTask, int maxAttempts, string? proxies, Microsoft.SemanticKernel.Agents.ChatCompletionAgent scriptAgent, RunnerClient runner, IScriptValidator validator, Action<LogLevel, string, Exception?> log, CancellationToken ct) {
            string code = proxies + "\n" + _script;
            var result = await runner.ExecuteAsync(code).ConfigureAwait(false);
            string raw = result?.ToString() ?? string.Empty;
            return new ExecOutcome(true, raw, null, _script, 1);
        }
    }

    private AsonClient CreateClient(IChatCompletionService svc, IScriptRepairExecutor? repair = null) {
        var root = new RootOperator(new object());
        var snapshot = BuildSnapshot();
        var client = new AsonClient(svc, root, snapshot, new AsonClientOptions { SkipReceptionAgent = false, SkipExplainerAgent = true, ExecutionMode = ExecutionMode.InProcess }, repair, new KeywordScriptValidator(Array.Empty<string>()), new ResultExplainer());
        return client;
    }

    [Fact]
    public void LogEvent_PropagatesRunnerLog() {
        var svc = new QueueChatService("script");
        var client = CreateClient(svc);
        string? captured = null; LogLevel? level = null;
        client.Log += (s,e)=> { captured = e.Message; level = e.Level; };
        // Access internal runner and raise log
        var runnerField = typeof(AsonClient).GetField("_runner", BindingFlags.NonPublic|BindingFlags.Instance);
        var runner = runnerField!.GetValue(client);
        var raise = runner!.GetType().GetMethod("RaiseLogEvent", BindingFlags.Public|BindingFlags.Instance);
        raise!.Invoke(runner, new object?[]{ LogLevel.Warning, "HelloLog", null, "RunnerX" });
        Assert.NotNull(captured);
        Assert.Contains("[RunnerX] HelloLog", captured);
        Assert.Equal(LogLevel.Warning, level);
    }

    [Fact]
    public async Task ExecuteScriptDirect_ErrorPath_LogsError() {
        var svc = new QueueChatService("script");
        var client = CreateClient(svc);
        List<(LogLevel lvl,string msg)> logs = new();
        client.Log += (s,e)=> logs.Add((e.Level, e.Message));
        // Invalid C# to force compilation error inside runner => caught in ExecuteScriptDirectAsync
        var output = await client.ExecuteScriptDirectAsync("this is not valid csharp code;", validate:false);
        Assert.Equal("Error", output);
        Assert.Contains(logs, l => l.lvl == LogLevel.Error && l.msg.Contains("Direct script execution error"));
    }

}
