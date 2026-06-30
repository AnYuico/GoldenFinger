using System.IO;
using System.Text;
using System.Windows;

namespace GameToolOrchestrator.Wpf.Services;

public static class WpfCrashLogger
{
    private static readonly object SyncRoot = new();

    public static string LogPath { get; } = Path.GetFullPath(Path.Combine("logs", "wpf-crash.log"));

    public static void Log(Exception exception, string context)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var builder = new StringBuilder();
            builder.AppendLine("============================================================");
            builder.AppendLine($"Time: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");
            builder.AppendLine($"Context: {context}");
            AppendException(builder, exception, depth: 0);

            lock (SyncRoot)
            {
                File.AppendAllText(LogPath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Crash logging must never throw back into the WPF dispatcher.
        }
    }

    public static void ShowMessageBox(string context)
    {
        try
        {
            MessageBox.Show(
                $"{context}{Environment.NewLine}{Environment.NewLine}请查看日志：{LogPath}",
                "GameToolOrchestrator 启动或运行错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // If WPF itself cannot show a dialog, the crash log is still the source of truth.
        }
    }

    private static void AppendException(StringBuilder builder, Exception exception, int depth)
    {
        var prefix = depth == 0 ? "Exception" : $"InnerException[{depth}]";
        builder.AppendLine($"{prefix}.Type: {exception.GetType().FullName}");
        builder.AppendLine($"{prefix}.Message: {exception.Message}");
        builder.AppendLine($"{prefix}.StackTrace:");
        builder.AppendLine(exception.StackTrace ?? "<no stack trace>");

        if (exception.InnerException is not null)
        {
            AppendException(builder, exception.InnerException, depth + 1);
        }
    }
}
