using System.Text;
using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Models;
using Serilog;
using Serilog.Events;

namespace GameToolOrchestrator.Infrastructure.Logging;

public sealed class SerilogExecutionLogger : IExecutionLogger, IDisposable
{
    private readonly ILogger _logger;

    public SerilogExecutionLogger(string logDirectory = "logs")
    {
        Directory.CreateDirectory(logDirectory);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(logDirectory, "orchestrator-.log"),
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: LogEventLevel.Information,
                encoding: Encoding.UTF8,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public void Info(string message)
    {
        _logger.Information("{Message}", message);
    }

    public void Warning(string message)
    {
        _logger.Warning("{Message}", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        if (exception is null)
        {
            _logger.Error("{Message}", message);
            return;
        }

        _logger.Error(exception, "{Message}", message);
    }

    public void StatusChanged(string scopeId, ExecutionStatus status, string? message = null)
    {
        _logger.Information(
            "Status changed: {ScopeId} -> {Status}. {StatusMessage}",
            scopeId,
            status,
            message ?? string.Empty);
    }

    public void Dispose()
    {
        if (_logger is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
