using System.Windows;

namespace GameToolOrchestrator.Wpf.Services;

public sealed class SystemClipboardService : IClipboardService
{
    public void CopyText(string text)
    {
        Clipboard.SetText(text);
    }
}
