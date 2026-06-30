using System.Diagnostics;
using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Infrastructure.Process;

public sealed class DefaultProcessHandle : IProcessHandle
{
    private readonly System.Diagnostics.Process _process;

    public DefaultProcessHandle(System.Diagnostics.Process process)
    {
        _process = process;
    }

    public int ProcessId => _process.Id;

    public string ProcessName => _process.ProcessName;

    public bool HasExited => _process.HasExited;

    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    public async Task<ProcessExitResult> WaitForExitAsync(
        TimeSpan? timeout,
        bool killOnTimeout,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_process.HasExited)
            {
                return ProcessExitResult.Succeeded(_process.ExitCode);
            }

            if (!timeout.HasValue)
            {
                await _process.WaitForExitAsync(cancellationToken);
                return ProcessExitResult.Succeeded(_process.ExitCode);
            }

            var waitTask = _process.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(timeout.Value, cancellationToken);
            var completed = await Task.WhenAny(waitTask, timeoutTask);

            if (completed == waitTask)
            {
                await waitTask;
                return ProcessExitResult.Succeeded(_process.ExitCode);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return ProcessExitResult.Cancelled();
            }

            if (killOnTimeout)
            {
                await KillAsync(CancellationToken.None);
                return ProcessExitResult.TimedOut(wasKilled: true, "Process timed out and was killed.");
            }

            return ProcessExitResult.TimedOut(wasKilled: false, "Process timed out. killOnTimeout is false, so the process was left running.");
        }
        catch (OperationCanceledException)
        {
            return ProcessExitResult.Cancelled();
        }
        catch (InvalidOperationException exception)
        {
            return ProcessExitResult.TimedOut(wasKilled: false, exception.Message);
        }
    }

    public async Task KillAsync(CancellationToken cancellationToken)
    {
        if (_process.HasExited)
        {
            return;
        }

        _process.Kill(entireProcessTree: true);
        await _process.WaitForExitAsync(cancellationToken);
    }
}
