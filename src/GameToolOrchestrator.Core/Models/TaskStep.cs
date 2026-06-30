namespace GameToolOrchestrator.Core.Models;

public sealed class TaskStep
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ToolId { get; set; } = string.Empty;

    public int Order { get; set; }

    public bool Enabled { get; set; } = true;

    public CompletionStrategy CompletionStrategy { get; set; } = CompletionStrategy.ProcessExit;

    public bool? WaitForExit { get; set; }

    public int? TimeoutSeconds { get; set; }

    public int? TimeoutMinutes { get; set; }

    public List<AutomationActionDefinition> Actions { get; set; } = [];
}
