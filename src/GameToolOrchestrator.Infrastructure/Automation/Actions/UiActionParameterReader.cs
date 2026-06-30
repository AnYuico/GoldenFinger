using GameToolOrchestrator.Core.Actions;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Infrastructure.Automation.Actions;

internal static class UiActionParameterReader
{
    public static string GetString(
        AutomationActionDefinition action,
        string directValue,
        string parameterKey)
    {
        if (!string.IsNullOrWhiteSpace(directValue))
        {
            return directValue;
        }

        return ActionParameterReader.GetString(action.Parameters, parameterKey) ?? string.Empty;
    }
}
