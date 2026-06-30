using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Actions;

public sealed class WaitProcessExitActionExecutor : IActionExecutor
{
    public const string TypeName = "waitProcessExit";

    public string ActionType => TypeName;

    public async Task<ActionExecutionResult> ExecuteAsync(
        AutomationActionDefinition action,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var result = ActionExecutionResult.Started(action);
        var timeout = ActionParameterReader.GetTimeout(action.Parameters, action.TimeoutSeconds, action.TimeoutMinutes)
            ?? context.ExecutionOptions.GetProcessExitTimeout();
        var killOnTimeout = ActionParameterReader.GetBool(
            action.Parameters,
            "killOnTimeout",
            context.ExecutionOptions.KillOnTimeout);

        try
        {
            context.Logger.Info($"Action '{action.Id}' waiting for process {context.Process.ProcessId} to exit.");
            var exitResult = await context.Process.WaitForExitAsync(timeout, killOnTimeout, cancellationToken);
            result.Status = exitResult.Status;
            result.ErrorMessage = exitResult.ErrorMessage;

            if (exitResult.ExitCode.HasValue)
            {
                result.Outputs["exitCode"] = exitResult.ExitCode.Value.ToString();
            }

            result.Outputs["wasKilled"] = exitResult.WasKilled.ToString();
        }
        catch (OperationCanceledException)
        {
            result.Status = ExecutionStatus.Cancelled;
            result.ErrorMessage = "waitProcessExit was cancelled.";
        }

        result.CompletedAt = DateTimeOffset.UtcNow;
        return result;
    }
}
