using Ason.CodeGen;
using Ason.BlazorServer.Template.Operators;
using Ason.BlazorServer.Template.Components;
using Ason.BlazorServer.Template.State;
using AsonRunner;
using Microsoft.SemanticKernel.Connectors.OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<SessionState>();
builder.Services.AddAson(
    defaultChatCompletionFactory: sp => new OpenAIChatCompletionService("gpt-4.1-mini", Environment.GetEnvironmentVariable("MY_OPEN_AI_KEY") ?? string.Empty),
    rootOperatorFactory: sp => sp.GetRequiredService<SessionState>().MainAppOperator,
    operators: new OperatorBuilder().AddAssemblies(typeof(MainAppOperator).Assembly).Build(),
    configureOptions: opt => {
        opt.RunnerMode = ExecutionMode.ExternalProcess;
    });

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
