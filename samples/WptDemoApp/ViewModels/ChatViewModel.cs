using Ason;
using Ason.CodeGen;
using AsonRunner;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client; // MCP interfaces from Core
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Windows.Interop;
using System.Collections.ObjectModel; // added
using Microsoft.Extensions.AI;
using WpfSampleApp.AI; // for ChatMessage

namespace WpfSampleApp.ViewModels;

public partial class ChatViewModel : ObservableObject {
    [ObservableProperty]
    string userInput = "Update fist three employee names to John Doe";

    [ObservableProperty]
    string? chatResponse;

    public ObservableCollection<string> PromptSuggestions { get; } = new() {
        "Change position of all employees hired in 2025 to X",
        "Number of employees hired in 2025",
        "Create a chart with sales for the top 3 products in 2025",
        "Add an appointment based on data from the last email",
    };

    // Maintain conversation history for new overload
    readonly List<ChatMessage> _messages = new();

    AsonClient Chat;
    static OperatorsLibrary? _sharedSnapshot;

    static OperatorsLibrary GetOperators() => _sharedSnapshot ??= new OperatorBuilder()
        .AddAssemblies(typeof(MainAppOperator).Assembly)
        //.AddExtractor()
        //.AddMcp(CreateContext7Client())
        .SetBaseFilter(mi => mi.GetCustomAttribute<AsonMethodAttribute>() != null)
        .Build();

    //IMcpClient CreateContext7Client() {
    //    var sharedHandler = new SocketsHttpHandler {
    //        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    //        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1)
    //    };
    //    var httpClient = new HttpClient(sharedHandler);
    //    httpClient.DefaultRequestHeaders.Add("CONTEXT7_API_KEY", Environment.GetEnvironmentVariable("CONTEXT7_API_KEY"));
    //    var transport = new SseClientTransport(new() { Endpoint = new Uri("https://mcp.context7.com/mcp"), Name = "context7" }, httpClient);
    //    return McpClientFactory.CreateAsync(transport).GetAwaiter().GetResult();
    //}

    public ChatViewModel(MainViewModel mainViewModel) {
        var apiKey = Environment.GetEnvironmentVariable("MY_OPEN_AI_KEY") ?? string.Empty;
        IChatCompletionService chatService = new OpenAIChatCompletionService(modelId: "gpt-4.1-mini", apiKey: apiKey);
        var operators = GetOperators();
        var options = new AsonClientOptions {
            MaxFixAttempts = 2,
            SkipReceptionAgent = false,
            ExecutionMode = ExecutionMode.InProcess,
            //RunnerExecutablePath = @"..\..\..\..\..\src\bin\Debug\net9.0"
            //UseRemoteRunner = true,
            //RemoteRunnerBaseUrl = "http://localhost:5236"
        };
        Chat = new AsonClient(chatService, mainViewModel.MainAppOperator, operators, options);

        Chat.Log += (sender, e) => {
            var prefix = $"[{e.Level}] ";
            Debug.WriteLine(prefix + e.Message + (e.Exception is not null ? "\n" + e.Exception : string.Empty));
        };
    }




    [RelayCommand]
    async Task SendMessage() {
        var userText = UserInput?.Trim();
        if (string.IsNullOrWhiteSpace(userText)) return;

        // Add user message to history
        _messages.Add(new ChatMessage(ChatRole.User, userText));
        ChatResponse = string.Empty;
        UserInput = string.Empty;

        await foreach (var chunk in Chat.SendStreamingAsync(_messages)) {
            ChatResponse += chunk;
        }

        if (!string.IsNullOrEmpty(ChatResponse)) {
            _messages.Add(new ChatMessage(ChatRole.Assistant, ChatResponse));
        }
    }

    [RelayCommand]
    void UsePrompt(string prompt) => UserInput = prompt;

    [RelayCommand]
    void ShowSuggestedPrompts(string prompt) => ChatResponse = null;
}
