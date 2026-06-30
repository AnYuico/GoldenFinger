using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Actions;
using GameToolOrchestrator.Infrastructure.Automation.Actions;

namespace GameToolOrchestrator.Infrastructure.Automation;

public static class InfrastructureActionExecutors
{
    public static IActionExecutorFactory CreateDefaultFactory(IUiAutomationService automationService)
    {
        return new DefaultActionExecutorFactory(
        [
            new WaitSecondsActionExecutor(),
            new WaitProcessExitActionExecutor(),
            new WaitWindowActionExecutor(automationService),
            new ClickButtonActionExecutor(automationService)
        ]);
    }
}
