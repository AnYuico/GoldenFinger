using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Actions;
using GameToolOrchestrator.Core.Models;
using GameToolOrchestrator.Core.Progress;

namespace GameToolOrchestrator.Core.Engine;

public sealed class ExecutionEngine : IExecutionEngine
{
    private readonly IProcessLauncher _processLauncher;
    private readonly IActionExecutorFactory _actionExecutorFactory;
    private readonly IExecutionLogger _logger;
    private readonly IExecutionProgressReporter _progressReporter;

    public ExecutionEngine(
        IProcessLauncher processLauncher,
        IActionExecutorFactory actionExecutorFactory,
        IExecutionLogger logger,
        IExecutionProgressReporter? progressReporter = null)
    {
        _processLauncher = processLauncher;
        _actionExecutorFactory = actionExecutorFactory;
        _logger = logger;
        _progressReporter = progressReporter ?? NullExecutionProgressReporter.Instance;
    }

    public async Task<ExecutionResult> ExecuteAsync(
        OrchestratorConfig config,
        string taskPlanId,
        CancellationToken cancellationToken = default)
    {
        var executionResult = new ExecutionResult
        {
            TaskPlanId = taskPlanId,
            Status = ExecutionStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow
        };

        var plan = config.FindTaskPlan(taskPlanId);
        if (plan is null)
        {
            executionResult.Status = ExecutionStatus.Failed;
            executionResult.ErrorMessage = $"Task plan '{taskPlanId}' was not found.";
            executionResult.CompletedAt = DateTimeOffset.UtcNow;
            _logger.Error(executionResult.ErrorMessage);
            return executionResult;
        }

        var options = plan.Execution ?? config.Execution;
        var taskResult = new TaskExecutionResult
        {
            TaskPlanId = plan.Id,
            TaskPlanName = plan.Name,
            Status = ExecutionStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow
        };

        executionResult.Tasks.Add(taskResult);
        _logger.Info($"Starting task plan '{plan.Id}'.");
        ReportLog(plan.Id, string.Empty, string.Empty, string.Empty, "Information", $"Starting task plan '{plan.Id}'.");

        foreach (var step in plan.Steps.OrderBy(step => step.Order).ThenBy(step => step.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                taskResult.Status = ExecutionStatus.Cancelled;
                taskResult.ErrorMessage = "Execution was cancelled before the next step started.";
                break;
            }

            var stepResult = CreateStepResult(step);
            taskResult.Steps.Add(stepResult);

            if (!step.Enabled)
            {
                SetStepStatus(stepResult, ExecutionStatus.Skipped, "Step is disabled.");
                CompleteStep(stepResult);
                continue;
            }

            await ExecuteStepAsync(config, plan, options, step, stepResult, cancellationToken);

            if (IsFailure(stepResult.Status) && options.StopOnFailure)
            {
                _logger.Warning($"Stopping task plan '{plan.Id}' because step '{step.Id}' ended with {stepResult.Status}.");
                break;
            }
        }

        CompleteTaskResult(taskResult);
        executionResult.Status = taskResult.Status;
        executionResult.ErrorMessage = taskResult.ErrorMessage;
        executionResult.CompletedAt = DateTimeOffset.UtcNow;

        _logger.Info($"Task plan '{plan.Id}' finished with status {executionResult.Status}.");
        _progressReporter.Report(new ExecutionProgressEvent
        {
            EventType = ExecutionProgressEventType.TaskCompleted,
            TaskPlanId = plan.Id,
            Status = executionResult.Status,
            Message = $"Task plan '{plan.Id}' finished with status {executionResult.Status}."
        });
        return executionResult;
    }

    private async Task ExecuteStepAsync(
        OrchestratorConfig config,
        TaskPlan plan,
        ExecutionOptions options,
        TaskStep step,
        StepExecutionResult stepResult,
        CancellationToken cancellationToken)
    {
        IProcessHandle? process = null;
        var stepTimeout = options.GetStepTimeout(step);
        using var stepCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (stepTimeout.HasValue)
        {
            stepCancellation.CancelAfter(stepTimeout.Value);
        }

        try
        {
            if (step.CompletionStrategy != CompletionStrategy.ProcessExit)
            {
                SetStepStatus(
                    stepResult,
                    ExecutionStatus.Failed,
                    $"Completion strategy '{step.CompletionStrategy}' is reserved for a later phase. MVP supports only processExit.");
                return;
            }

            var tool = config.FindTool(step.ToolId);
            if (tool is null)
            {
                SetStepStatus(stepResult, ExecutionStatus.Failed, $"Tool '{step.ToolId}' was not found.");
                return;
            }

            SetStepStatus(stepResult, ExecutionStatus.Launching, $"Launching tool '{tool.Id}'.");
            process = await _processLauncher.LaunchAsync(tool, stepCancellation.Token);
            stepResult.ProcessId = process.ProcessId;

            SetStepStatus(stepResult, ExecutionStatus.RunningActions, "Running configured actions.");
            await ExecuteActionsAsync(config, plan, options, step, tool, process, stepResult, stepCancellation, cancellationToken);

            if (IsFailure(stepResult.Status))
            {
                if (stepResult.Status == ExecutionStatus.TimedOut && options.KillOnTimeout && process is not null)
                {
                    await process.KillAsync(CancellationToken.None);
                    stepResult.WasKilled = true;
                }

                return;
            }

            if (step.WaitForExit ?? options.WaitForExit)
            {
                SetStepStatus(stepResult, ExecutionStatus.WaitingForExit, "Waiting for process exit.");
                var exitResult = await process.WaitForExitAsync(
                    options.GetProcessExitTimeout(),
                    options.KillOnTimeout,
                    stepCancellation.Token);

                stepResult.ExitCode = exitResult.ExitCode;
                stepResult.WasKilled = exitResult.WasKilled;

                if (exitResult.Status != ExecutionStatus.Succeeded)
                {
                    SetStepStatus(stepResult, exitResult.Status, exitResult.ErrorMessage ?? "Process did not exit successfully.");
                    return;
                }
            }

            SetStepStatus(stepResult, ExecutionStatus.Succeeded, "Step completed.");
        }
        catch (OperationCanceledException)
        {
            var status = cancellationToken.IsCancellationRequested ? ExecutionStatus.Cancelled : ExecutionStatus.TimedOut;
            SetStepStatus(stepResult, status, status == ExecutionStatus.Cancelled ? "Step was cancelled." : "Step timed out.");

            if (status == ExecutionStatus.TimedOut && options.KillOnTimeout && process is not null)
            {
                await process.KillAsync(CancellationToken.None);
                stepResult.WasKilled = true;
            }
        }
        catch (Exception exception)
        {
            SetStepStatus(stepResult, ExecutionStatus.Failed, exception.Message);
            _logger.Error($"Step '{step.Id}' failed.", exception);
        }
        finally
        {
            if (process is not null)
            {
                stepResult.ExitCode ??= process.ExitCode;
            }

            CompleteStep(stepResult);
        }
    }

    private async Task ExecuteActionsAsync(
        OrchestratorConfig config,
        TaskPlan plan,
        ExecutionOptions options,
        TaskStep step,
        ToolDefinition tool,
        IProcessHandle process,
        StepExecutionResult stepResult,
        CancellationTokenSource stepCancellation,
        CancellationToken rootCancellation)
    {
        var context = new ActionExecutionContext
        {
            Config = config,
            Plan = plan,
            Step = step,
            Tool = tool,
            ExecutionOptions = options,
            Process = process,
            Logger = _logger
        };

        foreach (var action in step.Actions.OrderBy(action => action.Order).ThenBy(action => action.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (!action.Enabled)
            {
                stepResult.Actions.Add(new ActionExecutionResult
                {
                    ActionId = action.Id,
                    ActionType = action.Type,
                    ActionName = action.Name,
                    Status = ExecutionStatus.Skipped,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = "Action is disabled."
                });
                continue;
            }

            if (!_actionExecutorFactory.TryGetExecutor(action.Type, out var executor))
            {
                var unknownActionResult = new ActionExecutionResult
                {
                    ActionId = action.Id,
                    ActionType = action.Type,
                    ActionName = action.Name,
                    Status = ExecutionStatus.Failed,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = $"Action type '{action.Type}' is not registered."
                };

                stepResult.Actions.Add(unknownActionResult);

                if (!action.ContinueOnError)
                {
                    SetStepStatus(stepResult, ExecutionStatus.Failed, unknownActionResult.ErrorMessage);
                    return;
                }

                continue;
            }

            var actionResult = await ExecuteActionWithTimeoutAsync(
                action,
                executor,
                context,
                options,
                stepCancellation,
                rootCancellation);

            stepResult.Actions.Add(actionResult);
            _progressReporter.Report(new ExecutionProgressEvent
            {
                EventType = ExecutionProgressEventType.ActionStatusChanged,
                TaskPlanId = plan.Id,
                StepId = step.Id,
                ToolId = tool.Id,
                ActionId = action.Id,
                ActionType = action.Type,
                Status = actionResult.Status,
                Level = IsFailure(actionResult.Status) ? "Error" : "Information",
                Message = actionResult.ErrorMessage ?? $"Action '{action.Id}' completed with {actionResult.Status}."
            });

            if (IsFailure(actionResult.Status) && !action.ContinueOnError)
            {
                SetStepStatus(stepResult, actionResult.Status, actionResult.ErrorMessage ?? $"Action '{action.Id}' failed.");
                return;
            }
        }
    }

    private static async Task<ActionExecutionResult> ExecuteActionWithTimeoutAsync(
        AutomationActionDefinition action,
        IActionExecutor executor,
        ActionExecutionContext context,
        ExecutionOptions options,
        CancellationTokenSource stepCancellation,
        CancellationToken rootCancellation)
    {
        using var actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(stepCancellation.Token);
        var actionTimeout = ActionParameterReader.GetTimeout(
            action.Parameters,
            action.TimeoutSeconds ?? options.DefaultActionTimeoutSeconds,
            action.TimeoutMinutes);

        if (actionTimeout.HasValue)
        {
            actionCancellation.CancelAfter(actionTimeout.Value);
        }

        var actionResult = await executor.ExecuteAsync(action, context, actionCancellation.Token);
        if (actionResult.Status == ExecutionStatus.Cancelled && !rootCancellation.IsCancellationRequested)
        {
            if (actionCancellation.IsCancellationRequested || stepCancellation.IsCancellationRequested)
            {
                actionResult.Status = ExecutionStatus.TimedOut;
                actionResult.ErrorMessage = $"Action '{action.Id}' timed out.";
            }
        }

        return actionResult;
    }

    private static StepExecutionResult CreateStepResult(TaskStep step)
    {
        return new StepExecutionResult
        {
            StepId = step.Id,
            StepName = step.Name,
            ToolId = step.ToolId,
            Status = ExecutionStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow
        };
    }

    private void SetStepStatus(StepExecutionResult result, ExecutionStatus status, string? message)
    {
        result.Status = status;
        result.ErrorMessage = IsFailure(status) ? message : result.ErrorMessage;
        result.StatusTransitions.Add(new StatusTransition
        {
            At = DateTimeOffset.UtcNow,
            Status = status,
            Message = message ?? string.Empty
        });

        _logger.StatusChanged(result.StepId, status, message);
        _progressReporter.Report(new ExecutionProgressEvent
        {
            EventType = ExecutionProgressEventType.StepStatusChanged,
            StepId = result.StepId,
            ToolId = result.ToolId,
            Status = status,
            Level = IsFailure(status) ? "Error" : "Information",
            Message = message ?? string.Empty
        });
    }

    private static void CompleteStep(StepExecutionResult result)
    {
        result.CompletedAt = DateTimeOffset.UtcNow;
    }

    private static void CompleteTaskResult(TaskExecutionResult taskResult)
    {
        taskResult.CompletedAt = DateTimeOffset.UtcNow;

        if (taskResult.Status == ExecutionStatus.Cancelled)
        {
            return;
        }

        if (taskResult.Steps.Any(step => step.Status == ExecutionStatus.Cancelled))
        {
            taskResult.Status = ExecutionStatus.Cancelled;
            taskResult.ErrorMessage = "At least one step was cancelled.";
            return;
        }

        if (taskResult.Steps.Any(step => step.Status == ExecutionStatus.TimedOut))
        {
            taskResult.Status = ExecutionStatus.TimedOut;
            taskResult.ErrorMessage = "At least one step timed out.";
            return;
        }

        if (taskResult.Steps.Any(step => step.Status == ExecutionStatus.Failed))
        {
            taskResult.Status = ExecutionStatus.Failed;
            taskResult.ErrorMessage = "At least one step failed.";
            return;
        }

        taskResult.Status = ExecutionStatus.Succeeded;
    }

    private static bool IsFailure(ExecutionStatus status)
    {
        return status is ExecutionStatus.Failed or ExecutionStatus.Cancelled or ExecutionStatus.TimedOut;
    }

    private void ReportLog(
        string taskPlanId,
        string stepId,
        string toolId,
        string actionId,
        string level,
        string message)
    {
        _progressReporter.Report(new ExecutionProgressEvent
        {
            EventType = ExecutionProgressEventType.LogMessage,
            TaskPlanId = taskPlanId,
            StepId = stepId,
            ToolId = toolId,
            ActionId = actionId,
            Level = level,
            Message = message
        });
    }
}
