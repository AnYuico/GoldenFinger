using GameToolOrchestrator.Core.Abstractions;

namespace GameToolOrchestrator.Wpf.Services;

public interface IExecutionEngineFactory
{
    IExecutionEngine Create(IExecutionProgressReporter progressReporter);
}
