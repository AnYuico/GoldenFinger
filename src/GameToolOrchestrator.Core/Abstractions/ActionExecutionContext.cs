using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Abstractions;

public sealed class ActionExecutionContext
{
    public required OrchestratorConfig Config { get; init; }

    public required TaskPlan Plan { get; init; }

    public required TaskStep Step { get; init; }

    public required ToolDefinition Tool { get; init; }

    public required ExecutionOptions ExecutionOptions { get; init; }

    public required IProcessHandle Process { get; init; }

    public required IExecutionLogger Logger { get; init; }
}
