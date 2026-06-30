using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Wpf.ViewModels;

public sealed class ToolItemViewModel : ObservableObject
{
    public ToolItemViewModel(ToolDefinition tool)
    {
        Tool = tool;
    }

    public ToolDefinition Tool { get; }

    public string Id => Tool.Id;

    public string Name => Tool.Name;

    public string Type => Tool.Type;

    public string ExecutablePath
    {
        get => Tool.ExecutablePath;
        set
        {
            if (Tool.ExecutablePath == value)
            {
                return;
            }

            Tool.ExecutablePath = value;
            OnPropertyChanged();
        }
    }

    public string WorkingDirectory
    {
        get => Tool.WorkingDirectory;
        set
        {
            if (Tool.WorkingDirectory == value)
            {
                return;
            }

            Tool.WorkingDirectory = value;
            OnPropertyChanged();
        }
    }

    public void RefreshFromTool()
    {
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(ExecutablePath));
        OnPropertyChanged(nameof(WorkingDirectory));
    }
}
