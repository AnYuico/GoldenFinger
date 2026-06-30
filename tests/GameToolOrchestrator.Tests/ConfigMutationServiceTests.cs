using GameToolOrchestrator.Core.Configuration;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Tests;

public sealed class ConfigMutationServiceTests
{
    private readonly ConfigMutationService _service = new();

    [Fact]
    public void AddTool_GeneratesUniqueIdAndDefaults()
    {
        var config = CreateConfig();

        var tool = _service.AddTool(config);

        Assert.Equal("tool-1", tool.Id);
        Assert.Equal("New Tool", tool.Name);
        Assert.Equal("generic-ui", tool.Type);
        Assert.Equal(string.Empty, tool.ExecutablePath);
        Assert.Equal(string.Empty, tool.WorkingDirectory);
        Assert.Equal(30, tool.LaunchTimeoutSeconds);
        Assert.Equal(180, tool.TaskTimeoutMinutes);
        Assert.Contains(tool, config.Tools);
    }

    [Fact]
    public void CopyTool_GeneratesUniqueIdAndDeepClonesMutableProperties()
    {
        var config = CreateConfig();
        config.Tools[0].Window = new WindowSelector { Title = "Original" };

        var copy = _service.CopyTool(config, config.Tools[0]);

        Assert.Equal("bettergi-copy", copy.Id);
        Assert.Equal("BetterGI Copy", copy.Name);
        Assert.NotSame(config.Tools[0].Window, copy.Window);
        copy.Window!.Title = "Changed";
        Assert.Equal("Original", config.Tools[0].Window!.Title);
    }

    [Fact]
    public void DeleteTool_WhenReferenced_IsBlocked()
    {
        var config = CreateConfig();

        var result = _service.DeleteTool(config, config.Tools[0]);

        Assert.False(result.Succeeded);
        Assert.Contains("daily/run-bettergi", result.ErrorMessage);
        Assert.Contains(config.Tools[0], config.Tools);
    }

    [Fact]
    public void DeleteTool_WhenUnreferenced_RemovesTool()
    {
        var config = CreateConfig();
        var unused = new ToolDefinition { Id = "unused", Name = "Unused" };
        config.Tools.Add(unused);

        var result = _service.DeleteTool(config, unused);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(unused, config.Tools);
    }

    [Fact]
    public void AddTaskPlan_GeneratesUniqueIdAndDefaults()
    {
        var config = CreateConfig();

        var plan = _service.AddTaskPlan(config);

        Assert.Equal("plan-1", plan.Id);
        Assert.Equal("New Task Plan", plan.Name);
        Assert.True(plan.Execution!.StopOnFailure);
        Assert.Empty(plan.Steps);
        Assert.Contains(plan, config.TaskPlans);
    }

    [Fact]
    public void CopyTaskPlan_DeepClonesSteps()
    {
        var config = CreateConfig();

        var copy = _service.CopyTaskPlan(config, config.TaskPlans[0]);

        Assert.Equal("daily-copy", copy.Id);
        Assert.Equal("Daily Copy", copy.Name);
        Assert.NotSame(config.TaskPlans[0].Steps[0], copy.Steps[0]);
        copy.Steps[0].ToolId = "changed";
        Assert.Equal("bettergi", config.TaskPlans[0].Steps[0].ToolId);
    }

    [Fact]
    public void DeleteTaskPlan_RemovesPlan()
    {
        var config = CreateConfig();
        var plan = config.TaskPlans[0];

        var result = _service.DeleteTaskPlan(config, plan);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(plan, config.TaskPlans);
    }

    [Fact]
    public void AddStep_UsesSelectedToolAndNextOrder()
    {
        var config = CreateConfig();
        var plan = config.TaskPlans[0];

        var step = _service.AddStep(config, plan, config.Tools[1]);

        Assert.NotNull(step);
        Assert.Equal("step-1", step!.Id);
        Assert.Equal("tool-b", step.ToolId);
        Assert.Equal(3, step.Order);
        Assert.True(step.Enabled);
    }

    [Fact]
    public void AddStep_WhenToolIsNull_ReturnsNull()
    {
        var config = CreateConfig();

        var step = _service.AddStep(config, config.TaskPlans[0], tool: null);

        Assert.Null(step);
    }

    [Fact]
    public void CopyStep_DeepClonesAndUsesNextOrder()
    {
        var config = CreateConfig();
        var plan = config.TaskPlans[0];

        var copy = _service.CopyStep(plan, plan.Steps[0]);

        Assert.Equal("run-bettergi-copy", copy.Id);
        Assert.Equal(3, copy.Order);
        Assert.NotSame(plan.Steps[0].Actions[0], copy.Actions[0]);
        copy.Actions[0].NameContains = "Changed";
        Assert.NotEqual("Changed", plan.Steps[0].Actions[0].NameContains);
    }

    [Fact]
    public void DeleteStep_RemovesStep()
    {
        var config = CreateConfig();
        var plan = config.TaskPlans[0];
        var step = plan.Steps[0];

        var result = _service.DeleteStep(plan, step);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(step, plan.Steps);
    }

    [Fact]
    public void NormalizeStepOrder_SortsAndRenumbers()
    {
        var config = CreateConfig();
        var plan = config.TaskPlans[0];
        plan.Steps[0].Order = 10;
        plan.Steps[1].Order = 5;

        _service.NormalizeStepOrder(plan);

        Assert.Collection(
            plan.Steps,
            step =>
            {
                Assert.Equal("second", step.Id);
                Assert.Equal(1, step.Order);
            },
            step =>
            {
                Assert.Equal("run-bettergi", step.Id);
                Assert.Equal(2, step.Order);
            });
    }

    [Theory]
    [InlineData("waitSeconds", "wait-seconds-1", "1", null, null, null, null)]
    [InlineData("waitProcessExit", "wait-process-exit-1", null, 180, null, null, null)]
    [InlineData("waitWindow", "wait-window-1", null, null, 30, "", null)]
    [InlineData("clickButton", "click-button-1", null, null, 10, null, "Button")]
    public void AddAction_UsesExpectedDefaults(
        string actionType,
        string expectedId,
        string? expectedSeconds,
        int? expectedTimeoutMinutes,
        int? expectedTimeoutSeconds,
        string? expectedTitleContains,
        string? expectedControlType)
    {
        var step = new TaskStep();

        var action = _service.AddAction(step, actionType);

        Assert.NotNull(action);
        Assert.Equal(expectedId, action!.Id);
        Assert.Equal(actionType, action.Type);
        Assert.True(action.Enabled);
        Assert.Equal(1, action.Order);
        if (expectedSeconds is not null)
        {
            Assert.Equal(expectedSeconds, action.Parameters["seconds"]);
        }

        Assert.Equal(expectedTimeoutMinutes, action.TimeoutMinutes);
        Assert.Equal(expectedTimeoutSeconds, action.TimeoutSeconds);
        Assert.Equal(expectedTitleContains ?? string.Empty, action.TitleContains);
        Assert.Equal(expectedControlType ?? string.Empty, action.ControlType);
    }

    [Fact]
    public void CopyAction_DeepClonesAndAppendsToEnd()
    {
        var config = CreateConfig();
        var step = config.TaskPlans[0].Steps[0];

        var copy = _service.CopyAction(step, step.Actions[0]);

        Assert.Equal("wait-window-copy", copy.Id);
        Assert.Equal(3, copy.Order);
        Assert.NotSame(step.Actions[0].Parameters, copy.Parameters);
        copy.Parameters["new"] = "value";
        Assert.False(step.Actions[0].Parameters.ContainsKey("new"));
    }

    [Fact]
    public void DeleteAction_RemovesAction()
    {
        var config = CreateConfig();
        var step = config.TaskPlans[0].Steps[0];
        var action = step.Actions[0];

        var result = _service.DeleteAction(step, action);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(action, step.Actions);
    }

    private static OrchestratorConfig CreateConfig()
    {
        return new OrchestratorConfig
        {
            Tools =
            [
                new ToolDefinition
                {
                    Id = "bettergi",
                    Name = "BetterGI",
                    ExecutablePath = "D:\\Tools\\BetterGI.exe"
                },
                new ToolDefinition
                {
                    Id = "tool-b",
                    Name = "Tool B"
                }
            ],
            TaskPlans =
            [
                new TaskPlan
                {
                    Id = "daily",
                    Name = "Daily",
                    Steps =
                    [
                        new TaskStep
                        {
                            Id = "run-bettergi",
                            ToolId = "bettergi",
                            Order = 1,
                            Actions =
                            [
                                new AutomationActionDefinition
                                {
                                    Id = "wait-window",
                                    Type = "waitWindow",
                                    Order = 1,
                                    TitleContains = "BetterGI",
                                    Parameters = new Dictionary<string, string> { ["sample"] = "value" }
                                },
                                new AutomationActionDefinition
                                {
                                    Id = "click-start",
                                    Type = "clickButton",
                                    Order = 2,
                                    NameContains = "Start"
                                }
                            ]
                        },
                        new TaskStep
                        {
                            Id = "second",
                            ToolId = "tool-b",
                            Order = 2
                        }
                    ]
                }
            ]
        };
    }
}
