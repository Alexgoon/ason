using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Reflection;
using AsonRunner;

namespace Ason;

public sealed class AsonClientOptions {
    public ILogger? Logger { get; init; }
    public int MaxFixAttempts { get; init; } = 2;
    public string? ScriptInstructions { get; init; }
    public string? ReceptionInstructions { get; init; }
    public string? ExplainerInstructions { get; init; }

    public IChatCompletionService? ScriptChatCompletion { get; init; }
    public IChatCompletionService? ReceptionChatCompletion { get; init; }
    public IChatCompletionService? ExplainerChatCompletion { get; init; }

    public bool SkipReceptionAgent { get; init; } = false;
    public bool SkipExplainerAgent { get; init; } = false;

    // Runner execution mode moved from AsonClient constructor
    public ExecutionMode ExecutionMode { get; init; } = ExecutionMode.InProcess;

    // When true, exposes the OrchestratorChatClient operator methods to scripts via generated proxies
    public bool AllowTextExtractor { get; init; } = true;

    public string[] ForbiddenScriptKeywords { get; init; } = new[]
    {
            "System.Diagnostics", "Process.Start", "System.IO", "DllImport", "System.Runtime.InteropServices",
            "System.Reflection", "Assembly.Load", "Activator.CreateInstance", "Directory.Delete", "Environment.GetEnvironmentVariable", "System.Net.Http"
        };

    // --- Remote runner configuration (new) ---
    public bool UseRemoteRunner { get; init; } = false;
    public string? RemoteRunnerBaseUrl { get; init; }
    public string? RemoteRunnerDockerImage { get; init; } = DockerInfo.DockerImageString;
    public bool StopLocalRunnerWhenEnablingRemote { get; init; } = true;

    // Optional per-client extra method filter (applied on top of snapshot cache)
    public Func<MethodInfo, bool>? AdditionalMethodFilter { get; init; }

    // Optional explicit path to Ason.ExternalExecutor (dll or exe). If null, default discovery logic is used.
    public string? RunnerExecutablePath { get; init; }
}