using Ason;
using Ason.CodeGen;
using Ason.Console.Template;
using Ason.Console.Template.Modules;
using AsonRunner;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;

var apiKey = Environment.GetEnvironmentVariable("MY_OPEN_AI_KEY") ?? string.Empty;
IChatCompletionService chatService = new OpenAIChatCompletionService(modelId: "gpt-4.1-mini", apiKey: apiKey);


OperatorsLibrary operatorLibrary = new OperatorBuilder()
                                        .AddAssemblies(typeof(MainOperator).Assembly)
                                        .Build();

RootOperator rootOperator = new RootModule().RootOperator;
AsonClientOptions options = new() {
    ExecutionMode = ExecutionMode.InProcess
};
var asonChatClient = new AsonClient(chatService, rootOperator, operatorLibrary, options);

asonChatClient.Log += (o, e) => Debug.WriteLine($"{e.Source}: {e.Message}");


Console.WriteLine("Type a message — you can create, update, delete, or ask about customers and orders:");
while (true) {
    Console.Write("You: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
        break;

    Console.Write("Assistant: ");

    await foreach (var token in asonChatClient.SendStreamingAsync(input)) {
        Console.Write(token);
    }
    Console.WriteLine();
}

Console.WriteLine("Goodbye.");
