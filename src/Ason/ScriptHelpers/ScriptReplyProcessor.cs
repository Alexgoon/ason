namespace Ason;

// Processes raw replies from the ScriptAgent into executable C# script
internal static class ScriptReplyProcessor
{
    // Entry point: clean up the raw agent reply into a script body
    public static string Process(string? agentReply)
    {
        if (string.IsNullOrWhiteSpace(agentReply)) return string.Empty;
        string cleaned = StripCodeFences(agentReply);
        cleaned = RemoveBlockComments(cleaned);
        cleaned = StripDuplicateUsings(cleaned);
        cleaned = CollapseExcessBlankLines(cleaned);
        return cleaned.Trim();
    }

    // Remove markdown code fences commonly returned by models
    private static string StripCodeFences(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        string t = text.Replace("```csharp", string.Empty, StringComparison.OrdinalIgnoreCase)
                       .Replace("```cs", string.Empty, StringComparison.OrdinalIgnoreCase)
                       .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
                       .Trim();
        return t;
    }

    private static string RemoveBlockComments(string text) {
        int start; while ((start = text.IndexOf("/*", StringComparison.Ordinal)) >= 0) {
            int end = text.IndexOf("*/", start+2, StringComparison.Ordinal);
            if (end < 0) { text = text.Remove(start); break; }
            text = text.Remove(start, end - start + 2);
        }
        return text;
    }

    // Filter out using directives that are already included in the standard prelude
    private static string StripDuplicateUsings(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var existing = ScriptGenDefaults.DefaultUsings;
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var filtered = lines.Where(l => {
            var trim = l.Trim();
            if (trim.StartsWith("using ")) return !existing.Any(e => string.Equals(trim, e, StringComparison.Ordinal));
            return true;
        });
        return string.Join(Environment.NewLine, filtered);
    }

    private static bool StartsWithAny(string value, params string[] tokens) => tokens.Any(t => value.StartsWith(t, StringComparison.Ordinal));


    private static string CollapseExcessBlankLines(string text) {
        return System.Text.RegularExpressions.Regex.Replace(text, "(\r?\n){3,}", Environment.NewLine + Environment.NewLine);
    }

}
