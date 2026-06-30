namespace GameToolOrchestrator.Core.Models;

public sealed class AutomationActionDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public bool Enabled { get; set; } = true;

    public int? TimeoutSeconds { get; set; }

    public int? TimeoutMinutes { get; set; }

    public bool ContinueOnError { get; set; } = false;

    public string TitleEquals { get; set; } = string.Empty;

    public string TitleContains { get; set; } = string.Empty;

    public string WindowTitleEquals { get; set; } = string.Empty;

    public string WindowTitleContains { get; set; } = string.Empty;

    public string AutomationId { get; set; } = string.Empty;

    public string NameEquals { get; set; } = string.Empty;

    public string NameContains { get; set; } = string.Empty;

    public string ControlType { get; set; } = string.Empty;

    public Dictionary<string, string> Parameters { get; set; } = [];
}
