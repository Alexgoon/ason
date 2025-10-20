using Ason;
using Ason.CodeGen;
using AsonRunner;
using Ason.Wpf.Template.Operators;
using Ason.Wpf.Template.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Ason.Wpf.Template.ViewModels;
public partial class MainViewModel : ObservableObject {

    [ObservableProperty]
    object? currentNavigationItem;

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


    [ObservableProperty]
    string chatResponse = string.Empty;

    [ObservableProperty]
    ObservableCollection<NavigatoinItem> navigatoinItems = new();

    RootOperator mainAppOperator;

    AsonClient asonChatClient;

    public MainViewModel() {
        mainAppOperator = new MainAppOperator(this);
        navigatoinItems = new ObservableCollection<NavigatoinItem>() {
                            new NavigatoinItem("Orders", () => new OrdersViewModel(mainAppOperator)),
                            new NavigatoinItem("Customers",  () => new CustomersViewModel(mainAppOperator)),
        };
        CurrentNavigationItem = navigatoinItems.FirstOrDefault();
        InitAsonClient();
    }

    [MemberNotNull(nameof(asonChatClient))]
    void InitAsonClient() {
        var apiKey = Environment.GetEnvironmentVariable("MY_OPEN_AI_KEY") ?? string.Empty;
        IChatCompletionService chatService = new OpenAIChatCompletionService(modelId: "gpt-4.1-mini", apiKey: apiKey);

        OperatorsLibrary operatorLibrary = new OperatorBuilder()
                                                .AddAssemblies(typeof(MainAppOperator).Assembly)
                                                .Build();

        mainAppOperator = new MainAppOperator(this);
        AsonClientOptions options = new() {
            ExecutionMode = ExecutionMode.InProcess,
            AnswerInstructions = """
        You are an AI assistant.

        You can see the full prior conversation (thread memory). The user may refine the request over multiple messages.

        For each user request, respond with EXACTLY ONE of the following:
          1. A direct, helpful natural-language answer (when you can answer without executing internal functions). Output ONLY that answer text.
          2. A structured directive indicating a script is required. When a script is required you MUST output ONLY the following exact structure:

             script
             <task>
             <single concise description of the consolidated user task capturing ALL relevant details from the conversation>
             </task>

        Rules:
        - Never include code, pseudo-code, or backticks.
        - Never explain your decision.
        - If you choose script you MUST include a <task> block with the consolidated task description (no other markup, no extra commentary).
        - The <task> block must appear immediately after the line containing only 'script'.
        - Do not output anything after </task>.
        - Alwyas reply with 'script' and a <task> when a user requests for some active action (even when it was executed previously).
        - The task description should be actionable, unambiguous, and contain key constraints (dates, counts, filters, entities, etc.) mentioned earlier.
        - If you are provided with data in a task, include it as is in the task description. Do not invent any data — simply describe the user’s task based on the conversation, staying as close to the original as possible.
        - The task executor does not have access to the original user message or conversation, so you must include all necessary details from the user’s conversation in the task description.
        """
        };
        asonChatClient = new AsonClient(chatService, mainAppOperator, operatorLibrary, options);

        asonChatClient.Log += (o, e) => Debug.WriteLine($"{e.Source}: {e.Message}");
    }

    public void Navigate<TViewModelType>() {
        CurrentNavigationItem = NavigatoinItems.FirstOrDefault(i => i.ViewModel.GetType() == typeof(TViewModelType));
    }

    [RelayCommand]
    async Task SendMessage() {
        var userText = UserInput?.Trim();
        if (string.IsNullOrWhiteSpace(userText)) return;
        UserInput = string.Empty;
        ChatResponse = string.Empty;

        await foreach (var token in asonChatClient.SendStreamingAsync(userText)) {
            ChatResponse += token;
        }
    }
}
