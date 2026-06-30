using System.Globalization;
using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Automation;
using GameToolOrchestrator.Core.Models;

namespace GameToolOrchestrator.Wpf.ViewModels;

public sealed class ActionEditorViewModel : ObservableObject
{
    private readonly ActionItemViewModel _actionItem;
    private readonly IUiAutomationService _automationService;
    private readonly Action _markDirty;
    private readonly Action<string, string> _addLog;
    private string _seconds = string.Empty;
    private string _timeoutSeconds = string.Empty;
    private string _timeoutMinutes = string.Empty;
    private string _titleEquals = string.Empty;
    private string _titleContains = string.Empty;
    private string _windowTitleContains = string.Empty;
    private string _automationId = string.Empty;
    private string _nameEquals = string.Empty;
    private string _nameContains = string.Empty;
    private string _controlType = string.Empty;
    private string _validationMessage = string.Empty;

    public ActionEditorViewModel(
        ActionItemViewModel actionItem,
        IUiAutomationService automationService,
        Action markDirty,
        Action<string, string> addLog)
    {
        _actionItem = actionItem;
        _automationService = automationService;
        _markDirty = markDirty;
        _addLog = addLog;

        LoadFromAction();
        TestSelectorCommand = new AsyncRelayCommand(() => TestSelectorAsync(click: false), CanTestSelector);
        TestSelectorAndClickCommand = new AsyncRelayCommand(() => TestSelectorAsync(click: true), CanTestSelector);
    }

    public AsyncRelayCommand TestSelectorCommand { get; }

    public AsyncRelayCommand TestSelectorAndClickCommand { get; }

    public AutomationActionDefinition Action => _actionItem.Action;

    public int Index => _actionItem.Index;

    public string Type => Action.Type;

    public bool IsWaitSecondsEditor => IsType("waitSeconds");

    public bool IsWaitProcessExitEditor => IsType("waitProcessExit");

    public bool IsWaitWindowEditor => IsType("waitWindow");

    public bool IsClickButtonEditor => IsType("clickButton");

    public bool IsSupportedEditor =>
        IsWaitSecondsEditor || IsWaitProcessExitEditor || IsWaitWindowEditor || IsClickButtonEditor;

    public bool IsUnsupportedEditor => !IsSupportedEditor;

    public string UnsupportedMessage =>
        IsSupportedEditor ? string.Empty : "当前 UI 暂不支持编辑此 action 类型，请直接修改 JSON。";

    public string TimeoutRuleHint =>
        IsWaitProcessExitEditor
            ? "生效规则：Parameters 中的 timeout* 优先；UI 字段中 timeoutMinutes 优先于 timeoutSeconds；都为空则使用全局 processExit 超时。"
            : "留空则使用执行引擎默认 action 超时；填写时必须为正数。";

    public string TitlePriorityHint =>
        !string.IsNullOrWhiteSpace(TitleEquals) && !string.IsNullOrWhiteSpace(TitleContains)
            ? "titleEquals 和 titleContains 都已填写，实际匹配优先使用 titleEquals。"
            : string.Empty;

    public string SelectorPriorityHint
    {
        get
        {
            var filled = new[]
            {
                AutomationId,
                NameEquals,
                NameContains
            }.Count(value => !string.IsNullOrWhiteSpace(value));

            return filled > 1
                ? "已填写多个 selector，匹配优先级：automationId > nameEquals > nameContains。"
                : "selector 优先级：automationId > nameEquals > nameContains。";
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public bool Enabled
    {
        get => _actionItem.Enabled;
        set
        {
            if (_actionItem.Enabled == value)
            {
                return;
            }

            _actionItem.Enabled = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public string Seconds
    {
        get => _seconds;
        set => SetEditableProperty(ref _seconds, value);
    }

    public string TimeoutSeconds
    {
        get => _timeoutSeconds;
        set => SetEditableProperty(ref _timeoutSeconds, value);
    }

    public string TimeoutMinutes
    {
        get => _timeoutMinutes;
        set => SetEditableProperty(ref _timeoutMinutes, value);
    }

    public string TitleEquals
    {
        get => _titleEquals;
        set
        {
            if (SetEditableProperty(ref _titleEquals, value))
            {
                OnPropertyChanged(nameof(TitlePriorityHint));
            }
        }
    }

    public string TitleContains
    {
        get => _titleContains;
        set
        {
            if (SetEditableProperty(ref _titleContains, value))
            {
                OnPropertyChanged(nameof(TitlePriorityHint));
            }
        }
    }

    public string WindowTitleContains
    {
        get => _windowTitleContains;
        set
        {
            if (SetEditableProperty(ref _windowTitleContains, value))
            {
                RaiseSelectorCommandStates();
            }
        }
    }

    public string AutomationId
    {
        get => _automationId;
        set
        {
            if (SetEditableProperty(ref _automationId, value))
            {
                OnPropertyChanged(nameof(SelectorPriorityHint));
                RaiseSelectorCommandStates();
            }
        }
    }

    public string NameEquals
    {
        get => _nameEquals;
        set
        {
            if (SetEditableProperty(ref _nameEquals, value))
            {
                OnPropertyChanged(nameof(SelectorPriorityHint));
                RaiseSelectorCommandStates();
            }
        }
    }

    public string NameContains
    {
        get => _nameContains;
        set
        {
            if (SetEditableProperty(ref _nameContains, value))
            {
                OnPropertyChanged(nameof(SelectorPriorityHint));
                RaiseSelectorCommandStates();
            }
        }
    }

    public string ControlType
    {
        get => _controlType;
        set => SetEditableProperty(ref _controlType, value);
    }

    public bool ValidateAndCommit()
    {
        if (!Validate())
        {
            return false;
        }

        switch (Type.Trim().ToLowerInvariant())
        {
            case "waitseconds":
                SetParameter("seconds", Seconds);
                ApplyTimeoutSeconds();
                break;
            case "waitprocessexit":
                ApplyTimeoutSeconds();
                ApplyTimeoutMinutes();
                break;
            case "waitwindow":
                Action.TitleEquals = TitleEquals.Trim();
                Action.TitleContains = TitleContains.Trim();
                RemoveParameter("titleEquals");
                RemoveParameter("titleContains");
                ApplyTimeoutSeconds();
                break;
            case "clickbutton":
                Action.WindowTitleContains = WindowTitleContains.Trim();
                Action.AutomationId = AutomationId.Trim();
                Action.NameEquals = NameEquals.Trim();
                Action.NameContains = NameContains.Trim();
                Action.ControlType = string.IsNullOrWhiteSpace(ControlType) ? "Button" : ControlType.Trim();
                RemoveParameter("windowTitleContains");
                RemoveParameter("automationId");
                RemoveParameter("nameEquals");
                RemoveParameter("nameContains");
                RemoveParameter("controlType");
                ApplyTimeoutSeconds();
                break;
        }

        _actionItem.RefreshFromAction();
        ValidationMessage = string.Empty;
        return true;
    }

    public bool Validate()
    {
        if (IsUnsupportedEditor)
        {
            ValidationMessage = string.Empty;
            return true;
        }

        if (IsWaitSecondsEditor && !ValidatePositiveDoubleOrEmpty(Seconds, "seconds"))
        {
            return false;
        }

        if ((IsWaitSecondsEditor || IsWaitWindowEditor || IsClickButtonEditor) &&
            !ValidatePositiveIntOrEmpty(TimeoutSeconds, "timeoutSeconds"))
        {
            return false;
        }

        if (IsWaitProcessExitEditor &&
            !ValidatePositiveIntOrEmpty(TimeoutSeconds, "timeoutSeconds"))
        {
            return false;
        }

        if (IsWaitProcessExitEditor &&
            !ValidatePositiveIntOrEmpty(TimeoutMinutes, "timeoutMinutes"))
        {
            return false;
        }

        if (IsWaitWindowEditor &&
            string.IsNullOrWhiteSpace(TitleEquals) &&
            string.IsNullOrWhiteSpace(TitleContains))
        {
            ValidationMessage = "waitWindow 需要填写 titleEquals 或 titleContains。";
            return false;
        }

        if (IsClickButtonEditor &&
            string.IsNullOrWhiteSpace(AutomationId) &&
            string.IsNullOrWhiteSpace(NameEquals) &&
            string.IsNullOrWhiteSpace(NameContains))
        {
            ValidationMessage = "clickButton 需要填写 automationId、nameEquals 或 nameContains。";
            return false;
        }

        ValidationMessage = string.Empty;
        return true;
    }

    public async Task TestSelectorAsync(bool click)
    {
        if (!IsClickButtonEditor)
        {
            _addLog("Warning", "Only clickButton actions support selector testing.");
            return;
        }

        if (!ValidateClickButtonSelectorOnly())
        {
            _addLog("Error", ValidationMessage);
            return;
        }

        var criteria = CreateButtonCriteria();

        try
        {
            var result = await _automationService.FindElementsAsync(criteria, diagnosticMaxDepth: 4, CancellationToken.None);
            _addLog("Information", $"Action editor selector matched {result.Elements.Count} controls.");

            foreach (var element in result.Elements)
            {
                _addLog("Information", $"[{element.Index}] Name='{element.Name}', AutomationId='{element.AutomationId}', ControlType='{element.ControlType}', Enabled={element.IsEnabled}, Offscreen={element.IsOffscreen}");
            }

            if (!click)
            {
                _addLog("Information", "Action editor selector test completed without clicking.");
                return;
            }

            if (result.Elements.Count == 0)
            {
                _addLog("Warning", "No control matched; click was not attempted.");
                return;
            }

            _addLog("Warning", $"About to click [{result.Elements[0].Index}] {result.Elements[0].Name}.");
            var clickResult = await _automationService.ClickButtonAsync(criteria, TimeSpan.FromSeconds(10), 4, CancellationToken.None);
            _addLog(clickResult.Status == ExecutionStatus.Succeeded ? "Information" : "Error", $"Click result: {clickResult.Status}. {clickResult.ErrorMessage}");
        }
        catch (Exception exception)
        {
            _addLog("Error", $"Action editor selector test failed: {exception.Message}");
        }
    }

    private void LoadFromAction()
    {
        _seconds = GetParameter("seconds") ?? GetParameter("durationSeconds") ?? string.Empty;
        _timeoutSeconds = Action.TimeoutSeconds?.ToString(CultureInfo.InvariantCulture)
            ?? GetParameter("timeoutSeconds")
            ?? string.Empty;
        _timeoutMinutes = Action.TimeoutMinutes?.ToString(CultureInfo.InvariantCulture)
            ?? GetParameter("timeoutMinutes")
            ?? string.Empty;
        _titleEquals = Coalesce(Action.TitleEquals, GetParameter("titleEquals"));
        _titleContains = Coalesce(Action.TitleContains, GetParameter("titleContains"));
        _windowTitleContains = Coalesce(Action.WindowTitleContains, GetParameter("windowTitleContains"));
        _automationId = Coalesce(Action.AutomationId, GetParameter("automationId"));
        _nameEquals = Coalesce(Action.NameEquals, GetParameter("nameEquals"));
        _nameContains = Coalesce(Action.NameContains, GetParameter("nameContains"));
        _controlType = Coalesce(Action.ControlType, GetParameter("controlType"));

        if (IsClickButtonEditor && string.IsNullOrWhiteSpace(_controlType))
        {
            _controlType = "Button";
        }
    }

    private bool SetEditableProperty(ref string field, string value)
    {
        if (!SetProperty(ref field, value))
        {
            return false;
        }

        MarkDirty();
        return true;
    }

    private void MarkDirty()
    {
        _markDirty();
    }

    private bool IsType(string type)
    {
        return string.Equals(Type, type, StringComparison.OrdinalIgnoreCase);
    }

    private bool ValidateClickButtonSelectorOnly()
    {
        if (string.IsNullOrWhiteSpace(AutomationId) &&
            string.IsNullOrWhiteSpace(NameEquals) &&
            string.IsNullOrWhiteSpace(NameContains))
        {
            ValidationMessage = "clickButton 需要填写 automationId、nameEquals 或 nameContains。";
            return false;
        }

        ValidationMessage = string.Empty;
        return true;
    }

    private bool ValidatePositiveDoubleOrEmpty(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return true;
        }

        ValidationMessage = $"{fieldName} 必须是正数。";
        return false;
    }

    private bool ValidatePositiveIntOrEmpty(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return true;
        }

        ValidationMessage = $"{fieldName} 必须是正整数。";
        return false;
    }

    private UiButtonSearchCriteria CreateButtonCriteria()
    {
        return new UiButtonSearchCriteria
        {
            WindowTitleContains = WindowTitleContains.Trim(),
            AutomationId = AutomationId.Trim(),
            NameEquals = NameEquals.Trim(),
            NameContains = NameContains.Trim(),
            ControlType = string.IsNullOrWhiteSpace(ControlType) ? "Button" : ControlType.Trim()
        };
    }

    private void ApplyTimeoutSeconds()
    {
        Action.TimeoutSeconds = ParseNullableInt(TimeoutSeconds);
        RemoveParameter("timeoutSeconds");
        RemoveParameter("timeoutMilliseconds");
    }

    private void ApplyTimeoutMinutes()
    {
        Action.TimeoutMinutes = ParseNullableInt(TimeoutMinutes);
        RemoveParameter("timeoutMinutes");
    }

    private static int? ParseNullableInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private string? GetParameter(string key)
    {
        foreach (var pair in Action.Parameters)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private void SetParameter(string key, string value)
    {
        RemoveParameter(key);

        if (!string.IsNullOrWhiteSpace(value))
        {
            Action.Parameters[key] = value.Trim();
        }
    }

    private void RemoveParameter(string key)
    {
        var existing = Action.Parameters.Keys
            .FirstOrDefault(candidate => string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            Action.Parameters.Remove(existing);
        }
    }

    private bool CanTestSelector()
    {
        return IsClickButtonEditor &&
               (!string.IsNullOrWhiteSpace(AutomationId) ||
                !string.IsNullOrWhiteSpace(NameEquals) ||
                !string.IsNullOrWhiteSpace(NameContains));
    }

    private void RaiseSelectorCommandStates()
    {
        TestSelectorCommand.RaiseCanExecuteChanged();
        TestSelectorAndClickCommand.RaiseCanExecuteChanged();
    }

    private static string Coalesce(string direct, string? fallback)
    {
        return !string.IsNullOrWhiteSpace(direct) ? direct : fallback ?? string.Empty;
    }
}
