using System.Collections.Concurrent;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Agents;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Ason;
using Ason.Client.Execution;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using System.Text;

namespace Ason.Tests.Execution;

public class ScriptRepairExecutorTests {

    private sealed class QueueChatService : IChatCompletionService {
        private readonly ConcurrentQueue<string> _responses = new();
        public QueueChatService(params string[] replies) { foreach (var r in replies) _responses.Enqueue(r); }
        public void Enqueue(params string[] replies) { foreach (var r in replies) _responses.Enqueue(r); }
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();
        private string Next() => _responses.TryDequeue(out var v) ? v : string.Empty;
        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default) {
            IReadOnlyList<ChatMessageContent> list = new List<ChatMessageContent>{ new ChatMessageContent(AuthorRole.Assistant, Next())};
            return Task.FromResult(list);
        }
        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var list = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
            foreach (var m in list) yield return new StreamingChatMessageContent(m.Role, m.Content ?? string.Empty);
        }
    }

    private static ChatCompletionAgent CreateAgent(IChatCompletionService svc) {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(svc);
        var kernel = builder.Build();
        return new ChatCompletionAgent { Name = "ScriptGenerator", Instructions = "gen", Kernel = kernel };
    }

    private static RunnerClient CreateInProcRunner() => new RunnerClient(new ConcurrentDictionary<string, OperatorBase>(), synchronizationContext: null) { Mode = AsonRunner.ExecutionMode.InProcess };

    private sealed class TestValidator : IScriptValidator {
        private readonly Func<string, string?> _fn; public TestValidator(Func<string,string?> fn) { _fn = fn; }
        public string? Validate(string script) => _fn(script);
    }

    private static string GenerateProxies() => ProxySerializer.SerializeAll(typeof(RootOperator).Assembly);

    private static ScriptRepairExecutor Executor() => new();

    private static Task<ExecOutcome> RunAsync(ScriptRepairExecutor exec, string userTask, int maxAttempts, string[] agentReplies, IScriptValidator validator, string? proxies = null, CancellationToken ct = default) {
        var svc = new QueueChatService(agentReplies); // replies consumed sequentially by agent attempts
        var agent = CreateAgent(svc);
        var runner = CreateInProcRunner();
        return exec.ExecuteWithRepairsAsync(userTask, maxAttempts, proxies, agent, runner, validator, (lvl,msg,ex)=>{}, ct);
    }

    [Fact]
    public async Task Exceeds_Max_Attempts_Fails() {
        var outcome = await RunAsync(Executor(), "task", 1, new[]{"BAD 1;","BAD 2;"}, new TestValidator(_=>"always bad"), GenerateProxies());
        Assert.False(outcome.Success);
        Assert.Equal(2, outcome.Attempts); // attempts = maxAttempts + 1
        Assert.Contains("always bad", outcome.ErrorMessage);
    }


    [Fact]
    public async Task Proxies_Missing_Fails() {
        var outcome = await RunAsync(Executor(), "task", 0, new[]{"return 1;"}, new TestValidator(_=>null), proxies:null);
        Assert.False(outcome.Success);
        Assert.Equal("Proxies not initialized", outcome.ErrorMessage);
    }

    [Fact]
    public async Task Cancellation_Throws() {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async ()=> {
            await RunAsync(Executor(), "task", 2, new[]{"return 1;"}, new TestValidator(_=>null), GenerateProxies(), cts.Token);
        });
    }
}
