using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Abstractions;

public interface IProcessHandle
{
    int ProcessId { get; }

    string ProcessName { get; }

    bool HasExited { get; }

    int? ExitCode { get; }

    Task<ProcessExitResult> WaitForExitAsync(
        TimeSpan? timeout,
        bool killOnTimeout,
        CancellationToken cancellationToken);

    Task KillAsync(CancellationToken cancellationToken);
}
