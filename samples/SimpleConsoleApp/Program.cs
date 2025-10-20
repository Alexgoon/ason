using System.Reflection;
using Ason;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;


var apiKey = Environment.GetEnvironmentVariable("MY_OPEN_AI_KEY");
IChatCompletionService chatService = new OpenAIChatCompletionService(modelId: "gpt-4o-mini", apiKey: apiKey!);

//var chat = new OrchestratorChatClient(
//    chatService,
//    ExecutionMode.ExternalProcess, // or ExecutionMode.InProcess / ExecutionMode.Docker
//    new OrchestratorOptions {
//        MaxFixAttempts = 2,
//        SkipAnswerAgent = false
//        // Optional: customize prompts or per-agent services
//        // ScriptChatCompletion = chatService,
//        // AnswerChatCompletion = chatService,
//        // ExplainerChatCompletion = chatService,
//    }
//);

//// Subscribe to orchestrator logging
//chat.Log += (sender, e) =>
//{
//    var prev = Console.ForegroundColor;
//    Console.ForegroundColor = e.Level switch
//    {
//        LogLevel.Trace => ConsoleColor.DarkGray,
//        LogLevel.Debug => ConsoleColor.DarkCyan,
//        LogLevel.Information => ConsoleColor.DarkGreen,
//        LogLevel.Warning => ConsoleColor.Yellow,
//        LogLevel.Error => ConsoleColor.Red,
//        LogLevel.Critical => ConsoleColor.Magenta,
//        _ => ConsoleColor.Gray
//    };
//    var prefix = $"[{e.Level}] ";
//    Console.WriteLine(prefix + e.Message + (e.Exception is not null ? "\n" + e.Exception : string.Empty));
//    Console.ForegroundColor = prev;
//};

//chat.GetOperatorsFromAssembly(Assembly.GetExecutingAssembly());

//string userTask = args.Length > 0 ? string.Join(" ", args) :
//    "Extract data from the latest (see the date) email's plain text and create a new customer based on this information";

////string userTask = args.Length > 0 ? string.Join(" ", args) :
////    "Get a customer and Test it as many times as specified in its Id property. Then show column chooser and return a customer name";

////string userTask = args.Length > 0 ? string.Join(" ", args) :
////    "Describe how to cook the most tasty cake";

//Console.WriteLine("\n=== Assistant Reply (streaming) ===\n");
//await foreach (var chunk in chat.SendStreamingAsync(userTask))
//{
//    Console.Write(chunk);
//}
//Console.WriteLine();