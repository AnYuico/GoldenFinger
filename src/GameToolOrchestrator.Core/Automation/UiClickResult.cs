using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Automation;

public sealed class UiClickResult
{
    public ExecutionStatus Status { get; set; }

    public UiWindowInfo? Window { get; set; }

    public UiElementInfo? Element { get; set; }

    public string? ErrorMessage { get; set; }

    public string VisibleWindowsSummary { get; set; } = string.Empty;

    public string ControlTreeSummary { get; set; } = string.Empty;

    public static UiClickResult Succeeded(UiWindowInfo window, UiElementInfo element)
    {
        return new UiClickResult
        {
            Status = ExecutionStatus.Succeeded,
            Window = window,
            Element = element
        };
    }

    public static UiClickResult Failed(ExecutionStatus status, string message)
    {
        return new UiClickResult
        {
            Status = status,
            ErrorMessage = message
        };
    }
}
