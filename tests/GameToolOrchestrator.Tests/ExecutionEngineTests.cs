using GameToolOrchestrator.Core.Actions;
using GameToolOrchestrator.Core.Engine;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Tests;

public sealed class ExecutionEngineTests
{
    [Fact]
    public async Task ExecuteAsync_SkipsDisabledSteps()
    {
        var launcher = new FakeProcessLauncher();
        var logger = new InMemoryExecutionLogger();
        var engine = CreateEngine(launcher, logger);
        var config = CreateConfig(
            new TaskStep
            {
                Id = "disabled",
                ToolId = "missing",
                Enabled = false,
                Order = 1
            });

        var result = await engine.ExecuteAsync(config, "plan");

        Assert.Equal(ExecutionStatus.Succeeded, result.Status);
        Assert.Equal(ExecutionStatus.Skipped, result.Tasks[0].Steps[0].Status);
        Assert.Empty(launcher.LaunchedToolIds);
    }

    [Fact]
    public async Task ExecuteAsync_RunsStepsInOrder()
    {
        var launcher = new FakeProcessLauncher();
        var engine = CreateEngine(launcher);
        var config = CreateConfig(
            new TaskStep { Id = "second", ToolId = "tool-b", Order = 20, WaitForExit = false },
            new TaskStep { Id = "first", ToolId = "tool-a", Order = 10, WaitForExit = false });
        config.Tools.Add(new ToolDefinition { Id = "tool-b", ExecutablePath = "b.exe" });

        var result = await engine.ExecuteAsync(config, "plan");

        Assert.Equal(ExecutionStatus.Succeeded, result.Status);
        Assert.Equal(["tool-a", "tool-b"], launcher.LaunchedToolIds);
        Assert.Equal(["first", "second"], result.Tasks[0].Steps.Select(step => step.StepId).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_FailsWhenToolIdIsMissing()
    {
        var logger = new InMemoryExecutionLogger();
        var engine = CreateEngine(new FakeProcessLauncher(), logger);
        var config = CreateConfig(new TaskStep { Id = "missing-tool", ToolId = "does-not-exist", Order = 1 });

        var result = await engine.ExecuteAsync(config, "plan");

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal(ExecutionStatus.Failed, result.Tasks[0].Steps[0].Status);
        Assert.Contains(logger.StatusChanges, change => change.ScopeId == "missing-tool" && change.Status == ExecutionStatus.Failed);
    }

    [Fact]
    public async Task ExecuteAsync_StopOnFailureTrueStopsAfterFailure()
    {
        var launcher = new FakeProcessLauncher();
        var engine = CreateEngine(launcher);
        var config = CreateConfig(
            new TaskStep { Id = "bad", ToolId = "missing", Order = 1 },
            new TaskStep { Id = "good", ToolId = "tool-a", Order = 2, WaitForExit = false });
        config.Execution.StopOnFailure = true;

        var result = await engine.ExecuteAsync(config, "plan");

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Single(result.Tasks[0].Steps);
        Assert.Empty(launcher.LaunchedToolIds);
    }

    [Fact]
    public async Task ExecuteAsync_StopOnFailureFalseContinuesAfterFailure()
    {
        var launcher = new FakeProcessLauncher();
        var engine = CreateEngine(launcher);
        var config = CreateConfig(
            new TaskStep { Id = "bad", ToolId = "missing", Order = 1 },
            new TaskStep { Id = "good", ToolId = "tool-a", Order = 2, WaitForExit = false });
        config.Execution.StopOnFailure = false;

        var result = await engine.ExecuteAsync(config, "plan");

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal(2, result.Tasks[0].Steps.Count);
        Assert.Equal(["tool-a"], launcher.LaunchedToolIds);
        Assert.Equal(ExecutionStatus.Succeeded, result.Tasks[0].Steps[1].Status);
    }

    private static ExecutionEngine CreateEngine(
        FakeProcessLauncher launcher,
        InMemoryExecutionLogger? logger = null)
    {
        return new ExecutionEngine(
            launcher,
            DefaultActionExecutorFactory.CreateDefault(),
            logger ?? new InMemoryExecutionLogger());
    }

    private static OrchestratorConfig CreateConfig(params TaskStep[] steps)
    {
        return new OrchestratorConfig
        {
            Execution = new ExecutionOptions
            {
                StopOnFailure = true,
                KillOnTimeout = false,
                WaitForExit = false,
                DefaultActionTimeoutSeconds = 1
            },
            Tools =
            [
                new ToolDefinition
                {
                    Id = "tool-a",
                    Name = "Tool A",
                    ExecutablePath = "a.exe"
                }
            ],
            TaskPlans =
            [
                new TaskPlan
                {
                    Id = "plan",
                    Name = "Plan",
                    Steps = steps.ToList()
                }
            ]
        };
    }
}
