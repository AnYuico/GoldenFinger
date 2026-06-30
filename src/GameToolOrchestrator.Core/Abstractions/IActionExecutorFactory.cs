namespace GameToolOrchestrator.Core.Abstractions;

public interface IActionExecutorFactory
{
    bool TryGetExecutor(string actionType, out IActionExecutor executor);
}
