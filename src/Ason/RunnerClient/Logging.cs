using Microsoft.Extensions.Logging;

namespace Ason;
public sealed class AsonLogEventArgs : EventArgs {
    public AsonLogEventArgs(LogLevel level, string message, string? exception = null, string? source = null) {
        Level = level; Message = message; Exception = exception; Source = source;
    }
    public LogLevel Level { get; }
    public string Message { get; }
    public string? Exception { get; }
    public string? Source { get; }
}

internal class LogHelper {
    internal static LogLevel ParseLogLevel(string? level) {
        return (level ?? "Information").Trim().ToLowerInvariant() switch {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" or "information" => LogLevel.Information,
            "warn" or "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "crit" or "critical" or "fatal" => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }
}