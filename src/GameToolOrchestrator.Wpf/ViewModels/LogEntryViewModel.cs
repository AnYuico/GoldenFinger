namespace GameToolOrchestrator.Wpf.ViewModels;

public sealed class LogEntryViewModel
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public string Level { get; init; } = "Information";

    public string StepId { get; init; } = string.Empty;

    public string ToolId { get; init; } = string.Empty;

    public string ActionId { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public override string ToString()
    {
        var scope = string.Join(
            "/",
            new[] { ToolId, StepId, ActionId }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return scope.Length == 0
            ? $"{Timestamp:HH:mm:ss} [{Level}] {Message}"
            : $"{Timestamp:HH:mm:ss} [{Level}] {scope} {Message}";
    }
}
