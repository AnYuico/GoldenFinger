using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Abstractions;

public interface IActionExecutor
{
    string ActionType { get; }

    Task<ActionExecutionResult> ExecuteAsync(
        AutomationActionDefinition action,
        ActionExecutionContext context,
        CancellationToken cancellationToken);
}
