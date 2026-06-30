using GameToolOrchestrator.Core.Abstractions;

namespace GameToolOrchestrator.Core.Actions;

public sealed class DefaultActionExecutorFactory : IActionExecutorFactory
{
    private readonly Dictionary<string, IActionExecutor> _executors;

    public DefaultActionExecutorFactory(IEnumerable<IActionExecutor> executors)
    {
        _executors = executors.ToDictionary(
            executor => executor.ActionType,
            executor => executor,
            StringComparer.OrdinalIgnoreCase);
    }

    public static DefaultActionExecutorFactory CreateDefault()
    {
        return new DefaultActionExecutorFactory(
        [
            new WaitSecondsActionExecutor(),
            new WaitProcessExitActionExecutor()
        ]);
    }

    public bool TryGetExecutor(string actionType, out IActionExecutor executor)
    {
        return _executors.TryGetValue(actionType, out executor!);
    }
}
