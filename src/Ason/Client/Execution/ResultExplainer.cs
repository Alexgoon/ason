using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Ason.Client.Execution;

public interface IResultExplainer {
    Task<string> ExplainAsync(string task, string rawResult, ChatCompletionAgent explainerAgent, Action<LogLevel,string,Exception?> log, CancellationToken ct);
}

internal sealed class ResultExplainer : IResultExplainer {
    public async Task<string> ExplainAsync(string task, string rawResult, ChatCompletionAgent explainerAgent, Action<LogLevel,string,Exception?> log, CancellationToken ct) {
        var input = "<task>\n" + task + "\n</task>\n" + "<result>\n" + rawResult + "\n</result>";
        var sb = new System.Text.StringBuilder();
        var messages = new Microsoft.SemanticKernel.ChatMessageContent[] { new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, input) };
        await foreach (var item in explainerAgent.InvokeAsync(messages, thread: null, options: null, cancellationToken: ct)) {
            var part = item.Message?.Content; if (!string.IsNullOrWhiteSpace(part)) sb.Append(part);
        }
        var full = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(full)) {
            string fallback = string.IsNullOrWhiteSpace(rawResult) ? "Task completed" : rawResult;
            log(LogLevel.Information, "Explanation empty – using fallback", null);
            return fallback;
        }
        log(LogLevel.Debug, "Explanation generated", null);
        return full;
    }
}
