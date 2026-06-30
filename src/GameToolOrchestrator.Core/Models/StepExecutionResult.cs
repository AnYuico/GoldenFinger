namespace GameToolOrchestrator.Core.Models;

public sealed class StepExecutionResult
{
    public string StepId { get; set; } = string.Empty;

    public string StepName { get; set; } = string.Empty;

    public string ToolId { get; set; } = string.Empty;

    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public int? ProcessId { get; set; }

    public int? ExitCode { get; set; }

    public bool WasKilled { get; set; }

    public string? ErrorMessage { get; set; }

    public List<ActionExecutionResult> Actions { get; set; } = [];

    public List<StatusTransition> StatusTransitions { get; set; } = [];
}
