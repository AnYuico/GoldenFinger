namespace GameToolOrchestrator.Core.Models;

public sealed class ExecutionResult
{
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");

    public string TaskPlanId { get; set; } = string.Empty;

    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public List<TaskExecutionResult> Tasks { get; set; } = [];
}
