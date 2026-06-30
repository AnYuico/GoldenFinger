using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Actions;
using GameToolOrchestrator.Core.Automation;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Infrastructure.Automation.Actions;

public sealed class WaitWindowActionExecutor : IActionExecutor
{
    public const string TypeName = "waitWindow";

    private readonly IUiAutomationService _automationService;
    private readonly int _diagnosticMaxDepth;

    public WaitWindowActionExecutor(IUiAutomationService automationService, int diagnosticMaxDepth = 4)
    {
        _automationService = automationService;
        _diagnosticMaxDepth = diagnosticMaxDepth;
    }

    public string ActionType => TypeName;

    public async Task<ActionExecutionResult> ExecuteAsync(
        AutomationActionDefinition action,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var result = ActionExecutionResult.Started(action);
        var criteria = CreateCriteria(action, context.Tool);
        var timeout = ActionParameterReader.GetTimeout(action.Parameters, action.TimeoutSeconds, action.TimeoutMinutes)
            ?? TimeSpan.FromSeconds(context.ExecutionOptions.DefaultActionTimeoutSeconds);

        if (!criteria.HasAnyTitleMatcher)
        {
            result.Status = ExecutionStatus.Failed;
            result.ErrorMessage = "waitWindow requires titleEquals or titleContains.";
            result.CompletedAt = DateTimeOffset.UtcNow;
            return result;
        }

        try
        {
            context.Logger.Info($"Waiting for window: {criteria}, timeout={timeout.TotalSeconds:0.###}s.");
            var window = await _automationService.WaitForWindowAsync(criteria, timeout, cancellationToken);
            if (window is not null)
            {
                result.Status = ExecutionStatus.Succeeded;
                result.Outputs["windowTitle"] = window.Title;
                result.Outputs["automationId"] = window.AutomationId;
                result.CompletedAt = DateTimeOffset.UtcNow;
                return result;
            }

            result.Status = ExecutionStatus.TimedOut;
            result.ErrorMessage = $"Timed out waiting for window: {criteria}.";
            await LogWindowDiagnosticsAsync(context.Logger, criteria, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            result.Status = ExecutionStatus.Cancelled;
            result.ErrorMessage = "waitWindow was cancelled.";
        }
        catch (Exception exception)
        {
            result.Status = ExecutionStatus.Failed;
            result.ErrorMessage = exception.Message;
            context.Logger.Error($"waitWindow failed for {criteria}.", exception);
            await LogWindowDiagnosticsAsync(context.Logger, criteria, cancellationToken);
        }

        result.CompletedAt = DateTimeOffset.UtcNow;
        return result;
    }

    private async Task LogWindowDiagnosticsAsync(
        IExecutionLogger logger,
        UiWindowSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var windows = await _automationService.GetVisibleWindowsAsync(cancellationToken);
        logger.Warning("Visible windows: " + FormatWindows(windows));

        var tree = await _automationService.GetControlTreeSummaryAsync(criteria, _diagnosticMaxDepth, cancellationToken);
        if (!string.IsNullOrWhiteSpace(tree))
        {
            logger.Warning("Target window control tree:" + Environment.NewLine + tree);
        }
    }

    private static UiWindowSearchCriteria CreateCriteria(AutomationActionDefinition action, ToolDefinition tool)
    {
        var criteria = new UiWindowSearchCriteria
        {
            TitleEquals = UiActionParameterReader.GetString(action, action.TitleEquals, "titleEquals"),
            TitleContains = UiActionParameterReader.GetString(action, action.TitleContains, "titleContains")
        };

        if (criteria.HasAnyTitleMatcher || tool.Window is null || string.IsNullOrWhiteSpace(tool.Window.Title))
        {
            return criteria;
        }

        if (string.Equals(tool.Window.TitleMatchMode, "exact", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tool.Window.TitleMatchMode, "equals", StringComparison.OrdinalIgnoreCase))
        {
            criteria.TitleEquals = tool.Window.Title;
        }
        else
        {
            criteria.TitleContains = tool.Window.Title;
        }

        return criteria;
    }

    private static string FormatWindows(IEnumerable<UiWindowInfo> windows)
    {
        var titles = windows
            .Select(window => string.IsNullOrWhiteSpace(window.Title) ? "<empty title>" : window.Title)
            .ToArray();

        return titles.Length == 0 ? "<none>" : string.Join(" | ", titles);
    }
}
