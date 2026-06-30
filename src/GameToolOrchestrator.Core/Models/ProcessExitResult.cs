namespace GameToolOrchestrator.Core.Models;

public sealed class ProcessExitResult
{
    public ExecutionStatus Status { get; set; }

    public int? ExitCode { get; set; }

    public bool WasKilled { get; set; }

    public string? ErrorMessage { get; set; }

    public static ProcessExitResult Succeeded(int? exitCode)
    {
        return new ProcessExitResult
        {
            Status = ExecutionStatus.Succeeded,
            ExitCode = exitCode
        };
    }

    public static ProcessExitResult TimedOut(bool wasKilled, string? message = null)
    {
        return new ProcessExitResult
        {
            Status = ExecutionStatus.TimedOut,
            WasKilled = wasKilled,
            ErrorMessage = message
        };
    }

    public static ProcessExitResult Cancelled()
    {
        return new ProcessExitResult
        {
            Status = ExecutionStatus.Cancelled,
            ErrorMessage = "The wait was cancelled."
        };
    }
}
