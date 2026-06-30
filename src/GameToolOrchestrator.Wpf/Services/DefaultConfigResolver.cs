using System.IO;

namespace GameToolOrchestrator.Wpf.Services;

public sealed class DefaultConfigResolver : IDefaultConfigResolver
{
    private readonly IReadOnlyList<string>? _rootDirectories;

    public DefaultConfigResolver(IEnumerable<string>? rootDirectories = null)
    {
        _rootDirectories = rootDirectories?.ToList();
    }

    public DefaultConfigSearchResult FindDefaultConfig()
    {
        var candidates = EnumerateRootDirectories()
            .SelectMany(directory => new[]
            {
                Path.Combine(directory, "config.json"),
                Path.Combine(directory, "sample-config.json")
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DefaultConfigSearchResult
        {
            CandidatePaths = candidates,
            FoundPath = candidates.FirstOrDefault(File.Exists)
        };
    }

    private IEnumerable<string> EnumerateRootDirectories()
    {
        if (_rootDirectories is not null)
        {
            foreach (var directory in _rootDirectories)
            {
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    yield return directory;
                }
            }

            yield break;
        }

        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;

        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }
}
