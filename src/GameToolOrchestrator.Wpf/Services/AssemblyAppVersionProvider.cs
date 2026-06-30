using System.Reflection;

namespace GameToolOrchestrator.Wpf.Services;

public sealed class AssemblyAppVersionProvider : IAppVersionProvider
{
    private readonly Assembly _assembly;

    public AssemblyAppVersionProvider()
        : this(typeof(AssemblyAppVersionProvider).Assembly)
    {
    }

    public AssemblyAppVersionProvider(Assembly assembly)
    {
        _assembly = assembly;
    }

    public string Version
    {
        get
        {
            var informationalVersion = _assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                return informationalVersion;
            }

            return _assembly.GetName().Version?.ToString() ?? "unknown";
        }
    }
}
