using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;
using System.Text.Json;

namespace Ason;

internal class ExtractionOperator : RootOperator {
    public ExtractionOperator() : base(new object()) { // provide non-null attached object to satisfy NRT
    }

    [AsonMethod(@"Extracts specified property values from plain text. Returns a dictionary mapping each name to its extracted string value (missing -> null). Always use safe TryParse methods to parse values. Respect property types when casting. Use a discriptive multi-word string for each extracted value in propertiesToExtract.")]
    public async Task<Dictionary<string, string?>> ExtractDataFromText(string text, string[] propertiesToExtract) {
        if (propertiesToExtract is null || propertiesToExtract.Length == 0)
            throw new ArgumentException("No properties specified", nameof(propertiesToExtract));

        var orchestrator = AsonClient.CurrentInstance ?? throw new InvalidOperationException("Orchestrator is not initialized.");

        // Enhanced instructions with heuristics + few-shot examples to improve extraction of names, companies, etc.
        var agent = new ChatCompletionAgent {
            Name = "PropertyExtractorAgent",
            Instructions = "You are an information extraction agent. GIVEN: (1) a list of exact property names, (2) raw unstructured text. TASK: Return ONLY one minified JSON object. RULES: (a) Keys must be EXACTLY the provided property names (respect casing). (b) Each value is either a best‑guess substring from the text (trim surrounding punctuation/quotes) or null if no plausible candidate. (c) Never invent keys, never output explanations, comments, markdown or code fences. (d) Use best judgement; prefer a non‑null value when the text strongly implies it. Use null only when the text truly lacks an answer. (e) Do not output placeholders like 'unknown', '', 'n/a'. OUTPUT MUST be strictly valid minified JSON. EXAMPLES OUTPUT: {\"Name\":\"Jim\",\"Company\":\"Fabrikam Inc.\"}",
            Kernel = orchestrator.ReceptionKernel
        };

        var propsBlock = string.Join("\n", propertiesToExtract);
        // Expanded prompt giving the model a clear containerized spec without leaking examples again (examples live in system Instructions)
        var prompt = $@"Extract each requested property from the text. Output ONE minified JSON object only. If uncertain but a strong plausible value exists, return your best guess; otherwise null. Maintain key order if possible.\n<properties>\n{propsBlock}\n</properties>\n<text>\n{text ?? string.Empty}\n</text>";

        var sb = new StringBuilder();
        var messages = new[] { new ChatMessageContent(AuthorRole.User, prompt) };
        await foreach (var item in agent.InvokeAsync(messages, thread: null, options: null)) {
            var part = item.Message?.Content;
            if (!string.IsNullOrWhiteSpace(part)) sb.Append(part);
        }

        var raw = sb.ToString().Trim();
        // Light fence stripping if model adds them
        if (raw.StartsWith("```", StringComparison.Ordinal)) {
            raw = raw.Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                     .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
                     .Trim();
        }

        // Attempt to locate JSON object if extra text slipped in
        int firstBrace = raw.IndexOf('{');
        int lastBrace = raw.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace) {
            raw = raw[firstBrace..(lastBrace + 1)];
        }

        Dictionary<string, string?> result = new(StringComparer.OrdinalIgnoreCase);
        try {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object) {
                foreach (var propName in propertiesToExtract) {
                    if (doc.RootElement.TryGetProperty(propName, out var val)) {
                        string? strVal = val.ValueKind switch {
                            JsonValueKind.String => val.GetString(),
                            JsonValueKind.Number => val.GetRawText(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            JsonValueKind.Null => null,
                            JsonValueKind.Array or JsonValueKind.Object => val.GetRawText(),
                            _ => val.ToString()
                        };
                        result[propName] = strVal;
                    }
                    else {
                        result[propName] = null; // ensure key present
                    }
                }
            }
            else {
                throw new InvalidOperationException("Model reply did not contain a JSON object.");
            }
        }
        catch (Exception ex) {
            throw new InvalidOperationException("Failed to parse model output as JSON object.", ex);
        }

        return result;
    }
}

