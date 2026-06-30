using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Abstractions;

public interface IExecutionLogger
{
    void Info(string message);

    void Warning(string message);

    void Error(string message, Exception? exception = null);

    void StatusChanged(string scopeId, ExecutionStatus status, string? message = null);
}
