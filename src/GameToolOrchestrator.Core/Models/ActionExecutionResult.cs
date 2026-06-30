namespace GameToolOrchestrator.Core.Models;

public sealed class ActionExecutionResult
{
    public string ActionId { get; set; } = string.Empty;

    public string ActionType { get; set; } = string.Empty;

    public string ActionName { get; set; } = string.Empty;

    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public Dictionary<string, string> Outputs { get; set; } = [];

    public static ActionExecutionResult Started(AutomationActionDefinition action)
    {
        return new ActionExecutionResult
        {
            ActionId = action.Id,
            ActionType = action.Type,
            ActionName = action.Name,
            Status = ExecutionStatus.RunningActions,
            StartedAt = DateTimeOffset.UtcNow
        };
    }
}
