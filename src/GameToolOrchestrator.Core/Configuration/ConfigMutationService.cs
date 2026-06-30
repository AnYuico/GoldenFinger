using System.Text.Json;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Core.Configuration;

public sealed class ConfigMutationService : IConfigMutationService
{
    private static readonly JsonSerializerOptions CloneOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ToolDefinition AddTool(OrchestratorConfig config)
    {
        var tool = new ToolDefinition
        {
            Id = GenerateNumberedId("tool", config.Tools.Select(tool => tool.Id)),
            Name = "New Tool",
            Type = "generic-ui",
            ExecutablePath = string.Empty,
            WorkingDirectory = string.Empty,
            Arguments = string.Empty,
            LaunchTimeoutSeconds = 30,
            TaskTimeoutMinutes = 180
        };

        config.Tools.Add(tool);
        return tool;
    }

    public ToolDefinition CopyTool(OrchestratorConfig config, ToolDefinition source)
    {
        var copy = DeepClone(source);
        copy.Id = GenerateCopyId(source.Id, config.Tools.Select(tool => tool.Id));
        copy.Name = AppendCopyName(source.Name);
        config.Tools.Add(copy);
        return copy;
    }

    public MutationResult DeleteTool(OrchestratorConfig config, ToolDefinition tool)
    {
        var references = FindToolReferences(config, tool.Id);
        if (references.Count > 0)
        {
            return MutationResult.Failed($"Tool '{tool.Id}' is referenced by: {string.Join(", ", references)}");
        }

        return config.Tools.Remove(tool)
            ? MutationResult.Success()
            : MutationResult.Failed($"Tool '{tool.Id}' was not found.");
    }

    public IReadOnlyList<string> FindToolReferences(OrchestratorConfig config, string toolId)
    {
        return config.TaskPlans
            .SelectMany(plan => plan.Steps
                .Where(step => string.Equals(step.ToolId, toolId, StringComparison.OrdinalIgnoreCase))
                .Select(step => $"{plan.Id}/{step.Id}"))
            .ToList();
    }

    public TaskPlan AddTaskPlan(OrchestratorConfig config)
    {
        var plan = new TaskPlan
        {
            Id = GenerateNumberedId("plan", config.TaskPlans.Select(plan => plan.Id)),
            Name = "New Task Plan",
            Execution = new ExecutionOptions { StopOnFailure = true },
            Steps = []
        };

        config.TaskPlans.Add(plan);
        return plan;
    }

    public TaskPlan CopyTaskPlan(OrchestratorConfig config, TaskPlan source)
    {
        var copy = DeepClone(source);
        copy.Id = GenerateCopyId(source.Id, config.TaskPlans.Select(plan => plan.Id));
        copy.Name = AppendCopyName(source.Name);
        config.TaskPlans.Add(copy);
        return copy;
    }

    public MutationResult DeleteTaskPlan(OrchestratorConfig config, TaskPlan plan)
    {
        return config.TaskPlans.Remove(plan)
            ? MutationResult.Success()
            : MutationResult.Failed($"TaskPlan '{plan.Id}' was not found.");
    }

    public TaskStep? AddStep(OrchestratorConfig config, TaskPlan plan, ToolDefinition? tool)
    {
        if (tool is null)
        {
            return null;
        }

        var step = new TaskStep
        {
            Id = GenerateNumberedId("step", plan.Steps.Select(step => step.Id)),
            ToolId = tool.Id,
            Order = GetNextStepOrder(plan),
            Enabled = true,
            Actions = []
        };

        plan.Steps.Add(step);
        return step;
    }

    public TaskStep CopyStep(TaskPlan plan, TaskStep source)
    {
        var copy = DeepClone(source);
        copy.Id = GenerateCopyId(source.Id, plan.Steps.Select(step => step.Id));
        copy.Order = GetNextStepOrder(plan);
        plan.Steps.Add(copy);
        return copy;
    }

    public MutationResult DeleteStep(TaskPlan plan, TaskStep step)
    {
        return plan.Steps.Remove(step)
            ? MutationResult.Success()
            : MutationResult.Failed($"Step '{step.Id}' was not found.");
    }

    public void NormalizeStepOrder(TaskPlan plan)
    {
        var sorted = plan.Steps
            .OrderBy(step => step.Order)
            .ThenBy(step => step.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        plan.Steps.Clear();
        for (var index = 0; index < sorted.Count; index++)
        {
            sorted[index].Order = index + 1;
            plan.Steps.Add(sorted[index]);
        }
    }

    public AutomationActionDefinition? AddAction(TaskStep step, string actionType)
    {
        var normalized = actionType.Trim();
        AutomationActionDefinition? action = normalized switch
        {
            "waitSeconds" => new AutomationActionDefinition
            {
                Type = "waitSeconds",
                Enabled = true,
                Parameters = new Dictionary<string, string> { ["seconds"] = "1" }
            },
            "waitProcessExit" => new AutomationActionDefinition
            {
                Type = "waitProcessExit",
                Enabled = true,
                TimeoutMinutes = 180
            },
            "waitWindow" => new AutomationActionDefinition
            {
                Type = "waitWindow",
                Enabled = true,
                TitleContains = string.Empty,
                TimeoutSeconds = 30
            },
            "clickButton" => new AutomationActionDefinition
            {
                Type = "clickButton",
                Enabled = true,
                WindowTitleContains = string.Empty,
                NameContains = string.Empty,
                ControlType = "Button",
                TimeoutSeconds = 10
            },
            _ => null
        };

        if (action is null)
        {
            return null;
        }

        action.Id = GenerateNumberedId(ToIdBase(action.Type), step.Actions.Select(action => action.Id));
        action.Order = GetNextActionOrder(step);
        step.Actions.Add(action);
        return action;
    }

    public AutomationActionDefinition CopyAction(TaskStep step, AutomationActionDefinition source)
    {
        var copy = DeepClone(source);
        copy.Id = GenerateCopyId(source.Id, step.Actions.Select(action => action.Id));
        copy.Order = GetNextActionOrder(step);
        step.Actions.Add(copy);
        return copy;
    }

    public MutationResult DeleteAction(TaskStep step, AutomationActionDefinition action)
    {
        return step.Actions.Remove(action)
            ? MutationResult.Success()
            : MutationResult.Failed($"Action '{action.Id}' was not found.");
    }

    private static int GetNextStepOrder(TaskPlan plan)
    {
        return plan.Steps.Count == 0 ? 1 : plan.Steps.Max(step => step.Order) + 1;
    }

    private static int GetNextActionOrder(TaskStep step)
    {
        return step.Actions.Count == 0 ? 1 : step.Actions.Max(action => action.Order) + 1;
    }

    private static string GenerateNumberedId(string prefix, IEnumerable<string> existingIds)
    {
        var existing = new HashSet<string>(existingIds, StringComparer.OrdinalIgnoreCase);
        var index = 1;
        string candidate;
        do
        {
            candidate = $"{prefix}-{index++}";
        }
        while (existing.Contains(candidate));

        return candidate;
    }

    private static string GenerateCopyId(string sourceId, IEnumerable<string> existingIds)
    {
        var existing = new HashSet<string>(existingIds, StringComparer.OrdinalIgnoreCase);
        var baseId = string.IsNullOrWhiteSpace(sourceId) ? "item" : sourceId.Trim();
        var candidate = $"{baseId}-copy";
        if (!existing.Contains(candidate))
        {
            return candidate;
        }

        var index = 2;
        do
        {
            candidate = $"{baseId}-copy-{index++}";
        }
        while (existing.Contains(candidate));

        return candidate;
    }

    private static string AppendCopyName(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? "Copy" : $"{name} Copy";
    }

    private static string ToIdBase(string value)
    {
        var chars = value.SelectMany((character, index) =>
        {
            if (char.IsUpper(character) && index > 0)
            {
                return new[] { '-', char.ToLowerInvariant(character) };
            }

            return new[] { char.ToLowerInvariant(character) };
        });

        var normalized = new string(chars.ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "action" : normalized;
    }

    private static T DeepClone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, CloneOptions);
        return JsonSerializer.Deserialize<T>(json, CloneOptions)
            ?? throw new InvalidOperationException($"Failed to clone {typeof(T).Name}.");
    }
}
