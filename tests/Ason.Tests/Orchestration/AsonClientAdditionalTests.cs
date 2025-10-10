using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ason;
using Ason.Client.Execution;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;
using AsonRunner;
using Ason.CodeGen;

namespace Ason.Tests.Orchestration;

public class AsonClientAdditionalTests {

    private static OperatorsLibrary Snapshot = new OperatorBuilder()
        .AddAssemblies(typeof(RootOperator).Assembly)
        .SetBaseFilter(mi => mi.GetCustomAttribute<AsonMethodAttribute>() != null)
        .Build();

    // Chat service returning queued responses; supports artificial delay per token.
    private sealed class DelayedQueueChatService : IChatCompletionService {
        private readonly ConcurrentQueue<string> _responses = new();
        private readonly int _delayMs;
        public DelayedQueueChatService(int delayMs, params string[] replies) { _delayMs = delayMs; foreach (var r in replies) _responses.Enqueue(r); }
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();
        private string Next() => _responses.TryDequeue(out var v) ? v : string.Empty;
        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default) {
            var txt = Next();
            IReadOnlyList<ChatMessageContent> list = new List<ChatMessageContent>{ new ChatMessageContent(AuthorRole.Assistant, txt) };
            return Task.FromResult(list);
        }
        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var txt = Next();
            // split into characters to exercise streaming assembly
            foreach (var ch in txt) {
                cancellationToken.ThrowIfCancellationRequested();
                if (_delayMs > 0) await Task.Delay(_delayMs, cancellationToken);
                yield return new StreamingChatMessageContent(AuthorRole.Assistant, ch.ToString());
            }
        }
    }

    private sealed class NoInvokeRepairExecutor : IScriptRepairExecutor {
        public int Calls; 
        public Task<ExecOutcome> ExecuteWithRepairsAsync(string userTask, int maxAttempts, string? proxies, Microsoft.SemanticKernel.Agents.ChatCompletionAgent scriptAgent, RunnerClient runner, IScriptValidator validator, Action<LogLevel, string, Exception?> log, CancellationToken ct) {
            Calls++; return Task.FromResult(new ExecOutcome(false, null, "Should not be called", null, 0)); }
    }

    private static AsonClient CreateClient(IChatCompletionService answerSvc, IScriptRepairExecutor repair, AsonClientOptions? opts = null) {
        var root = new RootOperator(new object());
        var options = opts ?? new AsonClientOptions { SkipReceptionAgent = false, SkipExplainerAgent = true };
        var client = new AsonClient(answerSvc, root, Snapshot, new AsonClientOptions {
            SkipReceptionAgent = options.SkipReceptionAgent,
            SkipExplainerAgent = options.SkipExplainerAgent,
            ReceptionChatCompletion = answerSvc,
            ScriptChatCompletion = answerSvc,
            ExplainerChatCompletion = answerSvc,
            MaxFixAttempts = 1,
            ForbiddenScriptKeywords = Array.Empty<string>(),
            ExecutionMode = ExecutionMode.InProcess
        }, repair, new KeywordScriptValidator(Array.Empty<string>()), new ResultExplainer());
        return client;
    }

    [Fact]
    public async Task AnswerRoute_DoesNotInvokeScriptRepairExecutor() {
        var repair = new NoInvokeRepairExecutor();
        var chat = new DelayedQueueChatService(0, "Plain answer with no script needed.");
        var client = CreateClient(chat, repair);
        var reply = await client.SendAsync("What is the status?");
        Assert.Contains("Plain answer", reply);
        Assert.Equal(0, repair.Calls); // ensure script generation path not used
    }

    [Fact]
    public async Task Cancellation_DuringAnswerStreaming() {
        var repair = new NoInvokeRepairExecutor();
        var chat = new DelayedQueueChatService(50, "LongAnswerThatWillBeCancelled");
        var client = CreateClient(chat, repair);
        using var cts = new CancellationTokenSource();
        var task = Task.Run(async () => {
            await foreach (var _ in client.SendStreamingAsync("question", cts.Token)) { /* consume */ }
        });
        cts.CancelAfter(60); // after a couple of characters
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
    }

    [Fact]
    public async Task Concurrent_Executions_IsolatedClients() {
        var tasks = new List<Task<string>>();
        for (int i = 0; i < 5; i++) {
            int local = i;
            var chat = new DelayedQueueChatService(0, "script", $"return {local};", $"Explanation {local}");
            var root = new RootOperator(new object());
            var client = new AsonClient(chat, root, Snapshot, new AsonClientOptions { SkipReceptionAgent = true, SkipExplainerAgent = false, ExecutionMode = ExecutionMode.InProcess }, null, new KeywordScriptValidator(Array.Empty<string>()), null);
            tasks.Add(client.SendAsync($"Task {local}"));
        }
        var results = await Task.WhenAll(tasks);
        for (int i = 0; i < results.Length; i++) Assert.Contains(i.ToString(), results[i]);
    }

    [Fact]
    public async Task ProxyRegeneration_Unchanged_When_No_New_Operators() {
        var chat = new DelayedQueueChatService(0, "script", "return 1;", "Expl");
        var root = new RootOperator(new object());
        var client = new AsonClient(chat, root, Snapshot, new AsonClientOptions { SkipReceptionAgent = true, SkipExplainerAgent = false, ExecutionMode = ExecutionMode.InProcess });
        // capture proxies via reflection
        var field = typeof(AsonClient).GetField("_proxies", BindingFlags.NonPublic | BindingFlags.Instance);
        string first = (string)field!.GetValue(client)!;
        _ = await client.SendAsync("task 1");
        string second = (string)field!.GetValue(client)!;
        Assert.Same(first, second); // reference equality implies not rebuilt
    }

    [Fact]
    public async Task LogOrdering_ScriptExecution() {
        var chat = new DelayedQueueChatService(0, "script", "return 2;", "Explanation 2");
        var root = new RootOperator(new object());
        var logs = new List<string>();
        var client = new AsonClient(chat, root, Snapshot, new AsonClientOptions { SkipReceptionAgent = true, SkipExplainerAgent = false, ExecutionMode = ExecutionMode.InProcess });
        client.Log += (s,e)=> logs.Add(e.Message);
        _ = await client.SendAsync("calc");
        // ensure execution success appears before any explanation generated log (if present)
        int execIdx = logs.FindIndex(l => l.Contains("Execution success"));
        int explIdx = logs.FindIndex(l => l.Contains("Explanation"));
        if (explIdx >=0) Assert.True(execIdx >=0 && execIdx < explIdx, "Execution log should precede explanation log");
    }

    [Fact]
    public async Task LargeStreamingScriptAssembly() {
        var streamer = new FragmentedScriptWordService();
        var root = new RootOperator(new object());
        var client = new AsonClient(streamer, root, Snapshot, new AsonClientOptions { SkipReceptionAgent = false, SkipExplainerAgent = true, ExecutionMode = ExecutionMode.InProcess });
        var chunks = new List<string>();
        await foreach (var c in client.SendStreamingAsync("task")) chunks.Add(c);
        string combined = string.Concat(chunks);
        Assert.DoesNotContain("script", combined, System.StringComparison.OrdinalIgnoreCase);
    }

    // Service streaming the word 'script' one letter at a time then returns minimal script when asked again (script agent path)
    private sealed class FragmentedScriptWordService : IChatCompletionService {
        private int _phase; // 0 answer agent, 1 script agent
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();
        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default) {
            if (_phase == 0) { _phase++; IReadOnlyList<ChatMessageContent> list = new List<ChatMessageContent>{ new ChatMessageContent(AuthorRole.Assistant, "script") }; return Task.FromResult(list); }
            IReadOnlyList<ChatMessageContent> list2 = new List<ChatMessageContent>{ new ChatMessageContent(AuthorRole.Assistant, "return 10;") };
            return Task.FromResult(list2);
        }
        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            if (_phase == 0) {
                foreach (var ch in new[]{'s','c','r','i','p','t'}) { yield return new StreamingChatMessageContent(AuthorRole.Assistant, ch.ToString()); await Task.Yield(); }
                _phase++;
            } else {
                yield return new StreamingChatMessageContent(AuthorRole.Assistant, "return 10;");
            }
        }
    }
}
