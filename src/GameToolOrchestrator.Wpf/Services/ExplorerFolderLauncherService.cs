using System.Diagnostics;

namespace GameToolOrchestrator.Wpf.Services;

public sealed class ExplorerFolderLauncherService : IFolderLauncherService
{
    public void OpenFolder(string folderPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{folderPath}\"",
            UseShellExecute = true
        });
    }
}
