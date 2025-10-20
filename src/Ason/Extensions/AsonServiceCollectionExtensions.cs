namespace Microsoft.Extensions.DependencyInjection;

using Ason;
using AsonRunner;
using Ason.CodeGen; // added for OperatorsLibrary
using Microsoft.SemanticKernel.ChatCompletion;
using System.Reflection;

/// <summary>
/// Optional configuration for <see cref="AsonServiceCollectionExtensions.AddAson"/>.
/// Required dependencies stay as explicit parameters of AddAson.
/// This now mirrors <see cref="AsonClientOptions"/> so callers can configure everything at registration time
/// without needing an extra ConfigureClientOptions delegate.
/// </summary>
public sealed class AsonRegistrationOptions
{
    // --- Mirrored AsonClientOptions properties (mutable for builder-style use) ---
    public int MaxFixAttempts { get; set; } = 2;
    public string? ScriptInstructions { get; set; }
    public string? ReceptionInstructions { get; set; }
    public string? ExplainerInstructions { get; set; }

    // You can either supply factories below OR let these stay null to fall back to defaultChatCompletion
    public Func<IServiceProvider, IChatCompletionService>? ScriptChatCompletionFactory { get; set; }
    public Func<IServiceProvider, IChatCompletionService>? ReceptionChatCompletionFactory { get; set; }
    public Func<IServiceProvider, IChatCompletionService>? ExplainerChatCompletionFactory { get; set; }

    public bool SkipReceptionAgent { get; set; } = false;
    public bool SkipExplainerAgent { get; set; } = false;

    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.InProcess;
    public bool AllowTextExtractor { get; set; } = true;

    public string[]? ForbiddenScriptKeywords { get; set; }

    // Remote runner options
    public bool UseRemoteRunner { get; set; } = false;
    public string? RemoteRunnerBaseUrl { get; set; }
    public string? RemoteRunnerDockerImage { get; set; } = DockerInfo.DockerImageString;
    public bool StopLocalRunnerWhenEnablingRemote { get; set; } = true;

    // Additional filtering of operator methods per client instance
    public Func<MethodInfo, bool>? AdditionalMethodFilter { get; set; }

    // New: optional handler to subscribe to each scoped AsonClient.Log event upon construction
    public EventHandler<AsonLogEventArgs>? LogHandler { get; set; }
}

public static class AsonServiceCollectionExtensions
{
    /// <summary>
    /// Registers a scoped <see cref="AsonClient"/> using supplied required dependencies.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="defaultChatCompletionFactory">Factory producing the default <see cref="IChatCompletionService"/> (transient per scope).</param>
    /// <param name="rootOperatorFactory">Factory producing the per-scope <see cref="RootOperator"/>.</param>
    /// <param name="operators">Pre-built <see cref="OperatorsLibrary"/> snapshot (not registered; just consumed).</param>
    /// <param name="configureOptions">Optional configuration callback for advanced options.</param>
    public static IServiceCollection AddAson(
        this IServiceCollection services,
        Func<IServiceProvider, IChatCompletionService> defaultChatCompletionFactory,
        Func<IServiceProvider, RootOperator> rootOperatorFactory,
        OperatorsLibrary operators,
        Action<AsonRegistrationOptions>? configureOptions = null)
    {
        if (defaultChatCompletionFactory is null) throw new ArgumentNullException(nameof(defaultChatCompletionFactory));
        if (rootOperatorFactory is null) throw new ArgumentNullException(nameof(rootOperatorFactory));
        if (operators is null) throw new ArgumentNullException(nameof(operators));

        // Register scoped AsonClient
        services.AddScoped(sp =>
        {
            var reg = new AsonRegistrationOptions();
            configureOptions?.Invoke(reg);

            var defaultChat = defaultChatCompletionFactory(sp);
            var root = rootOperatorFactory(sp);

            // Build concrete AsonClientOptions from registration options
            var clientOptions = new AsonClientOptions
            {
                MaxFixAttempts = reg.MaxFixAttempts,
                ScriptInstructions = reg.ScriptInstructions,
                ReceptionInstructions = reg.ReceptionInstructions,
                ExplainerInstructions = reg.ExplainerInstructions,
                ScriptChatCompletion = reg.ScriptChatCompletionFactory?.Invoke(sp),
                ReceptionChatCompletion = reg.ReceptionChatCompletionFactory?.Invoke(sp),
                ExplainerChatCompletion = reg.ExplainerChatCompletionFactory?.Invoke(sp),
                SkipReceptionAgent = reg.SkipReceptionAgent,
                SkipExplainerAgent = reg.SkipExplainerAgent,
                ExecutionMode = reg.ExecutionMode,
                AllowTextExtractor = reg.AllowTextExtractor,
                ForbiddenScriptKeywords = reg.ForbiddenScriptKeywords ?? new AsonClientOptions().ForbiddenScriptKeywords,
                UseRemoteRunner = reg.UseRemoteRunner,
                RemoteRunnerBaseUrl = reg.RemoteRunnerBaseUrl,
                RemoteRunnerDockerImage = reg.RemoteRunnerDockerImage,
                StopLocalRunnerWhenEnablingRemote = reg.StopLocalRunnerWhenEnablingRemote,
                AdditionalMethodFilter = reg.AdditionalMethodFilter
            };

            var client = new AsonClient(defaultChat, root, operators, clientOptions);
            if (reg.LogHandler != null) client.Log += reg.LogHandler; // subscribe
            return client;
        });

        return services;
    }
}
