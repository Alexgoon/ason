using System.Text;

namespace Ason;

// Shared defaults for generated script prelude and filtering
internal static class ScriptGenDefaults
{
    // Default using directives that are always prepended to generated scripts
    public static readonly string[] DefaultUsings = new[]
    {
        "using System;",
        "using System.Linq;",
        "using System.Threading.Tasks;",
        "using System.Collections.Generic;",
        "using System.Collections.ObjectModel;",
        "using System.Text.Json;",
        "using AsonHostInterop;"
    };

    // Helper to produce the standard using prelude block with newlines
    public static string GetUsingsPrelude()
    {
        var sb = new StringBuilder();
        foreach (var u in DefaultUsings)
        {
            sb.AppendLine(u);
        }
        return sb.ToString();
    }
}
