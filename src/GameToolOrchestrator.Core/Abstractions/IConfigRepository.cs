using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Abstractions;

public interface IConfigRepository
{
    Task<OrchestratorConfig> LoadAsync(string path, CancellationToken cancellationToken = default);

    Task SaveAsync(string path, OrchestratorConfig config, CancellationToken cancellationToken = default);
}
