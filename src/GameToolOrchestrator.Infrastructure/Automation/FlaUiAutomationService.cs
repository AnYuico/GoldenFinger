using System.Diagnostics;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Automation;
using GameToolOrchestrator.Core.Models;
using FlaUiControlType = FlaUI.Core.Definitions.ControlType;

namespace GameToolOrchestrator.Infrastructure.Automation;

public sealed class FlaUiAutomationService : IUiAutomationService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private const int MaxDiagnosticElements = 250;

    public Task<IReadOnlyList<UiWindowInfo>> GetVisibleWindowsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var automation = new UIA3Automation();
        var windows = GetVisibleWindowElements(automation)
            .Select(MapWindow)
            .ToArray();

        return Task.FromResult<IReadOnlyList<UiWindowInfo>>(windows);
    }

    public Task<IReadOnlyList<UiWindowInfo>> FindWindowsAsync(
        UiWindowSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var automation = new UIA3Automation();
        var windows = GetVisibleWindowElements(automation)
            .Where(window => MatchesWindow(window, criteria))
            .Select(MapWindow)
            .ToArray();

        return Task.FromResult<IReadOnlyList<UiWindowInfo>>(windows);
    }

    public async Task<UiWindowInfo?> WaitForWindowAsync(
        UiWindowSearchCriteria criteria,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var automation = new UIA3Automation();
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var window = FindWindowElement(automation, criteria);
            if (window is not null)
            {
                return MapWindow(window);
            }

            if (stopwatch.Elapsed >= timeout)
            {
                return null;
            }

            await Task.Delay(GetNextDelay(timeout, stopwatch.Elapsed), cancellationToken);
        }
    }

    public async Task<UiClickResult> ClickButtonAsync(
        UiButtonSearchCriteria criteria,
        TimeSpan timeout,
        int diagnosticMaxDepth,
        CancellationToken cancellationToken)
    {
        using var automation = new UIA3Automation();
        var stopwatch = Stopwatch.StartNew();
        AutomationElement? lastWindow = null;
        IReadOnlyList<AutomationElement> lastCandidates = [];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var window = FindWindowElement(automation, criteria.ToWindowCriteria());
            if (window is not null)
            {
                lastWindow = window;
                lastCandidates = FindButtonCandidates(window, criteria);
                var button = ChooseBestButton(lastCandidates);

                if (button is not null)
                {
                    var clickResult = TryClickButton(window, button);
                    if (clickResult.Status == ExecutionStatus.Succeeded)
                    {
                        return clickResult;
                    }

                    clickResult.VisibleWindowsSummary = FormatWindows(GetVisibleWindowElements(automation));
                    clickResult.ControlTreeSummary = BuildControlTreeSummary(window, diagnosticMaxDepth);
                    return clickResult;
                }
            }

            if (stopwatch.Elapsed >= timeout)
            {
                var result = UiClickResult.Failed(
                    ExecutionStatus.TimedOut,
                    BuildNotFoundMessage(criteria, lastWindow, lastCandidates));
                result.Window = lastWindow is null ? null : MapWindow(lastWindow);
                result.VisibleWindowsSummary = FormatWindows(GetVisibleWindowElements(automation));
                result.ControlTreeSummary = lastWindow is null
                    ? "<target window not found>"
                    : BuildControlTreeSummary(lastWindow, diagnosticMaxDepth);
                return result;
            }

            await Task.Delay(GetNextDelay(timeout, stopwatch.Elapsed), cancellationToken);
        }
    }

    public Task<UiElementSearchResult> FindElementsAsync(
        UiButtonSearchCriteria criteria,
        int diagnosticMaxDepth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var automation = new UIA3Automation();
        var window = FindWindowElement(automation, criteria.ToWindowCriteria());
        if (window is null)
        {
            return Task.FromResult(new UiElementSearchResult
            {
                Status = ExecutionStatus.TimedOut,
                ErrorMessage = $"Target window was not found for selector: {criteria}.",
                VisibleWindowsSummary = FormatWindows(GetVisibleWindowElements(automation)),
                ControlTreeSummary = "<target window not found>"
            });
        }

        var elements = FindButtonCandidates(window, criteria)
            .Select((element, index) => MapElement(element, index))
            .ToList();

        return Task.FromResult(new UiElementSearchResult
        {
            Status = elements.Count == 0 ? ExecutionStatus.Failed : ExecutionStatus.Succeeded,
            Window = MapWindow(window),
            Elements = elements,
            ErrorMessage = elements.Count == 0 ? $"No controls matched selector: {criteria}." : null,
            VisibleWindowsSummary = FormatWindows(GetVisibleWindowElements(automation)),
            ControlTreeSummary = BuildControlTreeSummary(window, diagnosticMaxDepth)
        });
    }

    public Task<string> GetControlTreeSummaryAsync(
        UiWindowSearchCriteria criteria,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var automation = new UIA3Automation();
        var window = FindWindowElement(automation, criteria);
        var summary = window is null
            ? "<target window not found>"
            : BuildControlTreeSummary(window, maxDepth);

        return Task.FromResult(summary);
    }

    private static AutomationElement[] GetVisibleWindowElements(UIA3Automation automation)
    {
        var desktop = automation.GetDesktop();
        return desktop
            .FindAllChildren(condition => condition.ByControlType(FlaUiControlType.Window))
            .Where(window => !SafeBool(() => window.IsOffscreen, defaultValue: true))
            .ToArray();
    }

    private static AutomationElement? FindWindowElement(UIA3Automation automation, UiWindowSearchCriteria criteria)
    {
        return GetVisibleWindowElements(automation)
            .FirstOrDefault(window => MatchesWindow(window, criteria));
    }

    private static bool MatchesWindow(AutomationElement window, UiWindowSearchCriteria criteria)
    {
        var title = SafeString(() => window.Name);

        if (!string.IsNullOrWhiteSpace(criteria.TitleEquals) &&
            !string.Equals(title, criteria.TitleEquals, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(criteria.TitleContains) &&
            !title.Contains(criteria.TitleContains, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return criteria.HasAnyTitleMatcher || !string.IsNullOrWhiteSpace(title);
    }

    private static IReadOnlyList<AutomationElement> FindButtonCandidates(
        AutomationElement window,
        UiButtonSearchCriteria criteria)
    {
        var controlType = ParseControlType(criteria.ControlType);
        return window
            .FindAllDescendants(condition => condition.ByControlType(controlType))
            .Where(element => MatchesButton(element, criteria))
            .ToArray();
    }

    private static bool MatchesButton(AutomationElement element, UiButtonSearchCriteria criteria)
    {
        var automationId = SafeString(() => element.AutomationId);
        var name = SafeString(() => element.Name);

        if (!string.IsNullOrWhiteSpace(criteria.AutomationId) &&
            !string.Equals(automationId, criteria.AutomationId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(criteria.NameEquals) &&
            !string.Equals(name, criteria.NameEquals, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(criteria.NameContains) &&
            !name.Contains(criteria.NameContains, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static AutomationElement? ChooseBestButton(IReadOnlyList<AutomationElement> candidates)
    {
        return candidates.FirstOrDefault(IsVisibleEnabledClickable)
               ?? candidates.FirstOrDefault(IsVisibleAndEnabled)
               ?? candidates.FirstOrDefault();
    }

    private static UiClickResult TryClickButton(AutomationElement window, AutomationElement button)
    {
        try
        {
            window.SetForeground();
            window.Focus();
        }
        catch
        {
            // Some windows refuse focus changes; continue and let Click report the concrete failure.
        }

        if (!IsVisibleAndEnabled(button))
        {
            return UiClickResult.Failed(
                ExecutionStatus.Failed,
                $"Matched button is not ready. Name='{SafeString(() => button.Name)}', AutomationId='{SafeString(() => button.AutomationId)}', IsEnabled={SafeBool(() => button.IsEnabled, false)}, IsOffscreen={SafeBool(() => button.IsOffscreen, true)}.");
        }

        if (!TryGetClickablePoint(button))
        {
            return UiClickResult.Failed(
                ExecutionStatus.Failed,
                $"Matched button has no clickable point. Name='{SafeString(() => button.Name)}', AutomationId='{SafeString(() => button.AutomationId)}'.");
        }

        try
        {
            button.Click();
            return UiClickResult.Succeeded(MapWindow(window), MapElement(button, index: 0));
        }
        catch (Exception exception)
        {
            return UiClickResult.Failed(
                ExecutionStatus.Failed,
                $"Failed to click button. Name='{SafeString(() => button.Name)}', AutomationId='{SafeString(() => button.AutomationId)}'. {exception.Message}");
        }
    }

    private static string BuildNotFoundMessage(
        UiButtonSearchCriteria criteria,
        AutomationElement? window,
        IReadOnlyList<AutomationElement> lastCandidates)
    {
        if (window is null)
        {
            return $"Timed out waiting for target window while clicking button: {criteria}.";
        }

        return $"Timed out waiting for button: {criteria}. Matching candidate count={lastCandidates.Count}.";
    }

    private static string BuildControlTreeSummary(AutomationElement root, int maxDepth)
    {
        var builder = new StringBuilder();
        var count = 0;
        AppendElement(builder, root, depth: 0, maxDepth, ref count);
        return builder.ToString();
    }

    private static void AppendElement(
        StringBuilder builder,
        AutomationElement element,
        int depth,
        int maxDepth,
        ref int count)
    {
        if (count >= MaxDiagnosticElements)
        {
            if (count == MaxDiagnosticElements)
            {
                builder.AppendLine("... control tree truncated ...");
                count++;
            }

            return;
        }

        builder
            .Append(' ', depth * 2)
            .Append("- Name='")
            .Append(SafeString(() => element.Name))
            .Append("', AutomationId='")
            .Append(SafeString(() => element.AutomationId))
            .Append("', ControlType='")
            .Append(SafeControlType(element))
            .Append("', IsEnabled=")
            .Append(SafeBool(() => element.IsEnabled, false))
            .Append(", IsOffscreen=")
            .Append(SafeBool(() => element.IsOffscreen, true))
            .AppendLine();

        count++;

        if (depth >= maxDepth)
        {
            return;
        }

        foreach (var child in SafeChildren(element))
        {
            AppendElement(builder, child, depth + 1, maxDepth, ref count);
        }
    }

    private static AutomationElement[] SafeChildren(AutomationElement element)
    {
        try
        {
            return element.FindAllChildren();
        }
        catch
        {
            return [];
        }
    }

    private static UiWindowInfo MapWindow(AutomationElement element)
    {
        var processId = SafeInt(() => element.Properties.ProcessId.ValueOrDefault);

        return new UiWindowInfo
        {
            Title = SafeString(() => element.Name),
            ProcessId = processId,
            ProcessName = GetProcessName(processId),
            AutomationId = SafeString(() => element.AutomationId),
            ControlType = SafeControlType(element),
            IsVisible = !SafeBool(() => element.IsOffscreen, true),
            IsMinimized = SafeIsMinimized(element),
            IsEnabled = SafeBool(() => element.IsEnabled, false),
            IsOffscreen = SafeBool(() => element.IsOffscreen, true)
        };
    }

    private static UiElementInfo MapElement(AutomationElement element, int index)
    {
        return new UiElementInfo
        {
            Index = index,
            Name = SafeString(() => element.Name),
            AutomationId = SafeString(() => element.AutomationId),
            ControlType = SafeControlType(element),
            IsEnabled = SafeBool(() => element.IsEnabled, false),
            IsOffscreen = SafeBool(() => element.IsOffscreen, true),
            IsClickable = TryGetClickablePoint(element)
        };
    }

    private static string FormatWindows(IEnumerable<AutomationElement> windows)
    {
        var titles = windows
            .Select(window => SafeString(() => window.Name))
            .Select(title => string.IsNullOrWhiteSpace(title) ? "<empty title>" : title)
            .ToArray();

        return titles.Length == 0 ? "<none>" : string.Join(" | ", titles);
    }

    private static FlaUiControlType ParseControlType(string value)
    {
        return Enum.TryParse<FlaUiControlType>(value, ignoreCase: true, out var parsed)
            ? parsed
            : FlaUiControlType.Button;
    }

    private static bool IsVisibleEnabledClickable(AutomationElement element)
    {
        return IsVisibleAndEnabled(element) && TryGetClickablePoint(element);
    }

    private static bool IsVisibleAndEnabled(AutomationElement element)
    {
        return SafeBool(() => element.IsEnabled, false) &&
               !SafeBool(() => element.IsOffscreen, true);
    }

    private static bool TryGetClickablePoint(AutomationElement element)
    {
        try
        {
            return element.TryGetClickablePoint(out _);
        }
        catch
        {
            return false;
        }
    }

    private static bool? SafeIsMinimized(AutomationElement element)
    {
        try
        {
            var window = element.AsWindow();
            var pattern = window.Patterns.Window.PatternOrDefault;
            return pattern?.WindowVisualState.Value == FlaUI.Core.Definitions.WindowVisualState.Minimized;
        }
        catch
        {
            return null;
        }
    }

    private static string GetProcessName(int? processId)
    {
        if (!processId.HasValue || processId <= 0)
        {
            return string.Empty;
        }

        try
        {
            return System.Diagnostics.Process.GetProcessById(processId.Value).ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static TimeSpan GetNextDelay(TimeSpan timeout, TimeSpan elapsed)
    {
        var remaining = timeout - elapsed;
        return remaining <= PollInterval ? remaining : PollInterval;
    }

    private static string SafeControlType(AutomationElement element)
    {
        try
        {
            return element.ControlType.ToString();
        }
        catch
        {
            return "<unknown>";
        }
    }

    private static string SafeString(Func<string?> read)
    {
        try
        {
            return read() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool SafeBool(Func<bool> read, bool defaultValue)
    {
        try
        {
            return read();
        }
        catch
        {
            return defaultValue;
        }
    }

    private static int? SafeInt(Func<int> read)
    {
        try
        {
            var value = read();
            return value <= 0 ? null : value;
        }
        catch
        {
            return null;
        }
    }
}
