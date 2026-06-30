namespace GameToolOrchestrator.Core.Automation;

public sealed class UiWindowInfo
{
    public string Title { get; set; } = string.Empty;

    public int? ProcessId { get; set; }

    public string ProcessName { get; set; } = string.Empty;

    public string AutomationId { get; set; } = string.Empty;

    public string ControlType { get; set; } = "Window";

    public bool IsVisible { get; set; } = true;

    public bool? IsMinimized { get; set; }

    public bool IsEnabled { get; set; }

    public bool IsOffscreen { get; set; }
}
