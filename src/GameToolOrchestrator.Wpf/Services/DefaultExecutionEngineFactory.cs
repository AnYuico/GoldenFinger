using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Engine;
using GameToolOrchestrator.Infrastructure.Automation;
using GameToolOrchestrator.Infrastructure.Process;

namespace GameToolOrchestrator.Wpf.Services;

public sealed class DefaultExecutionEngineFactory : IExecutionEngineFactory
{
    private readonly IUiAutomationService _automationService;
    private readonly IExecutionLogger _logger;

    public DefaultExecutionEngineFactory(
        IUiAutomationService automationService,
        IExecutionLogger logger)
    {
        _automationService = automationService;
        _logger = logger;
    }

    public IExecutionEngine Create(IExecutionProgressReporter progressReporter)
    {
        return new ExecutionEngine(
            new DefaultProcessLauncher(),
            InfrastructureActionExecutors.CreateDefaultFactory(_automationService),
            _logger,
            progressReporter);
    }
}
