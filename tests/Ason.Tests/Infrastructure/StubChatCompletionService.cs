using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;

namespace Ason.Tests.Infrastructure;

// Deterministic stub implementing just enough of IChatCompletionService for tests.
internal sealed class StubChatCompletionService : IChatCompletionService {
    private readonly ConcurrentQueue<string> _scriptReplies = new();
    private readonly ConcurrentQueue<string> _receptionReplies = new();
    private readonly ConcurrentQueue<string> _explainerReplies = new();

    public void EnqueueScript(params string[] replies) { foreach (var r in replies) _scriptReplies.Enqueue(r); }
    public void EnqueueReception(params string[] replies) { foreach (var r in replies) _receptionReplies.Enqueue(r); }
    public void EnqueueExplainer(params string[] replies) { foreach (var r in replies) _explainerReplies.Enqueue(r); }

    private string DequeueOrDefault(ConcurrentQueue<string> q, string def) => q.TryDequeue(out var v) ? v : def;

    // Provide empty dictionary to satisfy non-nullable contract
    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    // Return a single assistant message choosing priority: script -> reception -> explainer
    public async IAsyncEnumerable<ChatMessageContent> GetChatMessageContentsAsync(IEnumerable<ChatMessageContent> messages, ChatOptions? options = null, Kernel? kernel = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
        string text;
        if (!_scriptReplies.IsEmpty) text = DequeueOrDefault(_scriptReplies, "// default script\nreturn null;");
        else if (!_receptionReplies.IsEmpty) text = DequeueOrDefault(_receptionReplies, "script");
        else if (!_explainerReplies.IsEmpty) text = DequeueOrDefault(_explainerReplies, "Explanation");
        else text = "script";
        yield return new ChatMessageContent(AuthorRole.Assistant, text);
        await Task.CompletedTask;
    }

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(string prompt, ChatOptions? options = null, Kernel? kernel = null, CancellationToken cancellationToken = default) {
        IReadOnlyList<ChatMessageContent> list = new List<ChatMessageContent> { new ChatMessageContent(AuthorRole.Assistant, "script") };
        return Task.FromResult(list);
    }

    // Streaming API expected by interface (map to non-streaming for tests)
    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var list = await GetChatMessageContentsAsync("prompt", null, kernel, cancellationToken);
        foreach (var msg in list) {
            yield return new StreamingChatMessageContent(msg.Role, msg.Content);
        }
    }

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default) {
        IReadOnlyList<ChatMessageContent> list = new List<ChatMessageContent> { new ChatMessageContent(AuthorRole.Assistant, "script") };
        return Task.FromResult(list);
    }
}
