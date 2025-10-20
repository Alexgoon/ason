using Ason;
using Ason.CodeGen;
using Ason.Maui.Template.Operators;
using AsonRunner;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Ason.Maui.Template.ViewModels;

public partial class ChatViewModel : ObservableObject {
    [ObservableProperty]
    string userInput = @"Update the contact details for the following customers:
- Alice Johnson:  
  Phone: +1 (212) 555-0147  
  Address: 123 Main St, New York, NY  
- Bob Smith:  
  Phone: +1 (310) 555-0923  
  Address: 456 Oak Ave, Los Angeles, CA  
- Carol Davis:  
  Phone: +1 (617) 555-3789  
  Address: 789 Pine Rd, Boston, MA""";
    AsonClient asonChatClient;
    RootOperator mainAppOperator;

    [ObservableProperty]
    string chatResponse = string.Empty;
    public ChatViewModel(RootOperator rootOperator)
    {
        mainAppOperator = rootOperator;
        InitAsonClient();
    }

    [MemberNotNull(nameof(asonChatClient))]
    void InitAsonClient() {
        var apiKey = Environment.GetEnvironmentVariable("MY_OPEN_AI_KEY") ?? string.Empty;
        IChatCompletionService chatService = new OpenAIChatCompletionService(modelId: "gpt-4.1-mini", apiKey: apiKey);

        OperatorsLibrary operatorLibrary = new OperatorBuilder()
                                                .AddAssemblies(typeof(MainAppOperator).Assembly)
                                                .Build();

        AsonClientOptions options = new() {
            ExecutionMode = ExecutionMode.ExternalProcess,
            RemoteRunnerBaseUrl = DeviceInfo.Platform == DevicePlatform.Android? "http://10.0.2.2:5222" : "http://localhost:5222",
            UseRemoteRunner = true,
        };
        asonChatClient = new AsonClient(chatService, mainAppOperator, operatorLibrary, options);

        asonChatClient.Log += (o, e) => Debug.WriteLine($"{e.Source}: {e.Message}");
    }

    [RelayCommand]
    async Task SendMessage() {
        var userText = UserInput?.Trim();
        if (string.IsNullOrWhiteSpace(userText)) return;
        UserInput = string.Empty;
        ChatResponse = string.Empty;

        ChatResponse = await asonChatClient.SendAsync(userText); 
        //await foreach (var token in asonChatClient.SendStreamingAsync(userText)) {
        //    ChatResponse += token;
        //}
    }
}
