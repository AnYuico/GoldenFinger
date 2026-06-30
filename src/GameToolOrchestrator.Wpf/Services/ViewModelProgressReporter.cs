using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Progress;

namespace GameToolOrchestrator.Wpf.Services;

public sealed class ViewModelProgressReporter : IExecutionProgressReporter
{
    private readonly Action<ExecutionProgressEvent> _report;

    public ViewModelProgressReporter(Action<ExecutionProgressEvent> report)
    {
        _report = report;
    }

    public void Report(ExecutionProgressEvent progressEvent)
    {
        _report(progressEvent);
    }
}
