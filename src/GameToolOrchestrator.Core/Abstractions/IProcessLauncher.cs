using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Abstractions;

public interface IProcessLauncher
{
    Task<IProcessHandle> LaunchAsync(ToolDefinition tool, CancellationToken cancellationToken);
}
