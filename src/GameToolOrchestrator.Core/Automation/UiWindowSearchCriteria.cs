namespace GameToolOrchestrator.Core.Automation;

public sealed class UiWindowSearchCriteria
{
    public string TitleEquals { get; set; } = string.Empty;

    public string TitleContains { get; set; } = string.Empty;

    public bool HasAnyTitleMatcher =>
        !string.IsNullOrWhiteSpace(TitleEquals) ||
        !string.IsNullOrWhiteSpace(TitleContains);

    public override string ToString()
    {
        if (!string.IsNullOrWhiteSpace(TitleEquals))
        {
            return $"titleEquals='{TitleEquals}'";
        }

        if (!string.IsNullOrWhiteSpace(TitleContains))
        {
            return $"titleContains='{TitleContains}'";
        }

        return "any visible window";
    }
}
