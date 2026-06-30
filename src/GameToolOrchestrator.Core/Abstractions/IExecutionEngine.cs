using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Abstractions;

public interface IExecutionEngine
{
    Task<ExecutionResult> ExecuteAsync(
        OrchestratorConfig config,
        string taskPlanId,
        CancellationToken cancellationToken = default);
}
