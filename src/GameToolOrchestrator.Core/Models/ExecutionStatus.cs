namespace GameToolOrchestrator.Core.Models;

public enum ExecutionStatus
{
    Pending,
    Launching,
    WaitingForWindow,
    RunningActions,
    WaitingForExit,
    Succeeded,
    Failed,
    Cancelled,
    TimedOut,
    Skipped
}
