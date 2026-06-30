using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Wpf.Services;

public sealed class UiExecutionLogger : IExecutionLogger, IDisposable
{
    private readonly IExecutionLogger _inner;
    private readonly Action<string, string> _append;

    public UiExecutionLogger(IExecutionLogger inner, Action<string, string> append)
    {
        _inner = inner;
        _append = append;
    }

    public void Info(string message)
    {
        _inner.Info(message);
        _append("Information", message);
    }

    public void Warning(string message)
    {
        _inner.Warning(message);
        _append("Warning", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        _inner.Error(message, exception);
        _append("Error", exception is null ? message : $"{message} {exception.Message}");
    }

    public void StatusChanged(string scopeId, ExecutionStatus status, string? message = null)
    {
        _inner.StatusChanged(scopeId, status, message);
        _append(status is ExecutionStatus.Failed or ExecutionStatus.TimedOut ? "Error" : "Information",
            $"{scopeId} -> {status}. {message}");
    }

    public void Dispose()
    {
        if (_inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
