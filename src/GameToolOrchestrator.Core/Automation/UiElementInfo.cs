namespace GameToolOrchestrator.Core.Automation;

public sealed class UiElementInfo
{
    public int Index { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AutomationId { get; set; } = string.Empty;

    public string ControlType { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public bool IsOffscreen { get; set; }

    public bool IsClickable { get; set; }
}
