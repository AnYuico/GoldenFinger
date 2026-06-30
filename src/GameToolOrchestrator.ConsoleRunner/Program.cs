using System.Text;
using GameToolOrchestrator.ConsoleRunner;
using GameToolOrchestrator.Core.Engine;
using GameToolOrchestrator.Core.Models;
using GameToolOrchestrator.Infrastructure.Automation;
using GameToolOrchestrator.Infrastructure.Configuration;
using GameToolOrchestrator.Infrastructure.Logging;
using GameToolOrchestrator.Infrastructure.Process;

Console.OutputEncoding = Encoding.UTF8;

var command = RunnerCommandParser.Parse(args);
if (command is null)
{
    PrintUsage();
    return 64;
}

try
{
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
        Console.WriteLine("Cancellation requested. Waiting for the current operation to stop...");
    };

    var repository = new JsonConfigRepository();
    var automationService = new FlaUiAutomationService();

    if (command.Type == RunnerCommandType.RunPlan)
    {
        var config = await repository.LoadAsync(command.ConfigPath);
        using var logger = new SerilogExecutionLogger(config.Execution.LogDirectory);

        var engine = new ExecutionEngine(
            new DefaultProcessLauncher(),
            InfrastructureActionExecutors.CreateDefaultFactory(automationService),
            logger);

        var result = await engine.ExecuteAsync(config, command.TaskPlanId, cancellation.Token);
        PrintResult(result);
        return ToExitCode(result.Status);
    }

    using (var logger = new SerilogExecutionLogger("logs"))
    {
        var diagnostics = new DiagnosticCommandRunner(
            automationService,
            repository,
            InfrastructureActionExecutors.CreateDefaultFactory(automationService),
            logger);

        var result = await diagnostics.ExecuteAsync(command, cancellation.Token);
        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            Console.Write(result.Output);
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            Console.Error.WriteLine(result.Error);
        }

        return result.ExitCode;
    }
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Execution cancelled.");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  GameToolOrchestrator.ConsoleRunner <config.json> <taskPlanId>");
    Console.WriteLine("  GameToolOrchestrator.ConsoleRunner --config <config.json> --plan <taskPlanId>");
    Console.WriteLine("  GameToolOrchestrator.ConsoleRunner inspect-windows");
    Console.WriteLine("  GameToolOrchestrator.ConsoleRunner inspect-window --title-contains <text> [--max-depth 4]");
    Console.WriteLine("  GameToolOrchestrator.ConsoleRunner test-selector --window-title-contains <text> [--automation-id id | --name-equals text | --name-contains text] [--control-type Button] [--timeout-seconds 10] [--click]");
    Console.WriteLine("  GameToolOrchestrator.ConsoleRunner run-action --config <config.json> --tool <toolId> --action-index <zero-based-index>");
}

static void PrintResult(ExecutionResult result)
{
    Console.WriteLine($"Run {result.RunId}: {result.Status}");

    foreach (var task in result.Tasks)
    {
        Console.WriteLine($"TaskPlan {task.TaskPlanId}: {task.Status}");

        foreach (var step in task.Steps)
        {
            Console.WriteLine($"  [{step.Status}] {step.StepId} tool={step.ToolId} process={step.ProcessId?.ToString() ?? "-"}");

            foreach (var action in step.Actions)
            {
                Console.WriteLine($"    [{action.Status}] {action.ActionId} type={action.ActionType}");
            }
        }
    }
}

static int ToExitCode(ExecutionStatus status)
{
    return status switch
    {
        ExecutionStatus.Succeeded => 0,
        ExecutionStatus.Skipped => 0,
        ExecutionStatus.Cancelled => 2,
        ExecutionStatus.TimedOut => 3,
        _ => 1
    };
}
