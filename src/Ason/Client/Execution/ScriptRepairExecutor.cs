using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Ason; // ExecOutcome

namespace Ason.Client.Execution;

// Internal because RunnerClient is internal; keeps accessibility consistent
internal interface IScriptRepairExecutor {
    Task<ExecOutcome> ExecuteWithRepairsAsync(
        string userTask,
        int maxAttempts,
        string? proxies,
        ChatCompletionAgent scriptAgent,
        RunnerClient runner,
        IScriptValidator validator,
        Action<LogLevel, string, Exception?> log,
        CancellationToken ct);
}

internal sealed class ScriptRepairExecutor : IScriptRepairExecutor {
    public async Task<ExecOutcome> ExecuteWithRepairsAsync(
        string userTask,
        int maxAttempts,
        string? proxies,
        ChatCompletionAgent scriptAgent,
        RunnerClient runner,
        IScriptValidator validator,
        Action<LogLevel, string, Exception?> log,
        CancellationToken ct) {
        string? lastScript = null;
        string? lastError = null;
        for (int attempt = 0; attempt <= maxAttempts; attempt++) {
            ct.ThrowIfCancellationRequested();
            string prompt = BuildScriptPrompt(userTask, lastScript, lastError, attempt);
            log(LogLevel.Debug, $"ScriptAgent input:\n{prompt}", null);
            string rawReply = await InvokeTextAgentAsync(scriptAgent, prompt, ct);

            // If the model indicates impossibility with a leading 'Cannot', short-circuit and surface message
            var trimmed = rawReply.TrimStart();
            if (trimmed.StartsWith("Cannot", StringComparison.OrdinalIgnoreCase)) {
                log(LogLevel.Information, "ScriptAgent reported impossibility; returning message directly.", null);
                return new ExecOutcome(false, null, trimmed.Trim(), null, attempt + 1);
            }

            string userScript = ScriptReplyProcessor.Process(rawReply);

            log(LogLevel.Debug, $"ScriptAgent outout (attempt {attempt + 1}):\n{userScript}", null);
            var validationError = validator.Validate(userScript);
            if (validationError is not null) {
                lastError = validationError;
                lastScript = userScript;
                log(LogLevel.Warning, $"Validation failed: {validationError}", null);
                if (attempt == maxAttempts)
                    return new ExecOutcome(false, null, validationError, userScript, attempt + 1);
                continue;
            }
            if (string.IsNullOrWhiteSpace(proxies)) return new ExecOutcome(false, null, "Proxies not initialized", userScript, attempt + 1);
            string code = proxies + "\n" + userScript;
            try {
                var execResult = await runner.ExecuteAsync(code, ct).ConfigureAwait(false);
                string? raw = execResult?.ToString();
                log(LogLevel.Information, $"Execution success. RawResult={raw ?? "null"}", null);
                return new ExecOutcome(true, string.IsNullOrWhiteSpace(raw) || raw == "null" ? null : raw, null, userScript, attempt + 1);
            }
            catch (OperationCanceledException) {
                throw; // propagate cancellation directly
            }
            catch (Exception ex) {
                if (ex.Message.Contains("Task was cancelled", StringComparison.OrdinalIgnoreCase)) {
                    return new ExecOutcome(false, null, "Task was cancelled", userScript, attempt + 1);
                }
                lastError = ex.Message;
                lastScript = userScript;
                log(LogLevel.Error, "Execution error", ex);
                if (attempt == maxAttempts) return new ExecOutcome(false, null, ex.Message, userScript, attempt + 1);
            }
        }
        return new ExecOutcome(false, null, "Unknown error", null, maxAttempts + 1);
    }

    static string BuildScriptPrompt(string userTask, string? previousScript, string? lastError, int attempt) {
        if (attempt == 0) return userTask;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Regenerate the script to accomplish the task, correcting the previous failure.");
        sb.AppendLine("<task>"); sb.AppendLine(userTask); sb.AppendLine("</task>");
        if (!string.IsNullOrWhiteSpace(lastError)) { sb.AppendLine("<lastError>"); sb.AppendLine(lastError); sb.AppendLine("</lastError>"); }
        if (!string.IsNullOrWhiteSpace(previousScript)) { sb.AppendLine("<previousScript>"); sb.AppendLine(previousScript); sb.AppendLine("</previousScript>"); }
        sb.AppendLine("Output ONLY executable C# script statements as per your instructions, or a single sentence starting with 'Cannot' if impossible.");
        return sb.ToString();
    }

    static async Task<string> InvokeTextAgentAsync(ChatCompletionAgent agent, string content, CancellationToken ct) {
        var sb = new System.Text.StringBuilder();
        var messages = new List<Microsoft.SemanticKernel.ChatMessageContent> { new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, content) };
        await foreach (var item in agent.InvokeAsync(messages, thread: null, options: null, cancellationToken: ct)) {
            var part = item.Message?.Content; if (!string.IsNullOrWhiteSpace(part)) sb.Append(part);
        }
        return sb.ToString();
    }
}
