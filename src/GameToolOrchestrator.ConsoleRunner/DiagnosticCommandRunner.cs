using System.Diagnostics;
using System.Text;
using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Automation;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.ConsoleRunner;

public sealed class DiagnosticCommandRunner
{
    private static readonly TimeSpan SelectorPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly IUiAutomationService _automationService;
    private readonly IConfigRepository _configRepository;
    private readonly IActionExecutorFactory _actionExecutorFactory;
    private readonly IExecutionLogger _logger;

    public DiagnosticCommandRunner(
        IUiAutomationService automationService,
        IConfigRepository configRepository,
        IActionExecutorFactory actionExecutorFactory,
        IExecutionLogger logger)
    {
        _automationService = automationService;
        _configRepository = configRepository;
        _actionExecutorFactory = actionExecutorFactory;
        _logger = logger;
    }

    public async Task<CommandExecutionResult> ExecuteAsync(
        RunnerCommand command,
        CancellationToken cancellationToken)
    {
        return command.Type switch
        {
            RunnerCommandType.InspectWindows => await InspectWindowsAsync(cancellationToken),
            RunnerCommandType.InspectWindow => await InspectWindowAsync(command, cancellationToken),
            RunnerCommandType.TestSelector => await TestSelectorAsync(command, cancellationToken),
            RunnerCommandType.RunAction => await RunActionAsync(command, cancellationToken),
            _ => new CommandExecutionResult(64, string.Empty, "Unsupported diagnostic command.")
        };
    }

    private async Task<CommandExecutionResult> InspectWindowsAsync(CancellationToken cancellationToken)
    {
        var windows = await _automationService.GetVisibleWindowsAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine($"Visible windows: {windows.Count}");

        for (var index = 0; index < windows.Count; index++)
        {
            builder.AppendLine(FormatWindow(windows[index], index));
        }

        return new CommandExecutionResult(0, builder.ToString());
    }

    private async Task<CommandExecutionResult> InspectWindowAsync(
        RunnerCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.WindowCriteria.HasAnyTitleMatcher)
        {
            return new CommandExecutionResult(
                64,
                string.Empty,
                "inspect-window requires --title-equals or --title-contains.");
        }

        var windows = await _automationService.FindWindowsAsync(command.WindowCriteria, cancellationToken);
        var builder = new StringBuilder();

        if (windows.Count == 0)
        {
            var visibleWindows = await _automationService.GetVisibleWindowsAsync(cancellationToken);
            builder.AppendLine($"No window matched: {command.WindowCriteria}");
            builder.AppendLine("Visible windows:");
            AppendWindows(builder, visibleWindows);
            return new CommandExecutionResult(1, builder.ToString());
        }

        builder.AppendLine($"Matched windows: {windows.Count}");
        AppendWindows(builder, windows);
        builder.AppendLine();
        builder.AppendLine("Inspecting first matched window:");
        builder.AppendLine(FormatWindow(windows[0], 0));
        builder.AppendLine();
        builder.AppendLine($"Control tree (maxDepth={command.MaxDepth}):");
        builder.AppendLine(await _automationService.GetControlTreeSummaryAsync(
            command.WindowCriteria,
            command.MaxDepth,
            cancellationToken));

        return new CommandExecutionResult(0, builder.ToString());
    }

    private async Task<CommandExecutionResult> TestSelectorAsync(
        RunnerCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.ButtonCriteria.ToWindowCriteria().HasAnyTitleMatcher)
        {
            return new CommandExecutionResult(
                64,
                string.Empty,
                "test-selector requires --window-title-equals or --window-title-contains.");
        }

        if (!HasElementMatcher(command.ButtonCriteria))
        {
            return new CommandExecutionResult(
                64,
                string.Empty,
                "test-selector requires --automation-id, --name-equals, or --name-contains.");
        }

        var timeout = TimeSpan.FromSeconds(Math.Max(0, command.TimeoutSeconds));
        var searchResult = await WaitForElementsAsync(
            command.ButtonCriteria,
            timeout,
            command.MaxDepth,
            cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine($"Selector: {command.ButtonCriteria}");
        builder.AppendLine($"Matched controls: {searchResult.Elements.Count}");

        if (searchResult.Window is not null)
        {
            builder.AppendLine("Window:");
            builder.AppendLine(FormatWindow(searchResult.Window, 0));
        }

        foreach (var element in searchResult.Elements)
        {
            builder.AppendLine(FormatElement(element));
        }

        if (searchResult.Elements.Count == 0)
        {
            builder.AppendLine(searchResult.ErrorMessage ?? "No control matched.");
            builder.AppendLine("Visible windows:");
            builder.AppendLine(string.IsNullOrWhiteSpace(searchResult.VisibleWindowsSummary)
                ? "<none>"
                : searchResult.VisibleWindowsSummary);
            builder.AppendLine("Control tree:");
            builder.AppendLine(searchResult.ControlTreeSummary);
            return new CommandExecutionResult(1, builder.ToString());
        }

        if (!command.Click)
        {
            builder.AppendLine("Click: false. No click was performed.");
            return new CommandExecutionResult(0, builder.ToString());
        }

        var target = searchResult.Elements[0];
        var clickMessage = $"About to click control: {FormatElement(target)}";
        _logger.Warning(clickMessage);
        builder.AppendLine(clickMessage);

        var clickResult = await _automationService.ClickButtonAsync(
            command.ButtonCriteria,
            timeout,
            command.MaxDepth,
            cancellationToken);

        builder.AppendLine($"Click result: {clickResult.Status}");
        if (!string.IsNullOrWhiteSpace(clickResult.ErrorMessage))
        {
            builder.AppendLine(clickResult.ErrorMessage);
        }

        return new CommandExecutionResult(clickResult.Status == ExecutionStatus.Succeeded ? 0 : 1, builder.ToString());
    }

    private async Task<CommandExecutionResult> RunActionAsync(
        RunnerCommand command,
        CancellationToken cancellationToken)
    {
        var config = await _configRepository.LoadAsync(command.ConfigPath, cancellationToken);
        var tool = config.FindTool(command.ToolId);
        if (tool is null)
        {
            return new CommandExecutionResult(1, string.Empty, $"Tool '{command.ToolId}' was not found.");
        }

        var candidates = config.TaskPlans
            .SelectMany(plan => plan.Steps.Select(step => new { Plan = plan, Step = step }))
            .Where(pair => string.Equals(pair.Step.ToolId, command.ToolId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Step.Actions
                .OrderBy(action => action.Order)
                .ThenBy(action => action.Id, StringComparer.OrdinalIgnoreCase)
                .Select(action => new ActionCandidate(pair.Plan, pair.Step, action)))
            .ToList();

        if (command.ActionIndex < 0 || command.ActionIndex >= candidates.Count)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Action index {command.ActionIndex} was not found for tool '{command.ToolId}'. Index is zero-based.");
            builder.AppendLine("Available actions:");

            for (var index = 0; index < candidates.Count; index++)
            {
                builder.AppendLine($"[{index}] {candidates[index].Action.Id} type={candidates[index].Action.Type}");
            }

            return new CommandExecutionResult(1, builder.ToString());
        }

        var candidate = candidates[command.ActionIndex];
        if (!_actionExecutorFactory.TryGetExecutor(candidate.Action.Type, out var executor))
        {
            return new CommandExecutionResult(
                1,
                string.Empty,
                $"Action type '{candidate.Action.Type}' is not registered.");
        }

        var context = new ActionExecutionContext
        {
            Config = config,
            Plan = candidate.Plan,
            Step = candidate.Step,
            Tool = tool,
            ExecutionOptions = candidate.Plan.Execution ?? config.Execution,
            Process = new NoopProcessHandle(),
            Logger = _logger
        };

        var stopwatch = Stopwatch.StartNew();
        var actionResult = await executor.ExecuteAsync(candidate.Action, context, cancellationToken);
        stopwatch.Stop();

        var output = new StringBuilder();
        output.AppendLine($"Action: {candidate.Action.Id} type={candidate.Action.Type}");
        output.AppendLine($"Status: {actionResult.Status}");
        output.AppendLine($"Elapsed: {stopwatch.Elapsed.TotalMilliseconds:0} ms");

        if (!string.IsNullOrWhiteSpace(actionResult.ErrorMessage))
        {
            output.AppendLine($"Error: {actionResult.ErrorMessage}");
        }

        foreach (var outputValue in actionResult.Outputs)
        {
            output.AppendLine($"{outputValue.Key}: {outputValue.Value}");
        }

        return new CommandExecutionResult(IsSuccess(actionResult.Status) ? 0 : 1, output.ToString());
    }

    private async Task<UiElementSearchResult> WaitForElementsAsync(
        UiButtonSearchCriteria criteria,
        TimeSpan timeout,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        UiElementSearchResult? lastResult = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastResult = await _automationService.FindElementsAsync(criteria, maxDepth, cancellationToken);
            if (lastResult.Elements.Count > 0 || stopwatch.Elapsed >= timeout)
            {
                return lastResult;
            }

            var delay = timeout - stopwatch.Elapsed;
            if (delay > SelectorPollInterval)
            {
                delay = SelectorPollInterval;
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static void AppendWindows(StringBuilder builder, IReadOnlyList<UiWindowInfo> windows)
    {
        if (windows.Count == 0)
        {
            builder.AppendLine("<none>");
            return;
        }

        for (var index = 0; index < windows.Count; index++)
        {
            builder.AppendLine(FormatWindow(windows[index], index));
        }
    }

    private static string FormatWindow(UiWindowInfo window, int index)
    {
        return $"[{index}] Title='{window.Title}', ProcessName='{window.ProcessName}', ProcessId={FormatNullable(window.ProcessId)}, IsVisible={window.IsVisible}, IsMinimized={FormatNullable(window.IsMinimized)}, IsEnabled={window.IsEnabled}, IsOffscreen={window.IsOffscreen}, AutomationId='{window.AutomationId}', ControlType='{window.ControlType}'";
    }

    private static string FormatElement(UiElementInfo element)
    {
        return $"[{element.Index}] Name='{element.Name}', AutomationId='{element.AutomationId}', ControlType='{element.ControlType}', IsEnabled={element.IsEnabled}, IsOffscreen={element.IsOffscreen}, IsClickable={element.IsClickable}";
    }

    private static string FormatNullable<T>(T? value)
        where T : struct
    {
        return value.HasValue ? value.Value.ToString() ?? string.Empty : "unknown";
    }

    private static bool HasElementMatcher(UiButtonSearchCriteria criteria)
    {
        return !string.IsNullOrWhiteSpace(criteria.AutomationId) ||
               !string.IsNullOrWhiteSpace(criteria.NameEquals) ||
               !string.IsNullOrWhiteSpace(criteria.NameContains);
    }

    private static bool IsSuccess(ExecutionStatus status)
    {
        return status is ExecutionStatus.Succeeded or ExecutionStatus.Skipped;
    }

    private sealed record ActionCandidate(
        TaskPlan Plan,
        TaskStep Step,
        AutomationActionDefinition Action);
}
