using GameToolOrchestrator.Core.Actions;
using GameToolOrchestrator.Core.Engine;
using GameToolOrchestrator.Core.Models;
using GameToolOrchestrator.Core.Progress;

namespace GameToolOrchestrator.Tests;

public sealed class ExecutionProgressTests
{
    [Fact]
    public async Task ExecuteAsync_ReportsStepAndActionProgress()
    {
        var reporter = new RecordingProgressReporter();
        var engine = new ExecutionEngine(
            new FakeProcessLauncher(),
            DefaultActionExecutorFactory.CreateDefault(),
            new InMemoryExecutionLogger(),
            reporter);
        var config = new OrchestratorConfig
        {
            Execution = new ExecutionOptions { WaitForExit = false },
            Tools = [new ToolDefinition { Id = "tool", ExecutablePath = "tool.exe" }],
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
                            ToolId = "tool",
                            Actions =
                            [
                                new AutomationActionDefinition
                                {
                                    Id = "wait",
                                    Type = "waitSeconds",
                                    Parameters = new Dictionary<string, string> { ["milliseconds"] = "0" }
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var result = await engine.ExecuteAsync(config, "plan");

        Assert.Equal(ExecutionStatus.Succeeded, result.Status);
        Assert.Contains(reporter.Events, item => item.EventType == ExecutionProgressEventType.StepStatusChanged && item.StepId == "step");
        Assert.Contains(reporter.Events, item => item.EventType == ExecutionProgressEventType.ActionStatusChanged && item.ActionId == "wait");
    }
}
