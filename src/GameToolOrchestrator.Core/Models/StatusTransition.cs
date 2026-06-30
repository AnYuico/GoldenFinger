namespace GameToolOrchestrator.Core.Models;

public sealed class StatusTransition
{
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;

    public ExecutionStatus Status { get; set; }

    public string Message { get; set; } = string.Empty;
}
