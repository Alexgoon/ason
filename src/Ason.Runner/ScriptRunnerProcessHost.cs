using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace AsonRunner;

/// <summary>
/// Hosts an external ScriptRunner process (dotnet or docker) and exposes a simple line-oriented async API.
/// Shared by remote SignalR host (AsonRemoteRunner) and local RunnerClient (ScriptOperator) to avoid duplication.
/// </summary>
public sealed class ScriptRunnerProcessHost : IAsyncDisposable {
    readonly ExecutionMode _mode;
    readonly string? _dockerImage;
    readonly ILogger? _logger;

    Process? _process;
    StreamWriter? _stdin;
    StreamReader? _stdout;
    CancellationTokenSource _cts = new();

    public event Func<string, Task>? LineReceived;            // Raised for each stdout line
    public event Func<string, Task>? ProcessExited;           // Raised when process exits (reason string)

    public ScriptRunnerProcessHost(ExecutionMode mode, string? dockerImage, ILogger? logger) {
        _mode = mode;
        _dockerImage = dockerImage;
        _logger = logger;
    }

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync() {
        if (_mode == ExecutionMode.InProcess) throw new InvalidOperationException("InProcess mode does not require external host.");
        if (IsRunning) return;

        var si = new ProcessStartInfo {
            FileName = _mode == ExecutionMode.Docker ? "docker" : "dotnet",
            Arguments = _mode == ExecutionMode.Docker ? BuildDockerArgs(_dockerImage) : $"\"{typeof(ScriptExecutor).Assembly.Location}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardInputEncoding = new UTF8Encoding(false),
            CreateNoWindow = true
        };

        _process = new Process { StartInfo = si, EnableRaisingEvents = true };
        _process.Exited += async (_, _) => {
            try { if (ProcessExited != null) await ProcessExited.Invoke("exited"); } catch { }
        };
        _process.ErrorDataReceived += (_, e) => {
            if (!string.IsNullOrEmpty(e.Data)) _logger?.LogDebug("[ScriptRunner stderr] {Line}", e.Data);
        };
        _process.Start();
        _process.BeginErrorReadLine();

        _stdin = new StreamWriter(_process.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = true };
        _stdout = new StreamReader(_process.StandardOutput.BaseStream, new UTF8Encoding(false), false);

        _ = Task.Run(ReadLoopAsync, _cts.Token);
        await Task.CompletedTask;
    }

    public Task SendLineAsync(string line) {
        if (!IsRunning) throw new InvalidOperationException("Process is not running.");
        if (_stdin == null) throw new InvalidOperationException("stdin not initialized");
        return _stdin.WriteLineAsync(line);
    }

    async Task ReadLoopAsync() {
        var reader = _stdout;
        if (reader == null) return;
        while (!_cts.IsCancellationRequested) {
            string? line;
            try { line = await reader.ReadLineAsync().ConfigureAwait(false); }
            catch { break; }
            if (line is null) break; // EOF
            if (line.Length == 0) continue;
            var handler = LineReceived;
            if (handler != null) {
                try { await handler.Invoke(line); } catch { }
            }
        }
    }

    public async ValueTask DisposeAsync() {
        try { _cts.Cancel(); } catch { }
        var p = _process;
        if (p != null) {
            try {
                if (!p.HasExited) {
                    if (_mode == ExecutionMode.Docker) {
                        // Just kill docker CLI process; container ends when stdin closes.
                        SafeKill(p, tree: false);
                    } else {
                        // Attempt tree kill only when likely to succeed (avoids noisy AccessDenied first-chance exceptions under normal user accounts)
                        bool attemptedTree = false;
                        if (CanAttemptTreeKill()) {
                            attemptedTree = true;
                            try {
                                p.Kill(entireProcessTree: true);
                            } catch (System.ComponentModel.Win32Exception win32Ex) when (win32Ex.NativeErrorCode == 5 || win32Ex.NativeErrorCode == 87) { // Access denied / invalid parameter
                                _logger?.LogDebug(win32Ex, "Tree kill failed (error {Code}) for process pid={Pid}; falling back to single kill.", win32Ex.NativeErrorCode, SafeProcessId(p));
                                SafeKill(p, tree: false);
                            } catch (InvalidOperationException ioe) { // process may have exited between checks
                                _logger?.LogDebug(ioe, "Tree kill invalid operation for pid={Pid} (already exited?).", SafeProcessId(p));
                            }
                        }
                        if (!attemptedTree) {
                            SafeKill(p, tree: false);
                        }
                    }
                    try { p.WaitForExit(2000); } catch { }
                }
            } catch (Exception ex) {
                _logger?.LogDebug(ex, "Failed to terminate runner process (pid={Pid}).", SafeProcessId(p));
            }
            try { p.Dispose(); } catch { }
        }
        try { _stdin?.Dispose(); } catch { }
        try { _stdout?.Dispose(); } catch { }
        _stdin = null; _stdout = null; _process = null;
        await Task.CompletedTask;
    }

    static void SafeKill(Process p, bool tree) {
        try {
            if (tree) p.Kill(entireProcessTree: true); else p.Kill();
        } catch { /* swallow intentionally */ }
    }

    static bool CanAttemptTreeKill() {
        // We only try a tree kill on Windows when the process is privileged OR running as the same user (most common case).
        // Environment.IsPrivilegedProcess is available on modern runtimes; if unavailable, default to true to preserve previous behavior.
#if NET8_0_OR_GREATER
        if (!OperatingSystem.IsWindows()) return true; // other OSes fine
        try {
            return Environment.IsPrivilegedProcess; // avoid AccessDenied storms when not elevated
        } catch { return true; }
#else
        return true;
#endif
    }

    static string BuildDockerArgs(string? image) => $"run --rm -i {(string.IsNullOrWhiteSpace(image) ? DockerInfo.DockerImageString : image)}";
    static int SafeProcessId(Process p) { try { return p.Id; } catch { return -1; } }
}

public static class DockerInfo {
    public static string DockerImageString {
        get {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string versionStr = $"{version!.Major}.{version.Minor}.{version.Build}";
            return $"ghcr.io/alexgoon/ason:{versionStr}";
        }
    }
}
