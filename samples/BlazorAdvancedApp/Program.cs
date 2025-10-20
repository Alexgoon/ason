using Ason;
using Ason.CodeGen; 
using AsonRunner;
using BlazorAdvancedApp.AI;
using BlazorAdvancedApp.Components;
using BlazorAdvancedApp.Services; // added
using BlazorAdvancedApp.State;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MudBlazor.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IAppDataService, InMemoryAppDataService>();

builder.Services.AddScoped<SessionState>();

builder.Services.AddAson(
    defaultChatCompletionFactory: sp => new OpenAIChatCompletionService("gpt-4.1-mini", Environment.GetEnvironmentVariable("MY_OPEN_AI_KEY") ?? string.Empty),
    rootOperatorFactory: sp => sp.GetRequiredService<SessionState>().MainAppOperator,
    operators: new OperatorBuilder()
                    .AddAssemblies(typeof(BlazorMainAppOperator).Assembly)
                    .AddExtractor()
                    .Build(),
    configureOptions: opt => {
        opt.ExecutionMode = ExecutionMode.Docker;
        opt.MaxFixAttempts = 2;
        opt.SkipReceptionAgent = false;
    });

builder.Services.AddMudServices();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
