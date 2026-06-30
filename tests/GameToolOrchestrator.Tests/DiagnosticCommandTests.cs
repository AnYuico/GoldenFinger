using GameToolOrchestrator.ConsoleRunner;
using GameToolOrchestrator.Core.Automation;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Tests;

public sealed class DiagnosticCommandTests
{
    [Fact]
    public void Parser_ParsesInspectWindows()
    {
        var command = RunnerCommandParser.Parse(["inspect-windows"]);

        Assert.NotNull(command);
        Assert.Equal(RunnerCommandType.InspectWindows, command.Type);
    }

    [Fact]
    public async Task InspectWindow_WhenFound_PrintsMatchedWindowAndControlTree()
    {
        var automation = new FakeUiAutomationService
        {
            MatchingWindows =
            [
                new UiWindowInfo
                {
                    Title = "BetterGI",
                    ProcessName = "BetterGI",
                    ProcessId = 123,
                    IsVisible = true,
                    IsEnabled = true
                }
            ],
            ControlTreeSummary = "- Name='Start', AutomationId='StartButton', ControlType='Button', IsEnabled=True, IsOffscreen=False"
        };
        var runner = CreateRunner(automation);
        var command = new RunnerCommand
        {
            Type = RunnerCommandType.InspectWindow,
            WindowCriteria = { TitleContains = "BetterGI" },
            MaxDepth = 4
        };

        var result = await runner.ExecuteAsync(command, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Matched windows: 1", result.Output);
        Assert.Contains("Title='BetterGI'", result.Output);
        Assert.Contains("StartButton", result.Output);
    }

    [Fact]
    public async Task InspectWindow_WhenNotFound_PrintsVisibleWindows()
    {
        var automation = new FakeUiAutomationService
        {
            MatchingWindows = [],
            VisibleWindows =
            [
                new UiWindowInfo { Title = "Notepad", ProcessName = "notepad", ProcessId = 456, IsVisible = true }
            ]
        };
        var runner = CreateRunner(automation);
        var command = new RunnerCommand
        {
            Type = RunnerCommandType.InspectWindow,
            WindowCriteria = { TitleContains = "BetterGI" }
        };

        var result = await runner.ExecuteAsync(command, CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No window matched", result.Output);
        Assert.Contains("Notepad", result.Output);
    }

    [Fact]
    public async Task TestSelector_DoesNotClickByDefault()
    {
        var automation = new FakeUiAutomationService
        {
            ElementSearchResult = CreateElementSearchResult("开始执行")
        };
        var runner = CreateRunner(automation);
        var command = new RunnerCommand
        {
            Type = RunnerCommandType.TestSelector,
            ButtonCriteria =
            {
                WindowTitleContains = "BetterGI",
                NameContains = "开始",
                ControlType = "Button"
            },
            TimeoutSeconds = 1
        };

        var result = await runner.ExecuteAsync(command, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, automation.ClickCount);
        Assert.Contains("Click: false", result.Output);
    }

    [Fact]
    public async Task TestSelector_ClicksOnlyWhenClickFlagIsProvided()
    {
        var automation = new FakeUiAutomationService
        {
            ElementSearchResult = CreateElementSearchResult("开始执行"),
            ClickResult = UiClickResult.Succeeded(
                new UiWindowInfo { Title = "BetterGI" },
                new UiElementInfo { Index = 0, Name = "开始执行", ControlType = "Button", IsEnabled = true })
        };
        var runner = CreateRunner(automation);
        var command = new RunnerCommand
        {
            Type = RunnerCommandType.TestSelector,
            ButtonCriteria =
            {
                WindowTitleContains = "BetterGI",
                NameContains = "开始",
                ControlType = "Button"
            },
            TimeoutSeconds = 1,
            Click = true
        };

        var result = await runner.ExecuteAsync(command, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, automation.ClickCount);
        Assert.Contains("About to click control", result.Output);
        Assert.Contains("Click result: Succeeded", result.Output);
    }

    [Fact]
    public async Task RunAction_LocatesConfiguredActionByZeroBasedIndex()
    {
        var actionExecutor = new FakeActionExecutor();
        var repository = new InMemoryConfigRepository
        {
            Config = new OrchestratorConfig
            {
                Tools = [new ToolDefinition { Id = "bettergi", ExecutablePath = "BetterGI.exe" }],
                TaskPlans =
                [
                    new TaskPlan
                    {
                        Id = "plan",
                        Steps =
                        [
                            new TaskStep
                            {
                                Id = "step",
                                ToolId = "bettergi",
                                Actions =
                                [
                                    new AutomationActionDefinition { Id = "wait-window", Type = "fakeAction", Order = 1 },
                                    new AutomationActionDefinition { Id = "click-start", Type = "fakeAction", Order = 2 }
                                ]
                            }
                        ]
                    }
                ]
            }
        };
        var runner = CreateRunner(
            new FakeUiAutomationService(),
            repository,
            new FakeActionExecutorFactory(actionExecutor));
        var command = new RunnerCommand
        {
            Type = RunnerCommandType.RunAction,
            ConfigPath = "config.json",
            ToolId = "bettergi",
            ActionIndex = 1
        };

        var result = await runner.ExecuteAsync(command, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("click-start", actionExecutor.LastAction?.Id);
        Assert.Contains("Status: Succeeded", result.Output);
    }

    [Fact]
    public async Task TestSelector_PassesChineseSelector()
    {
        var automation = new FakeUiAutomationService
        {
            ElementSearchResult = CreateElementSearchResult("开始执行")
        };
        var runner = CreateRunner(automation);
        var command = RunnerCommandParser.Parse(
            ["test-selector", "--window-title-contains", "BetterGI", "--name-contains", "开始"]);

        Assert.NotNull(command);
        var result = await runner.ExecuteAsync(command, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("开始", automation.LastButtonCriteria?.NameContains);
        Assert.Contains("开始执行", result.Output);
    }

    private static DiagnosticCommandRunner CreateRunner(
        FakeUiAutomationService automation,
        InMemoryConfigRepository? repository = null,
        FakeActionExecutorFactory? factory = null)
    {
        var actionExecutor = new FakeActionExecutor();
        return new DiagnosticCommandRunner(
            automation,
            repository ?? new InMemoryConfigRepository(),
            factory ?? new FakeActionExecutorFactory(actionExecutor),
            new InMemoryExecutionLogger());
    }

    private static UiElementSearchResult CreateElementSearchResult(string name)
    {
        return new UiElementSearchResult
        {
            Status = ExecutionStatus.Succeeded,
            Window = new UiWindowInfo { Title = "BetterGI", ProcessName = "BetterGI", ProcessId = 123, IsVisible = true },
            Elements =
            [
                new UiElementInfo
                {
                    Index = 0,
                    Name = name,
                    AutomationId = "StartButton",
                    ControlType = "Button",
                    IsEnabled = true,
                    IsOffscreen = false,
                    IsClickable = true
                }
            ],
            ControlTreeSummary = "- Name='开始执行', AutomationId='StartButton', ControlType='Button', IsEnabled=True, IsOffscreen=False"
        };
    }
}
