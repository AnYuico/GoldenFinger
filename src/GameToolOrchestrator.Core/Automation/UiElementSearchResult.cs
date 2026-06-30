using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Automation;

public sealed class UiElementSearchResult
{
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    public UiWindowInfo? Window { get; set; }

    public List<UiElementInfo> Elements { get; set; } = [];

    public string? ErrorMessage { get; set; }

    public string VisibleWindowsSummary { get; set; } = string.Empty;

    public string ControlTreeSummary { get; set; } = string.Empty;
}
