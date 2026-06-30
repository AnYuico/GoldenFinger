using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Automation;
using GameToolOrchestrator.Core.Models;
using GameToolOrchestrator.Infrastructure.Automation;
using GameToolOrchestrator.Infrastructure.Automation.Actions;

namespace GameToolOrchestrator.Tests;

public sealed class UiAutomationActionTests
{
    [Fact]
    public void Factory_RecognizesWaitWindowAndClickButton()
    {
        var factory = InfrastructureActionExecutors.CreateDefaultFactory(new FakeUiAutomationService());

        Assert.True(factory.TryGetExecutor("waitWindow", out var waitWindow));
        Assert.True(factory.TryGetExecutor("clickButton", out var clickButton));
        Assert.Equal(WaitWindowActionExecutor.TypeName, waitWindow.ActionType);
        Assert.Equal(ClickButtonActionExecutor.TypeName, clickButton.ActionType);
    }

    [Fact]
    public async Task WaitWindow_UsesTitleContainsAndTimeout()
    {
        var automation = new FakeUiAutomationService
        {
            WindowToReturn = new UiWindowInfo { Title = "BetterGI - Running" }
        };
        var executor = new WaitWindowActionExecutor(automation);
        var action = new AutomationActionDefinition
        {
            Id = "wait-window",
            Type = WaitWindowActionExecutor.TypeName,
            TitleContains = "BetterGI",
            TimeoutSeconds = 12
        };

        var result = await executor.ExecuteAsync(action, CreateContext(), CancellationToken.None);

        Assert.Equal(ExecutionStatus.Succeeded, result.Status);
        Assert.Equal("BetterGI", automation.LastWindowCriteria?.TitleContains);
        Assert.Equal(TimeSpan.FromSeconds(12), automation.LastTimeout);
    }

    [Fact]
    public async Task ClickButton_UsesButtonSelectorAndDiagnosticDepth()
    {
        var automation = new FakeUiAutomationService
        {
            ClickResult = UiClickResult.Succeeded(
                new UiWindowInfo { Title = "BetterGI" },
                new UiElementInfo { Name = "开始执行", AutomationId = "StartButton", ControlType = "Button" })
        };
        var executor = new ClickButtonActionExecutor(automation);
        var action = new AutomationActionDefinition
        {
            Id = "click-start",
            Type = ClickButtonActionExecutor.TypeName,
            WindowTitleContains = "BetterGI",
            NameContains = "开始",
            ControlType = "Button",
            TimeoutSeconds = 10
        };

        var result = await executor.ExecuteAsync(action, CreateContext(), CancellationToken.None);

        Assert.Equal(ExecutionStatus.Succeeded, result.Status);
        Assert.Equal("BetterGI", automation.LastButtonCriteria?.WindowTitleContains);
        Assert.Equal("开始", automation.LastButtonCriteria?.NameContains);
        Assert.Equal("Button", automation.LastButtonCriteria?.ControlType);
        Assert.Equal(TimeSpan.FromSeconds(10), automation.LastTimeout);
        Assert.Equal(4, automation.LastDiagnosticMaxDepth);
    }

    private static ActionExecutionContext CreateContext()
    {
        var config = new OrchestratorConfig();
        var plan = new TaskPlan { Id = "plan", Name = "Plan" };
        var step = new TaskStep { Id = "step", ToolId = "bettergi" };
        var tool = new ToolDefinition
        {
            Id = "bettergi",
            Name = "BetterGI",
            ExecutablePath = "D:\\Tools\\BetterGI\\BetterGI.exe",
            Window = new WindowSelector
            {
                Title = "BetterGI",
                TitleMatchMode = "contains"
            }
        };

        return new ActionExecutionContext
        {
            Config = config,
            Plan = plan,
            Step = step,
            Tool = tool,
            ExecutionOptions = new ExecutionOptions(),
            Process = new FakeProcessHandle(),
            Logger = new InMemoryExecutionLogger()
        };
    }
}
