namespace GameToolOrchestrator.Core.Models;

public sealed class OrchestratorConfig
{
    public string Version { get; set; } = "1.0";

    public ExecutionOptions Execution { get; set; } = new();

    public List<ToolDefinition> Tools { get; set; } = [];

    public List<TaskPlan> TaskPlans { get; set; } = [];

    public ToolDefinition? FindTool(string toolId)
    {
        return Tools.FirstOrDefault(tool => string.Equals(tool.Id, toolId, StringComparison.OrdinalIgnoreCase));
    }

    public TaskPlan? FindTaskPlan(string taskPlanId)
    {
        return TaskPlans.FirstOrDefault(plan => string.Equals(plan.Id, taskPlanId, StringComparison.OrdinalIgnoreCase));
    }
}
