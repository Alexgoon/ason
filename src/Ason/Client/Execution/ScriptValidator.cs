using System;
using System.Collections.Generic;
using System.Linq;

namespace Ason.Client.Execution;

public interface IScriptValidator {
    string? Validate(string script);
}

internal sealed class KeywordScriptValidator : IScriptValidator {
    readonly string[] _forbidden;
    public KeywordScriptValidator(IEnumerable<string> forbidden) { _forbidden = forbidden.ToArray(); }
    public string? Validate(string script) {
        if (string.IsNullOrWhiteSpace(script)) return "Empty script";
        foreach (var pattern in _forbidden) if (script.Contains(pattern, StringComparison.Ordinal)) return $"Forbidden usage detected: {pattern}";
        return null;
    }
}
