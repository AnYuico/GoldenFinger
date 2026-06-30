using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Wpf.ViewModels;

public sealed class ActionItemViewModel : ObservableObject
{
    public ActionItemViewModel(AutomationActionDefinition action, int index)
    {
        Action = action;
        Index = index;
    }

    public AutomationActionDefinition Action { get; }

    public int Index { get; }

    public ActionEditorViewModel? Editor { get; set; }

    public string Id => Action.Id;

    public string Type => Action.Type;

    public bool Enabled
    {
        get => Action.Enabled;
        set
        {
            if (Action.Enabled == value)
            {
                return;
            }

            Action.Enabled = value;
            OnPropertyChanged();
        }
    }

    public string Summary
    {
        get
        {
            var direct = new[]
            {
                Pair("windowTitleContains", Action.WindowTitleContains),
                Pair("titleContains", Action.TitleContains),
                Pair("automationId", Action.AutomationId),
                Pair("nameEquals", Action.NameEquals),
                Pair("nameContains", Action.NameContains),
                Pair("controlType", Action.ControlType)
            }.Where(value => value.Length > 0);

            var parameters = Action.Parameters.Select(pair => $"{pair.Key}={pair.Value}");
            return string.Join(", ", direct.Concat(parameters));
        }
    }

    private static string Pair(string key, string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : $"{key}={value}";
    }

    public void RefreshFromAction()
    {
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(Summary));
    }
}
