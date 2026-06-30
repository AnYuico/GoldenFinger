using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Actions;
using GameToolOrchestrator.Core.Automation;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Infrastructure.Automation.Actions;

public sealed class ClickButtonActionExecutor : IActionExecutor
{
    public const string TypeName = "clickButton";

    private readonly IUiAutomationService _automationService;
    private readonly int _diagnosticMaxDepth;

    public ClickButtonActionExecutor(IUiAutomationService automationService, int diagnosticMaxDepth = 4)
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

        if (!HasButtonMatcher(criteria))
        {
            result.Status = ExecutionStatus.Failed;
            result.ErrorMessage = "clickButton requires automationId, nameEquals, or nameContains.";
            result.CompletedAt = DateTimeOffset.UtcNow;
            return result;
        }

        try
        {
            context.Logger.Info($"Clicking button: {criteria}, timeout={timeout.TotalSeconds:0.###}s.");
            var clickResult = await _automationService.ClickButtonAsync(
                criteria,
                timeout,
                _diagnosticMaxDepth,
                cancellationToken);

            result.Status = clickResult.Status;
            result.ErrorMessage = clickResult.ErrorMessage;

            if (clickResult.Window is not null)
            {
                result.Outputs["windowTitle"] = clickResult.Window.Title;
            }

            if (clickResult.Element is not null)
            {
                result.Outputs["buttonName"] = clickResult.Element.Name;
                result.Outputs["automationId"] = clickResult.Element.AutomationId;
                result.Outputs["controlType"] = clickResult.Element.ControlType;
            }

            if (clickResult.Status != ExecutionStatus.Succeeded)
            {
                LogDiagnostics(context.Logger, clickResult);
            }
        }
        catch (OperationCanceledException)
        {
            result.Status = ExecutionStatus.Cancelled;
            result.ErrorMessage = "clickButton was cancelled.";
        }
        catch (Exception exception)
        {
            result.Status = ExecutionStatus.Failed;
            result.ErrorMessage = exception.Message;
            context.Logger.Error($"clickButton failed for {criteria}.", exception);
            await LogFallbackDiagnosticsAsync(context.Logger, criteria, cancellationToken);
        }

        result.CompletedAt = DateTimeOffset.UtcNow;
        return result;
    }

    private static UiButtonSearchCriteria CreateCriteria(AutomationActionDefinition action, ToolDefinition tool)
    {
        var criteria = new UiButtonSearchCriteria
        {
            WindowTitleEquals = UiActionParameterReader.GetString(action, action.WindowTitleEquals, "windowTitleEquals"),
            WindowTitleContains = UiActionParameterReader.GetString(action, action.WindowTitleContains, "windowTitleContains"),
            AutomationId = UiActionParameterReader.GetString(action, action.AutomationId, "automationId"),
            NameEquals = UiActionParameterReader.GetString(action, action.NameEquals, "nameEquals"),
            NameContains = UiActionParameterReader.GetString(action, action.NameContains, "nameContains"),
            ControlType = UiActionParameterReader.GetString(action, action.ControlType, "controlType")
        };

        if (string.IsNullOrWhiteSpace(criteria.ControlType))
        {
            criteria.ControlType = "Button";
        }

        if ((!string.IsNullOrWhiteSpace(criteria.WindowTitleEquals) ||
             !string.IsNullOrWhiteSpace(criteria.WindowTitleContains)) ||
            tool.Window is null ||
            string.IsNullOrWhiteSpace(tool.Window.Title))
        {
            return criteria;
        }

        if (string.Equals(tool.Window.TitleMatchMode, "exact", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tool.Window.TitleMatchMode, "equals", StringComparison.OrdinalIgnoreCase))
        {
            criteria.WindowTitleEquals = tool.Window.Title;
        }
        else
        {
            criteria.WindowTitleContains = tool.Window.Title;
        }

        return criteria;
    }

    private static bool HasButtonMatcher(UiButtonSearchCriteria criteria)
    {
        return !string.IsNullOrWhiteSpace(criteria.AutomationId) ||
               !string.IsNullOrWhiteSpace(criteria.NameEquals) ||
               !string.IsNullOrWhiteSpace(criteria.NameContains);
    }

    private static void LogDiagnostics(IExecutionLogger logger, UiClickResult clickResult)
    {
        if (!string.IsNullOrWhiteSpace(clickResult.VisibleWindowsSummary))
        {
            logger.Warning("Visible windows: " + clickResult.VisibleWindowsSummary);
        }

        if (!string.IsNullOrWhiteSpace(clickResult.ControlTreeSummary))
        {
            logger.Warning("Target window control tree:" + Environment.NewLine + clickResult.ControlTreeSummary);
        }

        if (!string.IsNullOrWhiteSpace(clickResult.ErrorMessage))
        {
            logger.Warning(clickResult.ErrorMessage);
        }
    }

    private async Task LogFallbackDiagnosticsAsync(
        IExecutionLogger logger,
        UiButtonSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var windows = await _automationService.GetVisibleWindowsAsync(cancellationToken);
        logger.Warning("Visible windows: " + FormatWindows(windows));

        var tree = await _automationService.GetControlTreeSummaryAsync(
            criteria.ToWindowCriteria(),
            _diagnosticMaxDepth,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(tree))
        {
            logger.Warning("Target window control tree:" + Environment.NewLine + tree);
        }
    }

    private static string FormatWindows(IEnumerable<UiWindowInfo> windows)
    {
        var titles = windows
            .Select(window => string.IsNullOrWhiteSpace(window.Title) ? "<empty title>" : window.Title)
            .ToArray();

        return titles.Length == 0 ? "<none>" : string.Join(" | ", titles);
    }
}
