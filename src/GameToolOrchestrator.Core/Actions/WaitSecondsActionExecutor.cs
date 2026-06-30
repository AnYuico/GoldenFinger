using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Actions;

public sealed class WaitSecondsActionExecutor : IActionExecutor
{
    public const string TypeName = "waitSeconds";

    public string ActionType => TypeName;

    public async Task<ActionExecutionResult> ExecuteAsync(
        AutomationActionDefinition action,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var result = ActionExecutionResult.Started(action);
        var duration = ActionParameterReader.GetRequiredDuration(action.Parameters);

        if (duration < TimeSpan.Zero)
        {
            result.Status = ExecutionStatus.Failed;
            result.ErrorMessage = "waitSeconds duration cannot be negative.";
            result.CompletedAt = DateTimeOffset.UtcNow;
            return result;
        }

        try
        {
            context.Logger.Info($"Action '{action.Id}' waiting for {duration.TotalSeconds:0.###} seconds.");
            await Task.Delay(duration, cancellationToken);
            result.Status = ExecutionStatus.Succeeded;
        }
        catch (OperationCanceledException)
        {
            result.Status = ExecutionStatus.Cancelled;
            result.ErrorMessage = "waitSeconds was cancelled.";
        }

        result.CompletedAt = DateTimeOffset.UtcNow;
        return result;
    }
}
