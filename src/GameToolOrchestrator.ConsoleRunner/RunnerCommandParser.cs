namespace GameToolOrchestrator.ConsoleRunner;

public static class RunnerCommandParser
{
    public static RunnerCommand? Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        var first = args[0];
        if (!first.StartsWith("--", StringComparison.Ordinal) && args.Length == 2)
        {
            return new RunnerCommand
            {
                Type = RunnerCommandType.RunPlan,
                ConfigPath = args[0],
                TaskPlanId = args[1]
            };
        }

        if (string.Equals(first, "inspect-windows", StringComparison.OrdinalIgnoreCase))
        {
            return new RunnerCommand { Type = RunnerCommandType.InspectWindows };
        }

        if (string.Equals(first, "inspect-window", StringComparison.OrdinalIgnoreCase))
        {
            var options = ParseOptions(args.Skip(1).ToArray());
            return new RunnerCommand
            {
                Type = RunnerCommandType.InspectWindow,
                WindowCriteria =
                {
                    TitleEquals = Get(options, "title-equals"),
                    TitleContains = Get(options, "title-contains")
                },
                MaxDepth = GetInt(options, "max-depth", 4)
            };
        }

        if (string.Equals(first, "test-selector", StringComparison.OrdinalIgnoreCase))
        {
            var options = ParseOptions(args.Skip(1).ToArray());
            return new RunnerCommand
            {
                Type = RunnerCommandType.TestSelector,
                ButtonCriteria =
                {
                    WindowTitleEquals = Get(options, "window-title-equals"),
                    WindowTitleContains = Get(options, "window-title-contains"),
                    AutomationId = Get(options, "automation-id"),
                    NameEquals = Get(options, "name-equals"),
                    NameContains = Get(options, "name-contains"),
                    ControlType = string.IsNullOrWhiteSpace(Get(options, "control-type")) ? "Button" : Get(options, "control-type")
                },
                Click = options.ContainsKey("click"),
                MaxDepth = GetInt(options, "max-depth", 4),
                TimeoutSeconds = GetInt(options, "timeout-seconds", 10)
            };
        }

        if (string.Equals(first, "run-action", StringComparison.OrdinalIgnoreCase))
        {
            var options = ParseOptions(args.Skip(1).ToArray());
            var configPath = Get(options, "config");
            var toolId = Get(options, "tool");
            if (string.IsNullOrWhiteSpace(configPath) || string.IsNullOrWhiteSpace(toolId))
            {
                return null;
            }

            return new RunnerCommand
            {
                Type = RunnerCommandType.RunAction,
                ConfigPath = configPath,
                ToolId = toolId,
                ActionIndex = GetInt(options, "action-index", 0)
            };
        }

        if (string.Equals(first, "--config", StringComparison.OrdinalIgnoreCase))
        {
            var options = ParseOptions(args);
            var configPath = Get(options, "config");
            var plan = Get(options, "plan");
            if (string.IsNullOrWhiteSpace(configPath) || string.IsNullOrWhiteSpace(plan))
            {
                return null;
            }

            return new RunnerCommand
            {
                Type = RunnerCommandType.RunPlan,
                ConfigPath = configPath,
                TaskPlanId = plan
            };
        }

        return null;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = current[2..];
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[key] = args[++index];
            }
            else
            {
                options[key] = "true";
            }
        }

        return options;
    }

    private static string Get(IReadOnlyDictionary<string, string> options, string key)
    {
        return options.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static int GetInt(IReadOnlyDictionary<string, string> options, string key, int defaultValue)
    {
        return int.TryParse(Get(options, key), out var value) ? value : defaultValue;
    }
}
