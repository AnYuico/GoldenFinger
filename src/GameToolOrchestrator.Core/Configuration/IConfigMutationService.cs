using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Configuration;

public interface IConfigMutationService
{
    ToolDefinition AddTool(OrchestratorConfig config);

    ToolDefinition CopyTool(OrchestratorConfig config, ToolDefinition source);

    MutationResult DeleteTool(OrchestratorConfig config, ToolDefinition tool);

    IReadOnlyList<string> FindToolReferences(OrchestratorConfig config, string toolId);

    TaskPlan AddTaskPlan(OrchestratorConfig config);

    TaskPlan CopyTaskPlan(OrchestratorConfig config, TaskPlan source);

    MutationResult DeleteTaskPlan(OrchestratorConfig config, TaskPlan plan);

    TaskStep? AddStep(OrchestratorConfig config, TaskPlan plan, ToolDefinition? tool);

    TaskStep CopyStep(TaskPlan plan, TaskStep source);

    MutationResult DeleteStep(TaskPlan plan, TaskStep step);

    void NormalizeStepOrder(TaskPlan plan);

    AutomationActionDefinition? AddAction(TaskStep step, string actionType);

    AutomationActionDefinition CopyAction(TaskStep step, AutomationActionDefinition source);

    MutationResult DeleteAction(TaskStep step, AutomationActionDefinition action);
}
