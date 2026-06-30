using System.IO;
using System.Text;

namespace GameToolOrchestrator.Wpf.Services;

public sealed class WpfStartupLogger
{
    private readonly object _syncRoot = new();

    public WpfStartupLogger(string? logDirectory = null)
    {
        var directory = Path.GetFullPath(logDirectory ?? "logs");
        Directory.CreateDirectory(directory);
        LogPath = Path.Combine(directory, "wpf-startup.log");
    }

    public string LogPath { get; }

    public void BeginSession()
    {
        Log("============================================================");
        Log($"程序启动时间: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");
        Log($"当前工作目录: {Environment.CurrentDirectory}");
        Log($"AppContext.BaseDirectory: {AppContext.BaseDirectory}");
    }

    public void Log(string message)
    {
        lock (_syncRoot)
        {
            File.AppendAllText(
                LogPath,
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
    }

    public void LogException(string message, Exception exception)
    {
        Log($"{message}: {exception.GetType().FullName}: {exception.Message}");
        Log(exception.StackTrace ?? "<no stack trace>");
    }
}
