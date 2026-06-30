using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Actions;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Tests;

public sealed class ActionExecutorTests
{
    [Fact]
    public async Task WaitSeconds_SupportsCancellation()
    {
        var executor = new WaitSecondsActionExecutor();
        var action = new AutomationActionDefinition
        {
            Id = "wait",
            Type = WaitSecondsActionExecutor.TypeName,
            Parameters = new Dictionary<string, string>
            {
                ["seconds"] = "30"
            }
        };

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        var result = await executor.ExecuteAsync(action, CreateContext(), cancellation.Token);

        Assert.Equal(ExecutionStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task WaitProcessExit_ReturnsTimedOutWhenProcessDoesNotExit()
    {
        var process = new FakeProcessHandle { NeverExits = true };
        var executor = new WaitProcessExitActionExecutor();
        var action = new AutomationActionDefinition
        {
            Id = "wait-exit",
            Type = WaitProcessExitActionExecutor.TypeName,
            Parameters = new Dictionary<string, string>
            {
                ["timeoutMilliseconds"] = "20"
            }
        };

        var result = await executor.ExecuteAsync(action, CreateContext(process), CancellationToken.None);

        Assert.Equal(ExecutionStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task WaitProcessExit_DoesNotKillOnTimeoutByDefault()
    {
        var process = new FakeProcessHandle { NeverExits = true };
        var executor = new WaitProcessExitActionExecutor();
        var action = new AutomationActionDefinition
        {
            Id = "wait-exit",
            Type = WaitProcessExitActionExecutor.TypeName,
            Parameters = new Dictionary<string, string>
            {
                ["timeoutMilliseconds"] = "20"
            }
        };

        var result = await executor.ExecuteAsync(action, CreateContext(process), CancellationToken.None);

        Assert.Equal(ExecutionStatus.TimedOut, result.Status);
        Assert.False(process.Killed);
        Assert.Equal("False", result.Outputs["wasKilled"]);
    }

    private static ActionExecutionContext CreateContext(IProcessHandle? process = null)
    {
        var config = new OrchestratorConfig();
        var plan = new TaskPlan { Id = "plan", Name = "Plan" };
        var step = new TaskStep { Id = "step", ToolId = "tool" };
        var tool = new ToolDefinition { Id = "tool", ExecutablePath = "tool.exe" };

        return new ActionExecutionContext
        {
            Config = config,
            Plan = plan,
            Step = step,
            Tool = tool,
            ExecutionOptions = new ExecutionOptions(),
            Process = process ?? new FakeProcessHandle(),
            Logger = new InMemoryExecutionLogger()
        };
    }
}
