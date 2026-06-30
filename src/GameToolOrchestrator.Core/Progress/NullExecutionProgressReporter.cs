using GameToolOrchestrator.Core.Abstractions;

namespace GameToolOrchestrator.Core.Progress;

public sealed class NullExecutionProgressReporter : IExecutionProgressReporter
{
    public static NullExecutionProgressReporter Instance { get; } = new();

    private NullExecutionProgressReporter()
    {
    }

    public void Report(ExecutionProgressEvent progressEvent)
    {
    }
}
