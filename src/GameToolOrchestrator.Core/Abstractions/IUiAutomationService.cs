using GameToolOrchestrator.Core.Automation;

namespace GameToolOrchestrator.Core.Abstractions;

public interface IUiAutomationService
{
    Task<IReadOnlyList<UiWindowInfo>> GetVisibleWindowsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<UiWindowInfo>> FindWindowsAsync(
        UiWindowSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<UiWindowInfo?> WaitForWindowAsync(
        UiWindowSearchCriteria criteria,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    Task<UiElementSearchResult> FindElementsAsync(
        UiButtonSearchCriteria criteria,
        int diagnosticMaxDepth,
        CancellationToken cancellationToken);

    Task<UiClickResult> ClickButtonAsync(
        UiButtonSearchCriteria criteria,
        TimeSpan timeout,
        int diagnosticMaxDepth,
        CancellationToken cancellationToken);

    Task<string> GetControlTreeSummaryAsync(
        UiWindowSearchCriteria criteria,
        int maxDepth,
        CancellationToken cancellationToken);
}
