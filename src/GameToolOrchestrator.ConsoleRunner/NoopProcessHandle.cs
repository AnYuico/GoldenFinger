using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.ConsoleRunner;

public sealed class NoopProcessHandle : IProcessHandle
{
    public int ProcessId => 0;

    public string ProcessName => "external";

    public bool HasExited => true;

    public int? ExitCode => 0;

    public Task<ProcessExitResult> WaitForExitAsync(
        TimeSpan? timeout,
        bool killOnTimeout,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(ProcessExitResult.Succeeded(0));
    }

    public Task KillAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
