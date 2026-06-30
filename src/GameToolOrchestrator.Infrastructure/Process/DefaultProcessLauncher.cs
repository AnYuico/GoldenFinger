using System.Diagnostics;
using System.Security.Principal;
using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Infrastructure.Process;

public sealed class DefaultProcessLauncher : IProcessLauncher
{
    public Task<IProcessHandle> LaunchAsync(ToolDefinition tool, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(tool.ExecutablePath))
        {
            throw new InvalidOperationException($"Tool '{tool.Id}' has no executablePath.");
        }

        if (!File.Exists(tool.ExecutablePath))
        {
            throw new FileNotFoundException($"Executable for tool '{tool.Id}' was not found.", tool.ExecutablePath);
        }

        if (tool.RequiresAdministrator && !IsCurrentProcessElevated())
        {
            throw new InvalidOperationException(
                $"Tool '{tool.Id}' is configured as requiring administrator privileges. Run GameToolOrchestrator as administrator or change the tool configuration.");
        }

        var workingDirectory = string.IsNullOrWhiteSpace(tool.WorkingDirectory)
            ? Path.GetDirectoryName(Path.GetFullPath(tool.ExecutablePath)) ?? Environment.CurrentDirectory
            : tool.WorkingDirectory;

        var startInfo = new ProcessStartInfo
        {
            FileName = tool.ExecutablePath,
            Arguments = tool.Arguments ?? string.Empty,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start tool '{tool.Id}'.");

        return Task.FromResult<IProcessHandle>(new DefaultProcessHandle(process));
    }

    private static bool IsCurrentProcessElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
