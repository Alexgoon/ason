using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Ason;
using Ason.Client.Execution;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AsonRunner;
using System.Collections.Generic;
using System.Linq;
using Ason.CodeGen;
using System.Reflection;

namespace Ason.Tests.Orchestration;

public class AsonClientEndToEndTests {
    private static OperatorsLibrary Snapshot = new OperatorBuilder()
        .AddAssemblies(typeof(RootOperator).Assembly)
        .SetBaseFilter(mi => mi.GetCustomAttribute<AsonMethodAttribute>() != null)
        .Build();

    // Simple deterministic chat service that pops queued replies sequentially.
    private sealed class QueueChatService : IChatCompletionService {
        private readonly ConcurrentQueue<string> _replies = new();
        public QueueChatService(IEnumerable<string> replies) { foreach (var r in replies) _replies.Enqueue(r); }
        public QueueChatService(params string[] replies) : this((IEnumerable<string>)replies) { }
        public void Enqueue(params string[] replies) { foreach (var r in replies) _replies.Enqueue(r); }
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();
        private string Next() => _replies.TryDequeue(out var v) ? v : string.Empty;
        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default) {
            IReadOnlyList<ChatMessageContent> list = new List<ChatMessageContent>{ new ChatMessageContent(AuthorRole.Assistant, Next()) };
            return Task.FromResult(list);
        }
        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var list = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
            foreach (var m in list) yield return new StreamingChatMessageContent(m.Role, m.Content);
        }
    }

    private static AsonClient CreateClient(
        IChatCompletionService scriptSvc,
        IChatCompletionService? explainerSvc = null,
        IChatCompletionService? answerSvc = null,
        AsonClientOptions? opts = null,
        IScriptValidator? validator = null) {
        var root = new RootOperator(new object());
        var options = opts ?? new AsonClientOptions();
        options = new AsonClientOptions {
            MaxFixAttempts = options.MaxFixAttempts,
            SkipReceptionAgent = options.SkipReceptionAgent,
            SkipExplainerAgent = options.SkipExplainerAgent,
            ScriptChatCompletion = scriptSvc,
            ReceptionChatCompletion = answerSvc ?? scriptSvc,
            ExplainerChatCompletion = explainerSvc ?? scriptSvc,
            ForbiddenScriptKeywords = new string[0],
            AllowTextExtractor = true,
            ExecutionMode = ExecutionMode.InProcess
        };
        var client = new AsonClient(scriptSvc, root, Snapshot, options, null, validator, null);
        return client;
    }

    [Fact]
    public async Task E2E_HappyPath_ScriptAndExplanation() {
        var scriptSvc = new QueueChatService("return 5;");
        var explainerSvc = new QueueChatService("The result is 5.");
        var validator = new KeywordScriptValidator(System.Array.Empty<string>());
        var client = CreateClient(scriptSvc, explainerSvc: explainerSvc, answerSvc: new QueueChatService("script"),
            opts: new AsonClientOptions { SkipReceptionAgent = true, SkipExplainerAgent = false }, validator: validator);
        List<(LogLevel lvl,string msg)> logs = new();
        client.Log += (s,e)=> logs.Add((e.Level, e.Message));
        var reply = await client.SendAsync("Compute 2+3");
        Assert.Contains("5", reply);
        Assert.True(reply.IndexOf("result", System.StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.Contains(logs, l => l.msg.Contains("Execution success"));
    }

    [Fact]
    public async Task E2E_ValidationFailure_Then_Repair() {
        var scriptSvc = new QueueChatService("BAD return 1;", "return 2;");
        var validator = new TestValidator(s => s.Contains("BAD") ? "Validation failed" : null);
        var client = CreateClient(scriptSvc, answerSvc: new QueueChatService("script"),
            opts: new AsonClientOptions { SkipReceptionAgent = true, SkipExplainerAgent = true, MaxFixAttempts = 2 }, validator: validator);
        List<string> logMessages = new();
        client.Log += (s,e)=> logMessages.Add(e.Message);
        var reply = await client.SendAsync("Task");
        Assert.Contains("2", reply);
        Assert.Contains(logMessages, m => m.Contains("Validation failed"));
    }


    [Fact]
    public async Task E2E_RuntimeException_Then_Repair() {
        var scriptSvc = new QueueChatService("throw new System.Exception(\"boom\");", "return 7;");
        var validator = new TestValidator(_ => null);
        var client = CreateClient(scriptSvc, answerSvc: new QueueChatService("script"),
            opts: new AsonClientOptions { SkipReceptionAgent = true, SkipExplainerAgent = true, MaxFixAttempts = 2 }, validator: validator);
        List<string> logs = new();
        client.Log += (s,e)=> logs.Add(e.Message);
        var reply = await client.SendAsync("Compute");
        Assert.Contains("7", reply);
        Assert.Contains(logs, m => m.Contains("Execution error"));
    }

    [Fact]
    public async Task E2E_Explanation_Fallback_When_Empty() {
        var scriptSvc = new QueueChatService("return 9;");
        var explainerSvc = new QueueChatService("   ");
        var validator = new TestValidator(_ => null);
        var client = CreateClient(scriptSvc, explainerSvc: explainerSvc, answerSvc: new QueueChatService("script"),
            opts: new AsonClientOptions { SkipReceptionAgent = true, SkipExplainerAgent = false }, validator: validator);
        var reply = await client.SendAsync("Compute");
        Assert.Equal("9", reply);
    }

    // Simple validator wrapper for tests
    private sealed class TestValidator : IScriptValidator {
        private readonly System.Func<string,string?> _fn;
        public TestValidator(System.Func<string,string?> fn) { _fn = fn; }
        public string? Validate(string script) => _fn(script);
    }
}
