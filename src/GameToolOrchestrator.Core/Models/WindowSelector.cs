namespace GameToolOrchestrator.Core.Models;

public sealed class WindowSelector
{
    public string Title { get; set; } = string.Empty;

    public string TitleMatchMode { get; set; } = "contains";

    public string ClassName { get; set; } = string.Empty;

    public string AutomationId { get; set; } = string.Empty;

    public string ProcessName { get; set; } = string.Empty;

    public int WaitTimeoutSeconds { get; set; } = 60;
}
