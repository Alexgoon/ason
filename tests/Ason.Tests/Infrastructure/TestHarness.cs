using System;
using System.Reflection;
using System.Threading.Tasks;
using Ason;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Ason.Client.Execution;
using AsonRunner;
using Ason.CodeGen;

namespace Ason.Tests.Infrastructure;

internal static class TestHarness {
    static OperatorsLibrary Snapshot = new OperatorBuilder()
        .AddAssemblies(typeof(RootOperator).Assembly)
        .SetBaseFilter(mi => mi.GetCustomAttribute<AsonMethodAttribute>() != null)
        .Build();

    internal static AsonClient CreateBasicClient(IChatCompletionService chat, AsonClientOptions? opts = null) {
        var root = new RootOperator(new object());
        var options = opts ?? new AsonClientOptions();
        options = new AsonClientOptions {
            SkipReceptionAgent = options.SkipReceptionAgent,
            SkipExplainerAgent = options.SkipExplainerAgent,
            MaxFixAttempts = options.MaxFixAttempts,
            ScriptChatCompletion = chat,
            ReceptionChatCompletion = chat,
            ExplainerChatCompletion = chat,
            ExecutionMode = ExecutionMode.InProcess
        };
        return new AsonClient(chat, root, Snapshot, options);
    }
}
