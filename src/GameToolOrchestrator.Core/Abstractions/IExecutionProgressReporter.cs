using GameToolOrchestrator.Core.Progress;

namespace GameToolOrchestrator.Core.Abstractions;

public interface IExecutionProgressReporter
{
    void Report(ExecutionProgressEvent progressEvent);
}
