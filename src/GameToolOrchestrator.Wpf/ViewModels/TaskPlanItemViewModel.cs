using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Wpf.ViewModels;

public sealed class TaskPlanItemViewModel : ObservableObject
{
    public TaskPlanItemViewModel(TaskPlan plan)
    {
        Plan = plan;
    }

    public TaskPlan Plan { get; }

    public string Id => Plan.Id;

    public string Name => Plan.Name;

    public bool Enabled => true;

    public string EnabledText => Enabled ? "\u542f\u7528" : "\u7981\u7528";

    public int StepCount => Plan.Steps.Count;
}
