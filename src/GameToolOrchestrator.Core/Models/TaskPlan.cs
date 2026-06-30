namespace GameToolOrchestrator.Core.Models;

public sealed class TaskPlan
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ExecutionOptions? Execution { get; set; }

    public List<TaskStep> Steps { get; set; } = [];
}
