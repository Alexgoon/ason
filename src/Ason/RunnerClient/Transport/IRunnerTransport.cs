using Microsoft.Extensions.Logging;
using AsonRunner;

namespace Ason.Transport;

internal interface IRunnerTransport {
    bool IsStarted { get; }
    event Action<string> LineReceived; // raw JSON line from runner
    event Action<string> Closed; // reason
    Task StartAsync();
    Task SendAsync(string jsonLine);
    Task StopAsync();
}
