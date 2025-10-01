using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Agents;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Ason.Client.Execution;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Ason.Tests.Execution;

public class ResultExplainerTests {
    private sealed class FixedService : IChatCompletionService {
        private readonly string[] _responses; int _idx; readonly bool _throwAfterFirst;
        public FixedService(string[] responses, bool throwAfterFirst = false) { _responses = responses; _throwAfterFirst = throwAfterFirst; }
        public FixedService(params string[] responses) { _responses = responses; }
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();
        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default) {
            if (_throwAfterFirst && _idx > 0) throw new TaskCanceledException("Simulated early cancel");
            string r = _idx < _responses.Length ? _responses[_idx++] : string.Empty;
            IReadOnlyList<ChatMessageContent> list = new List<ChatMessageContent>{ new ChatMessageContent(AuthorRole.Assistant, r) };
            return Task.FromResult(list);
        }
        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var list = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
            foreach (var m in list) yield return new StreamingChatMessageContent(m.Role, m.Content ?? string.Empty);
        }
    }

    private static ChatCompletionAgent Agent(IChatCompletionService svc) {
        var builder = Kernel.CreateBuilder(); builder.Services.AddSingleton<IChatCompletionService>(svc); var kernel = builder.Build();
        return new ChatCompletionAgent { Name = "Explainer", Instructions = "expl", Kernel = kernel };
    }

    [Fact]
    public async Task ExplainAsync_Basic() {
        var explainer = new ResultExplainer();
        var svc = new FixedService("This is explanation.");
        var ag = Agent(svc);
        string result = await explainer.ExplainAsync("task", "42", ag, (l,m,e)=>{}, CancellationToken.None);
        Assert.Contains("explanation", result, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExplainAsync_EmptyRawResult() {
        var explainer = new ResultExplainer();
        var svc = new FixedService("Handled empty");
        var ag = Agent(svc);
        string result = await explainer.ExplainAsync("task", string.Empty, ag, (l,m,e)=>{}, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(result));
    }


    [Fact]
    public async Task ExplainAsync_LogsDebug() {
        LogLevel? captured = null; string? msg = null;
        var explainer = new ResultExplainer();
        var svc = new FixedService("done");
        var ag = Agent(svc);
        string result = await explainer.ExplainAsync("t","r", ag, (l,m,e)=> { captured = l; msg = m; }, CancellationToken.None);
        Assert.Equal(LogLevel.Debug, captured);
        Assert.False(string.IsNullOrEmpty(msg));
    }

    [Fact]
    public async Task ExplainAsync_EmptyModelResponse_FallbacksToRaw() {
        LogLevel? captured = null; string? logMsg = null;
        var explainer = new ResultExplainer();
        var svc = new FixedService("   ");
        var ag = Agent(svc);
        string raw = "RAW_OUTPUT";
        string result = await explainer.ExplainAsync("task", raw, ag, (lvl,msg,ex)=> { captured = lvl; logMsg = msg; }, CancellationToken.None);
        Assert.Equal(raw, result);
        Assert.Equal(LogLevel.Information, captured);
        Assert.False(string.IsNullOrEmpty(logMsg));
    }
}
