namespace GameToolOrchestrator.Core.Models;

public sealed class TaskExecutionResult
{
    public string TaskPlanId { get; set; } = string.Empty;

    public string TaskPlanName { get; set; } = string.Empty;

    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public List<StepExecutionResult> Steps { get; set; } = [];
}
