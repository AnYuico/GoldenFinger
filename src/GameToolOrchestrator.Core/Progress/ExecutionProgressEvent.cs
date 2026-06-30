using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Progress;

public sealed class ExecutionProgressEvent
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public ExecutionProgressEventType EventType { get; set; }

    public string Level { get; set; } = "Information";

    public string TaskPlanId { get; set; } = string.Empty;

    public string StepId { get; set; } = string.Empty;

    public string ToolId { get; set; } = string.Empty;

    public string ActionId { get; set; } = string.Empty;

    public string ActionType { get; set; } = string.Empty;

    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    public string Message { get; set; } = string.Empty;
}
