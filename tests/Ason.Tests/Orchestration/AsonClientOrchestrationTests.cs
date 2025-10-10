using Xunit;
using Ason;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;
using Ason.Client.Execution;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using AsonRunner;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ason.CodeGen;
using System.Reflection;

namespace Ason.Tests.Orchestration;

public class AsonClientOrchestrationTests {

    private static readonly OperatorsLibrary Snapshot = new OperatorBuilder()
        .AddAssemblies(typeof(RootOperator).Assembly)
        .SetBaseFilter(mi => mi.GetCustomAttribute<AsonMethodAttribute>() != null)
        .Build();

    private sealed class FixedChatService : IChatCompletionService {
        private readonly Queue<string> _answers = new();
        public FixedChatService(params string[] responses) { foreach (var r in responses) _answers.Enqueue(r); }
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

        private string Next() => _answers.Count > 0 ? _answers.Dequeue() : "script";

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ChatMessageContent> list = new List<ChatMessageContent> { new ChatMessageContent(AuthorRole.Assistant, Next()) };
            return Task.FromResult(list);
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var list = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
            foreach (var msg in list)
            {
                var content = msg.Content ?? string.Empty; // capture non-null
                if (content.Length > 2 && content == "script") {
                    yield return new StreamingChatMessageContent(msg.Role, "scr");
                    yield return new StreamingChatMessageContent(msg.Role, "ipt ");
                }
                else if (content == "  SCRIPT\n") {
                    yield return new StreamingChatMessageContent(msg.Role, "  ");
                    yield return new StreamingChatMessageContent(msg.Role, "SCRIPT\n");
                }
                else {
                    yield return new StreamingChatMessageContent(msg.Role, content);
                }
            }
        }
    }

    private AsonClient CreateClient(IChatCompletionService svc, AsonClientOptions opts) {
        var root = new RootOperator(new object());
        var client = new AsonClient(svc, root, Snapshot, new AsonClientOptions {
            SkipReceptionAgent = opts.SkipReceptionAgent,
            SkipExplainerAgent = opts.SkipExplainerAgent,
            MaxFixAttempts = opts.MaxFixAttempts,
            ExecutionMode = ExecutionMode.InProcess
        });
        return client;
    }

    [Fact]
    public async Task Route_DirectAnswer_Path() {
        var svc = new FixedChatService("Plain answer");
        var opts = new AsonClientOptions { SkipReceptionAgent = false, SkipExplainerAgent = true };
        var client = CreateClient(svc, opts);
        var result = await client.SendAsync("What is test?");
        Assert.Contains("Plain answer", result);
    }

    [Fact]
    public async Task Route_Script_Path_With_Explanation() {
        var svc = new FixedChatService("script", "return 1;", "Explained result");
        var opts = new AsonClientOptions { SkipReceptionAgent = false, SkipExplainerAgent = false };
        var client = CreateClient(svc, opts);
        var reply = await client.SendAsync("Compute something");
        Assert.Contains("Explained", reply);
    }

    [Fact]
    public async Task Route_Script_Path_When_SkipAnswerAgent() {
        var svc = new FixedChatService("return 2;","Explained 2");
        var opts = new AsonClientOptions { SkipReceptionAgent = true, SkipExplainerAgent = false };
        var client = CreateClient(svc, opts);
        var reply = await client.SendAsync("Do it");
        Assert.Contains("Explained", reply);
    }


    [Fact]
    public async Task Route_Answer_EmptyFallsBack() {
        var svc = new FixedChatService("   ");
        var opts = new AsonClientOptions { SkipReceptionAgent = false, SkipExplainerAgent = true };
        var client = CreateClient(svc, opts);
        var reply = await client.SendAsync("Question");
        Assert.False(string.IsNullOrEmpty(reply));
    }

    [Fact]
    public async Task Streaming_Answer_Path() {
        var svc = new FixedChatService("Plain answer");
        var opts = new AsonClientOptions { SkipReceptionAgent = false, SkipExplainerAgent = true };
        var client = CreateClient(svc, opts);
        var chunks = new List<string>();
        await foreach (var c in client.SendStreamingAsync("hi")) chunks.Add(c);
        Assert.Contains(chunks, c => c.Contains("Plain answer"));
    }

    [Fact]
    public async Task Streaming_Script_Path_DirectSkipAnswer() {
        var svc = new FixedChatService("return 3;", "Explanation 3");
        var opts = new AsonClientOptions { SkipReceptionAgent = true, SkipExplainerAgent = false };
        var client = CreateClient(svc, opts);
        var list = new List<string>();
        await foreach (var c in client.SendStreamingAsync("task")) list.Add(c);
        Assert.True(list.Count > 0);
    }

    [Fact]
    public async Task Streaming_Answer_When_First_Word_Not_Script() {
        var svc = new FixedChatService("  SCRIPTING more text");
        var opts = new AsonClientOptions { SkipReceptionAgent = false, SkipExplainerAgent = true };
        var client = CreateClient(svc, opts);
        var list = new List<string>();
        await foreach (var c in client.SendStreamingAsync("task")) list.Add(c);
        Assert.Contains(list, s => s.Contains("SCRIPTING"));
    }

    [Fact]
    public async Task ExecuteScriptDirectAsync_ValidationBlocks() {
        var svc = new FixedChatService("script");
        var opts = new AsonClientOptions();
        var client = CreateClient(svc, opts);
        var output = await client.ExecuteScriptDirectAsync("System.Reflection.Assembly.Load(\"x\");", validate:true);
        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public async Task ExecuteScriptDirectAsync_Succeeds() {
        var svc = new FixedChatService("script");
        var opts = new AsonClientOptions();
        var client = CreateClient(svc, opts);
        var script = "return 123;";
        var output = await client.ExecuteScriptDirectAsync(script, validate:true);
        Assert.Contains("123", output);
    }

}
