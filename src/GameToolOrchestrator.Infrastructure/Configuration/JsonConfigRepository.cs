using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Infrastructure.Configuration;

public sealed class JsonConfigRepository : IConfigRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    static JsonConfigRepository()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    }

    public async Task<OrchestratorConfig> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<OrchestratorConfig>(
            stream,
            SerializerOptions,
            cancellationToken);

        return config ?? throw new InvalidOperationException($"Config file '{path}' is empty or invalid.");
    }

    public async Task SaveAsync(string path, OrchestratorConfig config, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, config, SerializerOptions, cancellationToken);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(Environment.NewLine), cancellationToken);
    }
}
