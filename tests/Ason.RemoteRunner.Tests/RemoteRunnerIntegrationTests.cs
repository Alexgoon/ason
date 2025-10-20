using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Ason;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Ason.CodeGen;
using AsonRunner;

namespace Ason.RemoteRunner.Tests;

public class RemoteRunnerIntegrationTests {
    private static OperatorsLibrary Snapshot = new OperatorBuilder()
        .AddAssemblies(typeof(RootOperator).Assembly)
        .SetBaseFilter(mi => mi.GetCustomAttribute<ProxyMethodAttribute>() != null)
        .Build();

    private sealed class DummyChat : IChatCompletionService {
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();
        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default) {
            IReadOnlyList<ChatMessageContent> list = new List<ChatMessageContent>{ new ChatMessageContent(AuthorRole.Assistant, "script")};
            return Task.FromResult(list);
        }
        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, "script");
            await Task.CompletedTask;
        }
    }

    [Fact]
    public async Task EnableRemoteRunner_SetsFlags_And_StartsTransport() {
        var chat = new DummyChat();
        var root = new RootOperator(new object());
        var client = new AsonClient(chat, root, Snapshot, new AsonClientOptions { SkipAnswerAgent = true, RunnerMode = ExecutionMode.ExternalProcess });
        var runnerField = typeof(AsonClient).GetField("_runner", BindingFlags.NonPublic|BindingFlags.Instance);
        var runner = runnerField!.GetValue(client)!;
        var useRemoteProp = runner.GetType().GetProperty("UseRemote");
        Assert.False((bool)useRemoteProp!.GetValue(runner)!);
        await client.EnableRemoteRunnerAsync("http://localhost:5000", stopLocalIfRunning:true);
        Assert.True((bool)useRemoteProp.GetValue(runner)!);
        var remoteUrlProp = runner.GetType().GetProperty("RemoteUrl");
        Assert.Equal("http://localhost:5000", (string)remoteUrlProp!.GetValue(runner)!);
    }
}
