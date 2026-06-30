namespace GameToolOrchestrator.Core.Automation;

public sealed class UiButtonSearchCriteria
{
    public string WindowTitleEquals { get; set; } = string.Empty;

    public string WindowTitleContains { get; set; } = string.Empty;

    public string AutomationId { get; set; } = string.Empty;

    public string NameEquals { get; set; } = string.Empty;

    public string NameContains { get; set; } = string.Empty;

    public string ControlType { get; set; } = "Button";

    public UiWindowSearchCriteria ToWindowCriteria()
    {
        return new UiWindowSearchCriteria
        {
            TitleEquals = WindowTitleEquals,
            TitleContains = WindowTitleContains
        };
    }

    public override string ToString()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(WindowTitleEquals))
        {
            parts.Add($"windowTitleEquals='{WindowTitleEquals}'");
        }

        if (!string.IsNullOrWhiteSpace(WindowTitleContains))
        {
            parts.Add($"windowTitleContains='{WindowTitleContains}'");
        }

        if (!string.IsNullOrWhiteSpace(AutomationId))
        {
            parts.Add($"automationId='{AutomationId}'");
        }

        if (!string.IsNullOrWhiteSpace(NameEquals))
        {
            parts.Add($"nameEquals='{NameEquals}'");
        }

        if (!string.IsNullOrWhiteSpace(NameContains))
        {
            parts.Add($"nameContains='{NameContains}'");
        }

        parts.Add($"controlType='{ControlType}'");
        return string.Join(", ", parts);
    }
}
