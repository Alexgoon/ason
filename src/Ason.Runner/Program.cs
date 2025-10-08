using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AsonHostInterop;
using System.Runtime.CompilerServices;
using AsonRunner.Protocol;
using AsonRunner; // ensure core reference

namespace AsonRunnerProcess;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private static readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement?>> Pending = new();
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    private static async Task<int> Main(string[] args)
    {
        var stdin = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();
        using var reader = new StreamReader(stdin, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 16 * 1024);
        using var writer = new StreamWriter(stdout, new UTF8Encoding(false)) { AutoFlush = true };

        var cts = new CancellationTokenSource();

        await LogInfoAsync(writer, "Runner started. Waiting for commands...").ConfigureAwait(false);

        var readLoop = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null) break; // EOF
                if (line.Length == 0) continue;

                IRunnerMessage? msg = null;
                try { msg = RunnerMessageSerializer.Deserialize(line); }
                catch (Exception ex) { await LogErrorAsync(writer, "Deserialize error", ex).ConfigureAwait(false); continue; }
                if (msg is null) { await LogInfoAsync(writer, "Unknown raw message").ConfigureAwait(false); continue; }

                switch (msg)
                {
                    case ExecRequest er:
                        await LogInfoAsync(writer, $"Exec received. Id={er.Id}, length={er.Code.Length}").ConfigureAwait(false);
                        _ = Task.Run(() => HandleExecAsync(er, writer));
                        break;
                    case InvokeResult ir:
                        if (Pending.TryRemove(ir.Id, out var tcs))
                        {
                            if (!string.IsNullOrEmpty(ir.Error)) tcs.TrySetException(new Exception(ir.Error));
                            else if (ir.Result is JsonElement je) tcs.TrySetResult(je.Clone());
                            else if (ir.Result is null) tcs.TrySetResult(null);
                            else
                            {
                                var elem = JsonSerializer.SerializeToElement(ir.Result, Json);
                                tcs.TrySetResult(elem);
                            }
                        }
                        break;
                    default:
                        await LogInfoAsync(writer, $"Unhandled message type: {msg.Type}").ConfigureAwait(false);
                        break;
                }
            }
        }, cts.Token);

        await readLoop.ConfigureAwait(false);
        await LogInfoAsync(writer, "Runner exiting read loop.").ConfigureAwait(false);
        return 0;
    }

    private static async Task HandleExecAsync(ExecRequest exec, StreamWriter writer)
    {
        try
        {
            var bridge = new OutBridge(writer);
            await LogInfoAsync(writer, $"Evaluating script. Id={exec.Id}").ConfigureAwait(false);
            var result = await AsonRunner.ScriptExecutor.EvaluateAsync(exec.Code, bridge).ConfigureAwait(false);
            JsonElement? payload = null;
            if (result is not null) payload = JsonSerializer.SerializeToElement(result, Json);
            await WriteMessageAsync(writer, new ExecResult(exec.Id, payload));
            await LogInfoAsync(writer, $"Exec completed. Id={exec.Id}, hasResult={(payload.HasValue ? "true" : "false")} ").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await LogErrorAsync(writer, $"Exec failed. Id={exec.Id}", ex).ConfigureAwait(false);
            await WriteMessageAsync(writer, new ExecResult(exec.Id, null, ex.ToString()));
        }
    }

    private static async Task WriteMessageAsync(StreamWriter writer, IRunnerMessage message)
    {
        var json = RunnerMessageSerializer.Serialize(message);
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try { await writer.WriteLineAsync(json).ConfigureAwait(false); }
        finally { WriteGate.Release(); }
    }

    private static Task LogInfoAsync(StreamWriter writer, string message, [CallerMemberName] string? member = null)
        => LogAsync(writer, "Information", message, null, member);

    private static Task LogErrorAsync(StreamWriter writer, string message, Exception? ex = null, [CallerMemberName] string? member = null)
        => LogAsync(writer, "Error", message, ex, member);

    private static Task LogAsync(StreamWriter writer, string level, string message, Exception? ex = null, [CallerMemberName] string? member = null)
        => WriteMessageAsync(writer, new LogMessage(Guid.NewGuid().ToString("N"), level, message, member, ex?.ToString()));

    private sealed class OutBridge : IHostBridge
    {
        private readonly StreamWriter _writer;
        public OutBridge(StreamWriter writer) => _writer = writer;

        public Task LogAsync(string level, string message) => Program.LogAsync(_writer, level, message);

        public async Task<T?> InvokeAsync<T>(string target, string method, object?[]? args = null, string? handleId = null)
        {
            string id = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);
            Pending[id] = tcs;
            var req = new InvokeRequest(id, target, method, args, handleId);
            await WriteMessageAsync(_writer, req).ConfigureAwait(false);
            var result = await tcs.Task.ConfigureAwait(false);
            if (!result.HasValue) return default;
            var elem = result.Value;
            if (typeof(T) == typeof(JsonElement)) return (T)(object)elem;
            return elem.Deserialize<T>(Json);
        }

        public async Task<T?> InvokeMcpAsync<T>(string server, string tool, IDictionary<string, object?>? arguments = null)
        {
            string id = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);
            Pending[id] = tcs;
            var req = new McpInvokeRequest(id, server, tool, arguments);
            await WriteMessageAsync(_writer, req).ConfigureAwait(false);
            var result = await tcs.Task.ConfigureAwait(false);
            if (!result.HasValue) return default;
            var elem = result.Value;
            if (typeof(T) == typeof(JsonElement)) return (T)(object)elem;
            return elem.Deserialize<T>(Json);
        }
    }
}
