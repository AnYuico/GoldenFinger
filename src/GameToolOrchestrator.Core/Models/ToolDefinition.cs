namespace GameToolOrchestrator.Core.Models;

public sealed class ToolDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "generic-ui";

    public string ExecutablePath { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public bool RequiresAdministrator { get; set; } = false;

    public int? LaunchTimeoutSeconds { get; set; }

    public int? TaskTimeoutMinutes { get; set; }

    public WindowSelector? Window { get; set; }
}
