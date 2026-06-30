namespace GameToolOrchestrator.Wpf.Services;

public sealed class DefaultConfigSearchResult
{
    public string? FoundPath { get; init; }

    public List<string> CandidatePaths { get; init; } = [];
}
