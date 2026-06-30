using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Automation;
using GameToolOrchestrator.Core.Models;
using GameToolOrchestrator.Core.Progress;
using GameToolOrchestrator.Wpf.Services;

namespace GameToolOrchestrator.Tests;

internal sealed class InMemoryExecutionLogger : IExecutionLogger
{
    public List<string> Infos { get; } = [];

    public List<string> Warnings { get; } = [];

    public List<string> Errors { get; } = [];

    public List<(string ScopeId, ExecutionStatus Status, string? Message)> StatusChanges { get; } = [];

    public void Info(string message)
    {
        Infos.Add(message);
    }

    public void Warning(string message)
    {
        Warnings.Add(message);
    }

    public void Error(string message, Exception? exception = null)
    {
        Errors.Add(exception is null ? message : $"{message}: {exception.Message}");
    }

    public void StatusChanged(string scopeId, ExecutionStatus status, string? message = null)
    {
        StatusChanges.Add((scopeId, status, message));
    }
}

internal sealed class FakeProcessLauncher : IProcessLauncher
{
    private readonly Queue<FakeProcessHandle> _handles = new();

    public List<string> LaunchedToolIds { get; } = [];

    public void Enqueue(FakeProcessHandle handle)
    {
        _handles.Enqueue(handle);
    }

    public Task<IProcessHandle> LaunchAsync(ToolDefinition tool, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LaunchedToolIds.Add(tool.Id);

        var handle = _handles.Count > 0 ? _handles.Dequeue() : new FakeProcessHandle();
        return Task.FromResult<IProcessHandle>(handle);
    }
}

internal sealed class FakeProcessHandle : IProcessHandle
{
    private static int s_nextProcessId = 1000;

    public FakeProcessHandle()
    {
        ProcessId = Interlocked.Increment(ref s_nextProcessId);
    }

    public int ProcessId { get; }

    public string ProcessName { get; init; } = "fake";

    public bool HasExited { get; private set; }

    public int? ExitCode { get; private set; }

    public bool NeverExits { get; init; }

    public TimeSpan? ExitAfter { get; init; } = TimeSpan.Zero;

    public bool Killed { get; private set; }

    public List<(TimeSpan? Timeout, bool KillOnTimeout)> WaitCalls { get; } = [];

    public async Task<ProcessExitResult> WaitForExitAsync(
        TimeSpan? timeout,
        bool killOnTimeout,
        CancellationToken cancellationToken)
    {
        WaitCalls.Add((timeout, killOnTimeout));

        try
        {
            if (HasExited)
            {
                return ProcessExitResult.Succeeded(ExitCode);
            }

            if (NeverExits)
            {
                if (!timeout.HasValue)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return ProcessExitResult.Succeeded(ExitCode);
                }

                await Task.Delay(timeout.Value, cancellationToken);
                if (killOnTimeout)
                {
                    await KillAsync(CancellationToken.None);
                }

                return ProcessExitResult.TimedOut(killOnTimeout, "Fake process timed out.");
            }

            var exitAfter = ExitAfter ?? TimeSpan.Zero;
            if (timeout.HasValue && timeout.Value < exitAfter)
            {
                await Task.Delay(timeout.Value, cancellationToken);
                if (killOnTimeout)
                {
                    await KillAsync(CancellationToken.None);
                }

                return ProcessExitResult.TimedOut(killOnTimeout, "Fake process timed out.");
            }

            if (exitAfter > TimeSpan.Zero)
            {
                await Task.Delay(exitAfter, cancellationToken);
            }

            HasExited = true;
            ExitCode = 0;
            return ProcessExitResult.Succeeded(ExitCode);
        }
        catch (OperationCanceledException)
        {
            return ProcessExitResult.Cancelled();
        }
    }

    public Task KillAsync(CancellationToken cancellationToken)
    {
        Killed = true;
        HasExited = true;
        ExitCode = -1;
        return Task.CompletedTask;
    }
}

internal sealed class FakeUiAutomationService : IUiAutomationService
{
    public IReadOnlyList<UiWindowInfo> VisibleWindows { get; set; } = [];

    public IReadOnlyList<UiWindowInfo> MatchingWindows { get; set; } = [];

    public UiWindowInfo? WindowToReturn { get; set; }

    public UiClickResult? ClickResult { get; set; }

    public UiElementSearchResult? ElementSearchResult { get; set; }

    public string ControlTreeSummary { get; set; } = string.Empty;

    public UiWindowSearchCriteria? LastWindowCriteria { get; private set; }

    public UiButtonSearchCriteria? LastButtonCriteria { get; private set; }

    public TimeSpan? LastTimeout { get; private set; }

    public int? LastDiagnosticMaxDepth { get; private set; }

    public int ClickCount { get; private set; }

    public Task<IReadOnlyList<UiWindowInfo>> GetVisibleWindowsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(VisibleWindows);
    }

    public Task<IReadOnlyList<UiWindowInfo>> FindWindowsAsync(
        UiWindowSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        LastWindowCriteria = criteria;
        return Task.FromResult(MatchingWindows);
    }

    public Task<UiWindowInfo?> WaitForWindowAsync(
        UiWindowSearchCriteria criteria,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        LastWindowCriteria = criteria;
        LastTimeout = timeout;
        return Task.FromResult(WindowToReturn);
    }

    public Task<UiClickResult> ClickButtonAsync(
        UiButtonSearchCriteria criteria,
        TimeSpan timeout,
        int diagnosticMaxDepth,
        CancellationToken cancellationToken)
    {
        LastButtonCriteria = criteria;
        LastTimeout = timeout;
        LastDiagnosticMaxDepth = diagnosticMaxDepth;
        ClickCount++;
        return Task.FromResult(ClickResult ?? UiClickResult.Failed(ExecutionStatus.TimedOut, "Fake click timed out."));
    }

    public Task<UiElementSearchResult> FindElementsAsync(
        UiButtonSearchCriteria criteria,
        int diagnosticMaxDepth,
        CancellationToken cancellationToken)
    {
        LastButtonCriteria = criteria;
        LastDiagnosticMaxDepth = diagnosticMaxDepth;
        return Task.FromResult(ElementSearchResult ?? new UiElementSearchResult
        {
            Status = ExecutionStatus.Failed,
            ErrorMessage = "Fake element not found."
        });
    }

    public Task<string> GetControlTreeSummaryAsync(
        UiWindowSearchCriteria criteria,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        LastWindowCriteria = criteria;
        LastDiagnosticMaxDepth = maxDepth;
        return Task.FromResult(ControlTreeSummary);
    }
}

internal sealed class FakeUserConfirmationService : IUserConfirmationService
{
    public bool Response { get; set; } = true;

    public bool DeleteResponse { get; set; } = true;

    public int CallCount { get; private set; }

    public int DeleteCallCount { get; private set; }

    public string? LastOperationName { get; private set; }

    public string? LastDeleteDescription { get; private set; }

    public bool ConfirmDiscardUnsavedChanges(string operationName)
    {
        CallCount++;
        LastOperationName = operationName;
        return Response;
    }

    public bool ConfirmDelete(string objectDescription)
    {
        DeleteCallCount++;
        LastDeleteDescription = objectDescription;
        return DeleteResponse;
    }
}

internal sealed class FakeClipboardService : IClipboardService
{
    public int CopyCount { get; private set; }

    public string LastText { get; private set; } = string.Empty;

    public void CopyText(string text)
    {
        CopyCount++;
        LastText = text;
    }
}

internal sealed class FakeFolderLauncherService : IFolderLauncherService
{
    public List<string> OpenedFolders { get; } = [];

    public Exception? ExceptionToThrow { get; set; }

    public void OpenFolder(string folderPath)
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        OpenedFolders.Add(folderPath);
    }
}

internal sealed class InMemoryConfigRepository : IConfigRepository
{
    public OrchestratorConfig Config { get; set; } = new();

    public Exception? LoadException { get; set; }

    public Exception? SaveException { get; set; }

    public string? LastLoadPath { get; private set; }

    public string? LastSavePath { get; private set; }

    public Task<OrchestratorConfig> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        LastLoadPath = path;
        if (LoadException is not null)
        {
            throw LoadException;
        }

        return Task.FromResult(Config);
    }

    public Task SaveAsync(string path, OrchestratorConfig config, CancellationToken cancellationToken = default)
    {
        LastSavePath = path;
        if (SaveException is not null)
        {
            throw SaveException;
        }

        Config = config;
        return Task.CompletedTask;
    }
}

internal sealed class FakeExecutionEngineFactory : IExecutionEngineFactory
{
    public FakeExecutionEngine Engine { get; } = new();

    public IExecutionEngine Create(IExecutionProgressReporter progressReporter)
    {
        Engine.ProgressReporter = progressReporter;
        return Engine;
    }
}

internal sealed class FakeExecutionEngine : IExecutionEngine
{
    public int ExecuteCallCount { get; private set; }

    public string? LastTaskPlanId { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public IExecutionProgressReporter? ProgressReporter { get; set; }

    public TaskCompletionSource<ExecutionResult>? CompletionSource { get; set; }

    public ExecutionResult? ResultToReturn { get; set; }

    public async Task<ExecutionResult> ExecuteAsync(
        OrchestratorConfig config,
        string taskPlanId,
        CancellationToken cancellationToken = default)
    {
        ExecuteCallCount++;
        LastTaskPlanId = taskPlanId;
        LastCancellationToken = cancellationToken;

        ProgressReporter?.Report(new ExecutionProgressEvent
        {
            EventType = ExecutionProgressEventType.StepStatusChanged,
            StepId = config.FindTaskPlan(taskPlanId)?.Steps.FirstOrDefault()?.Id ?? string.Empty,
            ToolId = config.FindTaskPlan(taskPlanId)?.Steps.FirstOrDefault()?.ToolId ?? string.Empty,
            Status = ExecutionStatus.Launching,
            Message = "Fake launch"
        });

        if (CompletionSource is not null)
        {
            return await CompletionSource.Task;
        }

        if (ResultToReturn is not null)
        {
            return ResultToReturn;
        }

        return new ExecutionResult
        {
            TaskPlanId = taskPlanId,
            Status = ExecutionStatus.Succeeded,
            Tasks =
            [
                new TaskExecutionResult
                {
                    TaskPlanId = taskPlanId,
                    Status = ExecutionStatus.Succeeded,
                    Steps = config.FindTaskPlan(taskPlanId)?.Steps.Select(step => new StepExecutionResult
                    {
                        StepId = step.Id,
                        StepName = step.Name,
                        ToolId = step.ToolId,
                        Status = ExecutionStatus.Succeeded
                    }).ToList() ?? []
                }
            ]
        };
    }
}

internal sealed class RecordingProgressReporter : IExecutionProgressReporter
{
    public List<ExecutionProgressEvent> Events { get; } = [];

    public void Report(ExecutionProgressEvent progressEvent)
    {
        Events.Add(progressEvent);
    }
}

internal sealed class FakeActionExecutorFactory : IActionExecutorFactory
{
    private readonly IActionExecutor _executor;

    public FakeActionExecutorFactory(IActionExecutor executor)
    {
        _executor = executor;
    }

    public bool TryGetExecutor(string actionType, out IActionExecutor executor)
    {
        executor = _executor;
        return string.Equals(actionType, _executor.ActionType, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class FakeActionExecutor : IActionExecutor
{
    public string ActionType => "fakeAction";

    public AutomationActionDefinition? LastAction { get; private set; }

    public Task<ActionExecutionResult> ExecuteAsync(
        AutomationActionDefinition action,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        LastAction = action;
        return Task.FromResult(new ActionExecutionResult
        {
            ActionId = action.Id,
            ActionType = action.Type,
            ActionName = action.Name,
            Status = ExecutionStatus.Succeeded,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        });
    }
}
