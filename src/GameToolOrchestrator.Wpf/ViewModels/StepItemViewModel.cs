using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Wpf.ViewModels;

public sealed class StepItemViewModel : ObservableObject
{
    private ExecutionStatus _status = ExecutionStatus.Pending;
    private string _lastError = string.Empty;
    private ToolDefinition? _tool;

    public StepItemViewModel(TaskStep step, ToolDefinition? tool)
    {
        Step = step;
        _tool = tool;
    }

    public TaskStep Step { get; }

    public string Id => Step.Id;

    public int Order => Step.Order;

    public string ToolId => Step.ToolId;

    public string ToolName => _tool?.Name ?? string.Empty;

    public ToolDefinition? Tool
    {
        get => _tool;
        set
        {
            if (SetProperty(ref _tool, value))
            {
                OnPropertyChanged(nameof(ToolName));
                OnPropertyChanged(nameof(ExecutablePath));
                OnPropertyChanged(nameof(WorkingDirectory));
            }
        }
    }

    public bool Enabled
    {
        get => Step.Enabled;
        set
        {
            if (Step.Enabled == value)
            {
                return;
            }

            Step.Enabled = value;
            OnPropertyChanged();
        }
    }

    public string ExecutablePath
    {
        get => _tool?.ExecutablePath ?? string.Empty;
        set
        {
            if (_tool is null || _tool.ExecutablePath == value)
            {
                return;
            }

            _tool.ExecutablePath = value;
            OnPropertyChanged();
        }
    }

    public string WorkingDirectory
    {
        get => _tool?.WorkingDirectory ?? string.Empty;
        set
        {
            if (_tool is null || _tool.WorkingDirectory == value)
            {
                return;
            }

            _tool.WorkingDirectory = value;
            OnPropertyChanged();
        }
    }

    public ExecutionStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string LastError
    {
        get => _lastError;
        set => SetProperty(ref _lastError, value);
    }
}
