using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace AsonRunner;

public sealed class ScriptRunnerProcessHost : IAsyncDisposable {
    private readonly ExecutionMode _mode;
    private readonly string? _dockerImage;
    private readonly ILogger? _logger;
    private readonly string? _runnerPathOverride; // new

    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private CancellationTokenSource _cts = new();

    public event Func<string, Task>? LineReceived;
    public event Func<string, Task>? ProcessExited;

    public ScriptRunnerProcessHost(ExecutionMode mode, string? dockerImage, ILogger? logger, string? runnerExecutablePath = null) {
        _mode = mode;
        _dockerImage = dockerImage;
        _logger = logger;
        _runnerPathOverride = runnerExecutablePath;
    }

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync() {
        if (_mode == ExecutionMode.InProcess) throw new InvalidOperationException("InProcess mode does not require external host.");
        if (IsRunning) return;

        string runnerBaseName = "Ason.ExternalExecutor";
        string? baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(baseDir)) throw new InvalidOperationException("Cannot resolve base directory for external executor.");

        string? overridePath = _runnerPathOverride;
        string dllPath;
        string exePath;
        if (!string.IsNullOrWhiteSpace(overridePath)) {
            // If a directory was supplied, compose expected names inside it
            if (Directory.Exists(overridePath)) {
                dllPath = Path.Combine(overridePath, runnerBaseName + ".dll");
                exePath = Path.Combine(overridePath, runnerBaseName + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
            } else {
                // A file path was supplied explicitly (.dll or .exe)
                dllPath = overridePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? overridePath : Path.Combine(Path.GetDirectoryName(overridePath)!, runnerBaseName + ".dll");
                exePath = overridePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? overridePath : Path.Combine(Path.GetDirectoryName(overridePath)!, runnerBaseName + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
            }
        } else {
            dllPath = Path.Combine(baseDir, runnerBaseName + ".dll");
            exePath = Path.Combine(baseDir, runnerBaseName + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
        }

        string launchFile;
        bool useDotnet;
        if (File.Exists(exePath)) { launchFile = exePath; useDotnet = false; }
        else if (File.Exists(dllPath)) { launchFile = dllPath; useDotnet = true; }
        else throw new FileNotFoundException($"Could not locate external executor. Searched: '{dllPath}'. Make sure the Ason.ExternalExecutor NuGet package is added.");

        var si = new ProcessStartInfo {
            FileName = _mode == ExecutionMode.Docker ? "docker" : (useDotnet ? "dotnet" : launchFile),
            Arguments = _mode == ExecutionMode.Docker ? BuildDockerArgs(_dockerImage) : (useDotnet ? $"\"{launchFile}\"" : string.Empty),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardInputEncoding = new UTF8Encoding(false),
            CreateNoWindow = true
        };

        _process = new Process { StartInfo = si, EnableRaisingEvents = true };
        _process.Exited += async (_, _) => { try { if (ProcessExited != null) await ProcessExited.Invoke("exited"); } catch { } };
        _process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger?.LogDebug("[ScriptRunner stderr] {Line}", e.Data); };
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

    private async Task ReadLoopAsync() {
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
                        SafeKill(p, tree: false);
                    } else {
                        bool attemptedTree = false;
                        if (CanAttemptTreeKill()) {
                            attemptedTree = true;
                            try { p.Kill(entireProcessTree: true); }
                            catch (System.ComponentModel.Win32Exception win32Ex) when (win32Ex.NativeErrorCode == 5 || win32Ex.NativeErrorCode == 87) {
                                _logger?.LogDebug(win32Ex, "Tree kill failed (error {Code}) for process pid={Pid}; falling back to single kill.", win32Ex.NativeErrorCode, SafeProcessId(p));
                                SafeKill(p, tree: false);
                            } catch (InvalidOperationException ioe) {
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

    private static void SafeKill(Process p, bool tree) {
        try { if (tree) p.Kill(entireProcessTree: true); else p.Kill(); } catch { }
    }

    private static bool CanAttemptTreeKill() {
#if NET8_0_OR_GREATER
        if (!OperatingSystem.IsWindows()) return true;
        try { return Environment.IsPrivilegedProcess; } catch { return true; }
#else
        return true;
#endif
    }

    private static string BuildDockerArgs(string? image) => $"run --rm -i {(string.IsNullOrWhiteSpace(image) ? DockerInfo.DockerImageString : image)}";
    private static int SafeProcessId(Process p) { try { return p.Id; } catch { return -1; } }
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
