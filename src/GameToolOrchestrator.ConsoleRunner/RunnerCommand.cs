using GameToolOrchestrator.Core.Automation;

namespace GameToolOrchestrator.ConsoleRunner;

public enum RunnerCommandType
{
    RunPlan,
    InspectWindows,
    InspectWindow,
    TestSelector,
    RunAction
}

public sealed class RunnerCommand
{
    public RunnerCommandType Type { get; set; }

    public string ConfigPath { get; set; } = string.Empty;

    public string TaskPlanId { get; set; } = string.Empty;

    public string ToolId { get; set; } = string.Empty;

    public int ActionIndex { get; set; }

    public UiWindowSearchCriteria WindowCriteria { get; set; } = new();

    public UiButtonSearchCriteria ButtonCriteria { get; set; } = new();

    public int MaxDepth { get; set; } = 4;

    public int TimeoutSeconds { get; set; } = 10;

    public bool Click { get; set; }
}
