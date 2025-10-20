using System.Text.Json.Serialization;
namespace AsonRunner.Protocol;
public interface IRunnerMessage { string Id { get; } string Type { get; } }
public abstract record RunnerMessage(string Id, string Type) : IRunnerMessage;
public sealed record ExecRequest(string Id, string Code) : RunnerMessage(Id, "exec");
public sealed record ExecResult(string Id, object? Result, string? Error = null) : RunnerMessage(Id, "execResult");
public sealed record InvokeRequest(string Id, string Target, string Method, object?[]? Args, string? HandleId) : RunnerMessage(Id, "invoke");
public sealed record InvokeResult(string Id, object? Result, string? Error = null) : RunnerMessage(Id, "invokeResult");
public sealed record McpInvokeRequest(string Id, string Server, string Tool, IDictionary<string, object?>? Arguments) : RunnerMessage(Id, "invokeMcp");
public sealed record LogMessage(string Id, string Level, string Message, string? Source = null, string? Exception = null) : RunnerMessage(Id, "log");
