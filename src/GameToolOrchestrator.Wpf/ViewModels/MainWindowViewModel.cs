using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Core.Automation;
using GameToolOrchestrator.Core.Configuration;
using GameToolOrchestrator.Core.Models;
using GameToolOrchestrator.Core.Progress;
using GameToolOrchestrator.Wpf.Services;

namespace GameToolOrchestrator.Wpf.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IConfigRepository _configRepository;
    private readonly IExecutionEngineFactory _executionEngineFactory;
    private readonly IUiAutomationService _automationService;
    private readonly IConfigMutationService _mutationService;
    private readonly IDefaultConfigResolver _defaultConfigResolver;
    private readonly IUserConfirmationService? _confirmationService;
    private readonly IClipboardService? _clipboardService;
    private readonly IFolderLauncherService? _folderLauncherService;
    private readonly string _logsDirectory;
    private readonly SynchronizationContext? _synchronizationContext;
    private OrchestratorConfig? _config;
    private string _configPath = string.Empty;
    private string _statusMessage = "Ready";
    private ToolItemViewModel? _selectedTool;
    private TaskPlanItemViewModel? _selectedTaskPlan;
    private StepItemViewModel? _selectedStep;
    private ActionItemViewModel? _selectedAction;
    private ActionEditorViewModel? _selectedActionEditor;
    private bool _isExecuting;
    private string _currentTool = string.Empty;
    private string _currentAction = string.Empty;
    private string _diagnosticTitleContains = string.Empty;
    private string _diagnosticWindowTitleContains = string.Empty;
    private string _diagnosticAutomationId = string.Empty;
    private string _diagnosticNameContains = string.Empty;
    private string _selectedNewActionType = "waitSeconds";
    private bool _isDirty;
    private CancellationTokenSource? _executionCancellation;

    public MainWindowViewModel(
        IConfigRepository configRepository,
        IExecutionEngineFactory executionEngineFactory,
        IUiAutomationService automationService,
        IDefaultConfigResolver? defaultConfigResolver = null,
        IUserConfirmationService? confirmationService = null,
        IClipboardService? clipboardService = null,
        IFolderLauncherService? folderLauncherService = null,
        IConfigMutationService? mutationService = null,
        string? logsDirectory = null)
    {
        _configRepository = configRepository;
        _executionEngineFactory = executionEngineFactory;
        _automationService = automationService;
        _mutationService = mutationService ?? new ConfigMutationService();
        _defaultConfigResolver = defaultConfigResolver ?? new DefaultConfigResolver();
        _confirmationService = confirmationService;
        _clipboardService = clipboardService;
        _folderLauncherService = folderLauncherService;
        _logsDirectory = Path.GetFullPath(logsDirectory ?? "logs");
        _synchronizationContext = SynchronizationContext.Current;

        LoadConfigCommand = new AsyncRelayCommand(LoadConfigAsync, CanLoadConfig);
        SaveConfigCommand = new AsyncRelayCommand(SaveConfigAsync, CanSaveConfig);
        RefreshConfigCommand = new AsyncRelayCommand(LoadConfigAsync, CanLoadConfig);
        StartExecutionCommand = new AsyncRelayCommand(StartExecutionAsync, () => _config is not null && SelectedTaskPlan is not null && !IsExecuting);
        CancelExecutionCommand = new RelayCommand(CancelExecution, () => IsExecuting);
        RefreshWindowsCommand = new AsyncRelayCommand(RefreshWindowsAsync);
        ExportControlTreeCommand = new AsyncRelayCommand(ExportControlTreeAsync, () => !string.IsNullOrWhiteSpace(DiagnosticTitleContains));
        TestSelectorCommand = new AsyncRelayCommand(() => TestSelectorAsync(click: false), CanTestSelector);
        TestSelectorAndClickCommand = new AsyncRelayCommand(() => TestSelectorAsync(click: true), CanTestSelector);
        CopyDiagnosticsCommand = new RelayCommand(CopyDiagnostics);
        OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);
        OpenConfigFolderCommand = new RelayCommand(OpenConfigFolder, CanOpenConfigFolder);
        AddToolCommand = new RelayCommand(AddTool, CanMutateConfig);
        CopyToolCommand = new RelayCommand(CopyTool, () => CanMutateConfig() && SelectedTool is not null);
        DeleteToolCommand = new RelayCommand(DeleteTool, () => CanMutateConfig() && SelectedTool is not null);
        AddTaskPlanCommand = new RelayCommand(AddTaskPlan, CanMutateConfig);
        CopyTaskPlanCommand = new RelayCommand(CopyTaskPlan, () => CanMutateConfig() && SelectedTaskPlan is not null);
        DeleteTaskPlanCommand = new RelayCommand(DeleteTaskPlan, () => CanMutateConfig() && SelectedTaskPlan is not null);
        AddStepCommand = new RelayCommand(AddStep, () => CanMutateConfig() && SelectedTaskPlan is not null);
        CopyStepCommand = new RelayCommand(CopyStep, () => CanMutateConfig() && SelectedTaskPlan is not null && SelectedStep is not null);
        DeleteStepCommand = new RelayCommand(DeleteStep, () => CanMutateConfig() && SelectedTaskPlan is not null && SelectedStep is not null);
        NormalizeStepOrderCommand = new RelayCommand(NormalizeStepOrder, () => CanMutateConfig() && SelectedTaskPlan is not null);
        AddActionCommand = new RelayCommand(AddAction, () => CanMutateConfig() && SelectedStep is not null);
        CopyActionCommand = new RelayCommand(CopyAction, () => CanMutateConfig() && SelectedStep is not null && SelectedAction is not null);
        DeleteActionCommand = new RelayCommand(DeleteAction, () => CanMutateConfig() && SelectedStep is not null && SelectedAction is not null);
    }

    public ObservableCollection<ToolItemViewModel> Tools { get; } = [];

    public ObservableCollection<TaskPlanItemViewModel> TaskPlans { get; } = [];

    public ObservableCollection<StepItemViewModel> Steps { get; } = [];

    public ObservableCollection<ActionItemViewModel> Actions { get; } = [];

    public ObservableCollection<LogEntryViewModel> Logs { get; } = [];

    public ObservableCollection<string> VisibleWindowTitles { get; } = [];

    public IReadOnlyList<string> SupportedActionTypes { get; } =
    [
        "waitSeconds",
        "waitProcessExit",
        "waitWindow",
        "clickButton"
    ];

    public AsyncRelayCommand LoadConfigCommand { get; }

    public AsyncRelayCommand SaveConfigCommand { get; }

    public AsyncRelayCommand RefreshConfigCommand { get; }

    public AsyncRelayCommand StartExecutionCommand { get; }

    public RelayCommand CancelExecutionCommand { get; }

    public AsyncRelayCommand RefreshWindowsCommand { get; }

    public AsyncRelayCommand ExportControlTreeCommand { get; }

    public AsyncRelayCommand TestSelectorCommand { get; }

    public AsyncRelayCommand TestSelectorAndClickCommand { get; }

    public RelayCommand CopyDiagnosticsCommand { get; }

    public RelayCommand OpenLogsFolderCommand { get; }

    public RelayCommand OpenConfigFolderCommand { get; }

    public RelayCommand AddToolCommand { get; }

    public RelayCommand CopyToolCommand { get; }

    public RelayCommand DeleteToolCommand { get; }

    public RelayCommand AddTaskPlanCommand { get; }

    public RelayCommand CopyTaskPlanCommand { get; }

    public RelayCommand DeleteTaskPlanCommand { get; }

    public RelayCommand AddStepCommand { get; }

    public RelayCommand CopyStepCommand { get; }

    public RelayCommand DeleteStepCommand { get; }

    public RelayCommand NormalizeStepOrderCommand { get; }

    public RelayCommand AddActionCommand { get; }

    public RelayCommand CopyActionCommand { get; }

    public RelayCommand DeleteActionCommand { get; }

    public string WindowTitle => IsDirty ? "GameToolOrchestrator - \u672a\u4fdd\u5b58" : "GameToolOrchestrator";

    public string DirtyStatusText => IsDirty ? "\u672a\u4fdd\u5b58" : string.Empty;

    public bool CanBrowseConfig => !IsExecuting;

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(DirtyStatusText));
                SaveConfigCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ConfigPath
    {
        get => _configPath;
        set
        {
            if (SetProperty(ref _configPath, value))
            {
                OpenConfigFolderCommand.RaiseCanExecuteChanged();
                RaiseCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ToolItemViewModel? SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (SetProperty(ref _selectedTool, value))
            {
                OnPropertyChanged(nameof(HasSelectedTool));
                RaiseCommandStates();
            }
        }
    }

    public bool HasSelectedTool => SelectedTool is not null;

    public TaskPlanItemViewModel? SelectedTaskPlan
    {
        get => _selectedTaskPlan;
        set
        {
            if (SetProperty(ref _selectedTaskPlan, value))
            {
                PopulateSteps();
                RaiseCommandStates();
            }
        }
    }

    public StepItemViewModel? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (SetProperty(ref _selectedStep, value))
            {
                OnPropertyChanged(nameof(HasSelectedStep));
                SelectToolForStep(value);
                PopulateActions();
                RaiseCommandStates();
            }
        }
    }

    public bool HasSelectedStep => SelectedStep is not null;

    public ActionItemViewModel? SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (SetProperty(ref _selectedAction, value))
            {
                SelectedActionEditor = value?.Editor;
                RaiseCommandStates();
            }
        }
    }

    public ActionEditorViewModel? SelectedActionEditor
    {
        get => _selectedActionEditor;
        private set => SetProperty(ref _selectedActionEditor, value);
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (SetProperty(ref _isExecuting, value))
            {
                OnPropertyChanged(nameof(CanBrowseConfig));
                RaiseCommandStates();
            }
        }
    }

    public string CurrentTool
    {
        get => _currentTool;
        private set => SetProperty(ref _currentTool, value);
    }

    public string CurrentAction
    {
        get => _currentAction;
        private set => SetProperty(ref _currentAction, value);
    }

    public string DiagnosticTitleContains
    {
        get => _diagnosticTitleContains;
        set
        {
            if (SetProperty(ref _diagnosticTitleContains, value))
            {
                ExportControlTreeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string DiagnosticWindowTitleContains
    {
        get => _diagnosticWindowTitleContains;
        set
        {
            if (SetProperty(ref _diagnosticWindowTitleContains, value))
            {
                RaiseDiagnosticCommandStates();
            }
        }
    }

    public string DiagnosticAutomationId
    {
        get => _diagnosticAutomationId;
        set
        {
            if (SetProperty(ref _diagnosticAutomationId, value))
            {
                RaiseDiagnosticCommandStates();
            }
        }
    }

    public string DiagnosticNameContains
    {
        get => _diagnosticNameContains;
        set
        {
            if (SetProperty(ref _diagnosticNameContains, value))
            {
                RaiseDiagnosticCommandStates();
            }
        }
    }

    public string SelectedNewActionType
    {
        get => _selectedNewActionType;
        set => SetProperty(ref _selectedNewActionType, value);
    }

    public void SetDefaultConfigPath()
    {
        var result = _defaultConfigResolver.FindDefaultConfig();
        ConfigPath = result.FoundPath ?? result.CandidatePaths.FirstOrDefault() ?? string.Empty;
    }

    public async Task InitializeDefaultConfigAsync(Action<string>? startupLog = null)
    {
        var result = _defaultConfigResolver.FindDefaultConfig();

        foreach (var candidate in result.CandidatePaths)
        {
            startupLog?.Invoke($"尝试查找配置: {candidate}; exists={File.Exists(candidate)}");
        }

        if (string.IsNullOrWhiteSpace(result.FoundPath))
        {
            ConfigPath = result.CandidatePaths.FirstOrDefault() ?? string.Empty;
            ClearLoadedConfig();
            const string message = "未找到默认配置文件，请手动选择 config.json。";
            StatusMessage = message;
            AddLog("Warning", message);
            startupLog?.Invoke("是否找到默认配置: false");
            startupLog?.Invoke("默认配置是否加载成功: false; reason=not found");
            RaiseCommandStates();
            return;
        }

        ConfigPath = result.FoundPath;
        startupLog?.Invoke($"是否找到默认配置: true; path={result.FoundPath}");
        await LoadConfigAsync(startupLog);
    }

    public async Task LoadConfigAsync()
    {
        if (!ConfirmDiscardUnsavedChanges("\u91cd\u65b0\u52a0\u8f7d\u914d\u7f6e"))
        {
            return;
        }

        await LoadConfigAsync(startupLog: null);
    }

    public async Task<bool> LoadConfigFromPathAsync(string path, bool confirmUnsavedChanges = true)
    {
        if (IsExecuting)
        {
            const string message = "\u6b63\u5728\u6267\u884c\u4efb\u52a1\uff0c\u4e0d\u80fd\u5207\u6362\u914d\u7f6e\u6587\u4ef6\u3002";
            StatusMessage = message;
            AddLog("Warning", message);
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            const string message = "\u8bf7\u5148\u9009\u62e9 config.json\u3002";
            StatusMessage = message;
            AddLog("Warning", message);
            return false;
        }

        if (confirmUnsavedChanges && !ConfirmDiscardUnsavedChanges("\u5207\u6362\u914d\u7f6e"))
        {
            return false;
        }

        ConfigPath = path;
        await LoadConfigAsync(startupLog: null);
        return _config is not null;
    }

    public async Task LoadConfigAsync(Action<string>? startupLog)
    {
        if (string.IsNullOrWhiteSpace(ConfigPath))
        {
            SetDefaultConfigPath();
        }

        if (string.IsNullOrWhiteSpace(ConfigPath))
        {
            ClearLoadedConfig();
            const string message = "未找到默认配置文件，请手动选择 config.json。";
            StatusMessage = message;
            AddLog("Warning", message);
            startupLog?.Invoke("默认配置是否加载成功: false; reason=no config path");
            RaiseCommandStates();
            return;
        }

        try
        {
            _config = await _configRepository.LoadAsync(ConfigPath);
            PopulateTools();
            PopulateTaskPlans();
            IsDirty = false;
            StatusMessage = $"Loaded {ConfigPath}";
            AddLog("Information", $"Loaded config: {ConfigPath}");
            startupLog?.Invoke($"默认配置是否加载成功: true; path={ConfigPath}");
        }
        catch (Exception exception)
        {
            ClearLoadedConfig();
            StatusMessage = "Failed to load config.";
            AddLog("Error", $"Failed to load config: {exception.Message}");
            startupLog?.Invoke($"默认配置是否加载成功: false; reason={exception.GetType().FullName}: {exception.Message}");
        }

        RaiseCommandStates();
    }

    public async Task SaveConfigAsync()
    {
        if (_config is null)
        {
            AddLog("Warning", "No loaded config to save.");
            StatusMessage = "No loaded config to save.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ConfigPath))
        {
            AddLog("Warning", "Please choose a config path before saving.");
            StatusMessage = "No config path selected.";
            return;
        }

        if (IsExecuting)
        {
            AddLog("Warning", "Cannot save config while execution is running.");
            StatusMessage = "Cannot save while running.";
            return;
        }

        if (!ValidateAndCommitActionEditors())
        {
            return;
        }

        try
        {
            await _configRepository.SaveAsync(ConfigPath, _config);
            IsDirty = false;
            StatusMessage = $"Saved {ConfigPath}";
            AddLog("Information", $"Saved config: {ConfigPath}");
        }
        catch (Exception exception)
        {
            StatusMessage = "Failed to save config.";
            AddLog("Error", $"Failed to save config: {exception.Message}");
            AddLog("Warning", "Next step: check whether the config file is read-only, locked by another program, or blocked by permissions.");
        }
    }

    public async Task StartExecutionAsync()
    {
        if (_config is null || SelectedTaskPlan is null)
        {
            AddLog("Warning", "没有有效配置或未选择 TaskPlan，无法开始执行。");
            StatusMessage = "No valid task plan selected.";
            return;
        }

        _executionCancellation = new CancellationTokenSource();
        IsExecuting = true;
        ResetStepStatuses();
        StatusMessage = $"Running {SelectedTaskPlan.Id}";
        AddLog("Information", $"Starting task plan {SelectedTaskPlan.Id}.");

        try
        {
            var reporter = new ViewModelProgressReporter(OnProgress);
            var engine = _executionEngineFactory.Create(reporter);
            var result = await engine.ExecuteAsync(_config, SelectedTaskPlan.Id, _executionCancellation.Token);
            ApplyResult(result);
            AppendExecutionSummary(result);
            AppendFailureAdvice(result);
            StatusMessage = $"Finished: {result.Status}";
            AddLog(result.Status == ExecutionStatus.Succeeded ? "Information" : "Error", $"Task plan finished with {result.Status}.");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled";
            AddLog("Warning", "Execution cancelled.");
        }
        catch (Exception exception)
        {
            StatusMessage = "Execution failed.";
            AddLog("Error", $"Execution failed: {exception.Message}");
        }
        finally
        {
            IsExecuting = false;
            CurrentTool = string.Empty;
            CurrentAction = string.Empty;
            _executionCancellation.Dispose();
            _executionCancellation = null;
        }
    }

    public void CancelExecution()
    {
        if (!IsExecuting)
        {
            return;
        }

        AddLog("Warning", "User requested cancellation.");
        _executionCancellation?.Cancel();
    }

    public async Task RefreshWindowsAsync()
    {
        try
        {
            var windows = await _automationService.GetVisibleWindowsAsync(CancellationToken.None);
            VisibleWindowTitles.Clear();

            foreach (var window in windows)
            {
                var title = string.IsNullOrWhiteSpace(window.Title) ? "<empty title>" : window.Title;
                VisibleWindowTitles.Add(title);
                AddLog("Information", $"Window: {title} process={window.ProcessName} pid={window.ProcessId?.ToString() ?? "unknown"}");
            }
        }
        catch (Exception exception)
        {
            AddLog("Error", $"Failed to refresh windows: {exception.Message}");
        }
    }

    public async Task ExportControlTreeAsync()
    {
        try
        {
            var tree = await _automationService.GetControlTreeSummaryAsync(
                new UiWindowSearchCriteria { TitleContains = DiagnosticTitleContains },
                maxDepth: 4,
                CancellationToken.None);

            AddLog("Information", $"Control tree for titleContains='{DiagnosticTitleContains}':");
            AddLog("Information", tree);
        }
        catch (Exception exception)
        {
            AddLog("Error", $"Failed to export control tree: {exception.Message}");
        }
    }

    public async Task TestSelectorAsync(bool click)
    {
        var criteria = new UiButtonSearchCriteria
        {
            WindowTitleContains = DiagnosticWindowTitleContains,
            AutomationId = DiagnosticAutomationId,
            NameContains = DiagnosticNameContains,
            ControlType = "Button"
        };

        try
        {
            var result = await _automationService.FindElementsAsync(criteria, diagnosticMaxDepth: 4, CancellationToken.None);
            AddLog("Information", $"Selector matched {result.Elements.Count} controls.");

            foreach (var element in result.Elements)
            {
                AddLog("Information", $"[{element.Index}] Name='{element.Name}', AutomationId='{element.AutomationId}', ControlType='{element.ControlType}', Enabled={element.IsEnabled}, Offscreen={element.IsOffscreen}");
            }

            if (!click)
            {
                AddLog("Information", "Selector test completed without clicking.");
                return;
            }

            if (result.Elements.Count == 0)
            {
                AddLog("Warning", "No control matched; click was not attempted.");
                return;
            }

            AddLog("Warning", $"About to click [{result.Elements[0].Index}] {result.Elements[0].Name}.");
            var clickResult = await _automationService.ClickButtonAsync(criteria, TimeSpan.FromSeconds(10), 4, CancellationToken.None);
            AddLog(clickResult.Status == ExecutionStatus.Succeeded ? "Information" : "Error", $"Click result: {clickResult.Status}. {clickResult.ErrorMessage}");
        }
        catch (Exception exception)
        {
            AddLog("Error", $"Selector test failed: {exception.Message}");
        }
    }

    public void CopyDiagnostics()
    {
        try
        {
            if (_clipboardService is null)
            {
                AddLog("Error", "Clipboard service is not available.");
                return;
            }

            var text = BuildDiagnosticInfo();
            _clipboardService.CopyText(text);
            AddLog("Information", "Diagnostic information copied to clipboard.");
        }
        catch (Exception exception)
        {
            AddLog("Error", $"Failed to copy diagnostic information: {exception.Message}");
        }
    }

    public string BuildDiagnosticInfo()
    {
        var version = typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString() ?? "<unknown>";
        var builder = new StringBuilder();

        builder.AppendLine("GameToolOrchestrator diagnostics");
        builder.AppendLine($"GeneratedAt: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");
        builder.AppendLine($"Version: {version}");
        builder.AppendLine($"ConfigPath: {ValueOrNone(ConfigPath)}");
        builder.AppendLine($"CurrentDirectory: {Environment.CurrentDirectory}");
        builder.AppendLine($"AppContext.BaseDirectory: {AppContext.BaseDirectory}");
        builder.AppendLine($"TaskPlan: {SelectedTaskPlan?.Id ?? "<none>"} / {SelectedTaskPlan?.Name ?? "<none>"}");
        builder.AppendLine($"Step: {SelectedStep?.Id ?? "<none>"} / toolId={SelectedStep?.ToolId ?? "<none>"} / toolName={SelectedStep?.ToolName ?? "<none>"}");
        builder.AppendLine($"Action: index={SelectedAction?.Index.ToString() ?? "<none>"} / type={SelectedAction?.Type ?? "<none>"} / summary={SelectedAction?.Summary ?? "<none>"}");
        builder.AppendLine($"IsDirty: {IsDirty}");
        builder.AppendLine($"IsExecuting: {IsExecuting}");
        builder.AppendLine();
        builder.AppendLine("Logs:");

        if (Logs.Count == 0)
        {
            builder.AppendLine("<empty>");
        }
        else
        {
            foreach (var log in Logs)
            {
                builder.AppendLine(log.ToString());
            }
        }

        return builder.ToString();
    }

    public void OpenLogsFolder()
    {
        try
        {
            if (_folderLauncherService is null)
            {
                AddLog("Error", "Folder launcher service is not available.");
                return;
            }

            Directory.CreateDirectory(_logsDirectory);
            _folderLauncherService.OpenFolder(_logsDirectory);
            AddLog("Information", $"Opened logs folder: {_logsDirectory}");
        }
        catch (Exception exception)
        {
            AddLog("Error", $"Failed to open logs folder: {exception.Message}");
        }
    }

    public void OpenConfigFolder()
    {
        try
        {
            if (_config is null || string.IsNullOrWhiteSpace(ConfigPath))
            {
                AddLog("Warning", "No loaded config file to open.");
                return;
            }

            if (_folderLauncherService is null)
            {
                AddLog("Error", "Folder launcher service is not available.");
                return;
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(ConfigPath));
            if (string.IsNullOrWhiteSpace(directory))
            {
                AddLog("Warning", "Config path does not have a containing folder.");
                return;
            }

            _folderLauncherService.OpenFolder(directory);
            AddLog("Information", $"Opened config folder: {directory}");
        }
        catch (Exception exception)
        {
            AddLog("Error", $"Failed to open config folder: {exception.Message}");
        }
    }

    public void AddTool()
    {
        if (!EnsureCanMutateConfig())
        {
            return;
        }

        var tool = _mutationService.AddTool(_config!);
        PopulateTools(tool.Id);
        MarkDirty();
        AddLog("Information", $"Added tool {tool.Id}.");
    }

    public void CopyTool()
    {
        if (!EnsureCanMutateConfig() || SelectedTool is null)
        {
            return;
        }

        var sourceId = SelectedTool.Id;
        var tool = _mutationService.CopyTool(_config!, SelectedTool.Tool);
        PopulateTools(tool.Id);
        MarkDirty();
        AddLog("Information", $"Copied tool {sourceId} to {tool.Id}.");
    }

    public void DeleteTool()
    {
        if (!EnsureCanMutateConfig() || SelectedTool is null)
        {
            return;
        }

        var tool = SelectedTool.Tool;
        var references = _mutationService.FindToolReferences(_config!, tool.Id);
        if (references.Count > 0)
        {
            AddLog("Error", $"Tool '{tool.Id}' cannot be deleted because it is referenced by: {string.Join(", ", references)}");
            StatusMessage = "Delete tool blocked.";
            return;
        }

        if (!ConfirmDelete($"tool '{tool.Id}'"))
        {
            return;
        }

        var result = _mutationService.DeleteTool(_config!, tool);
        if (!result.Succeeded)
        {
            AddLog("Error", result.ErrorMessage);
            StatusMessage = "Delete tool blocked.";
            return;
        }

        PopulateTools();
        MarkDirty();
        AddLog("Information", $"Deleted tool {tool.Id}.");
    }

    public void AddTaskPlan()
    {
        if (!EnsureCanMutateConfig())
        {
            return;
        }

        var plan = _mutationService.AddTaskPlan(_config!);
        PopulateTaskPlans();
        SelectTaskPlanById(plan.Id);
        MarkDirty();
        AddLog("Information", $"Added task plan {plan.Id}.");
    }

    public void CopyTaskPlan()
    {
        if (!EnsureCanMutateConfig() || SelectedTaskPlan is null)
        {
            return;
        }

        var plan = _mutationService.CopyTaskPlan(_config!, SelectedTaskPlan.Plan);
        PopulateTaskPlans();
        SelectTaskPlanById(plan.Id);
        MarkDirty();
        AddLog("Information", $"Copied task plan to {plan.Id}.");
    }

    public void DeleteTaskPlan()
    {
        if (!EnsureCanMutateConfig() || SelectedTaskPlan is null)
        {
            return;
        }

        var plan = SelectedTaskPlan.Plan;
        if (!ConfirmDelete($"task plan '{plan.Id}'"))
        {
            return;
        }

        var result = _mutationService.DeleteTaskPlan(_config!, plan);
        if (!result.Succeeded)
        {
            AddLog("Error", result.ErrorMessage);
            return;
        }

        PopulateTaskPlans();
        MarkDirty();
        AddLog("Information", $"Deleted task plan {plan.Id}.");
    }

    public void AddStep()
    {
        if (!EnsureCanMutateConfig() || SelectedTaskPlan is null)
        {
            return;
        }

        if (SelectedTool is null)
        {
            AddLog("Error", "Cannot add step because there is no selected tool. Add or select a tool first.");
            return;
        }

        var step = _mutationService.AddStep(_config!, SelectedTaskPlan.Plan, SelectedTool.Tool);
        if (step is null)
        {
            AddLog("Error", "Cannot add step because there is no tool.");
            return;
        }

        PopulateSteps();
        SelectStepById(step.Id);
        MarkDirty();
        AddLog("Information", $"Added step {step.Id} using tool {step.ToolId}.");
    }

    public void CopyStep()
    {
        if (!EnsureCanMutateConfig() || SelectedTaskPlan is null || SelectedStep is null)
        {
            return;
        }

        var step = _mutationService.CopyStep(SelectedTaskPlan.Plan, SelectedStep.Step);
        PopulateSteps();
        SelectStepById(step.Id);
        MarkDirty();
        AddLog("Information", $"Copied step to {step.Id}.");
    }

    public void DeleteStep()
    {
        if (!EnsureCanMutateConfig() || SelectedTaskPlan is null || SelectedStep is null)
        {
            return;
        }

        var step = SelectedStep.Step;
        if (!ConfirmDelete($"step '{step.Id}'"))
        {
            return;
        }

        var result = _mutationService.DeleteStep(SelectedTaskPlan.Plan, step);
        if (!result.Succeeded)
        {
            AddLog("Error", result.ErrorMessage);
            return;
        }

        PopulateSteps();
        MarkDirty();
        AddLog("Information", $"Deleted step {step.Id}.");
    }

    public void NormalizeStepOrder()
    {
        if (!EnsureCanMutateConfig() || SelectedTaskPlan is null)
        {
            return;
        }

        _mutationService.NormalizeStepOrder(SelectedTaskPlan.Plan);
        PopulateSteps();
        MarkDirty();
        AddLog("Information", $"Normalized step order for task plan {SelectedTaskPlan.Id}.");
    }

    public void AddAction()
    {
        if (!EnsureCanMutateConfig() || SelectedStep is null)
        {
            return;
        }

        var action = _mutationService.AddAction(SelectedStep.Step, SelectedNewActionType);
        if (action is null)
        {
            AddLog("Error", $"Unsupported action type '{SelectedNewActionType}'.");
            return;
        }

        PopulateActions();
        SelectActionById(action.Id);
        MarkDirty();
        AddLog("Information", $"Added action {action.Id} ({action.Type}).");
    }

    public void CopyAction()
    {
        if (!EnsureCanMutateConfig() || SelectedStep is null || SelectedAction is null)
        {
            return;
        }

        var action = _mutationService.CopyAction(SelectedStep.Step, SelectedAction.Action);
        PopulateActions();
        SelectActionById(action.Id);
        MarkDirty();
        AddLog("Information", $"Copied action to {action.Id}. Copy is appended to the end of the action list.");
    }

    public void DeleteAction()
    {
        if (!EnsureCanMutateConfig() || SelectedStep is null || SelectedAction is null)
        {
            return;
        }

        var action = SelectedAction.Action;
        if (!ConfirmDelete($"action '{action.Id}'"))
        {
            return;
        }

        var result = _mutationService.DeleteAction(SelectedStep.Step, action);
        if (!result.Succeeded)
        {
            AddLog("Error", result.ErrorMessage);
            return;
        }

        PopulateActions();
        MarkDirty();
        AddLog("Information", $"Deleted action {action.Id}.");
    }

    public void AddLog(string level, string message, string stepId = "", string toolId = "", string actionId = "")
    {
        PostToUi(() =>
        {
            Logs.Add(new LogEntryViewModel
            {
                Timestamp = DateTimeOffset.Now,
                Level = level,
                StepId = stepId,
                ToolId = toolId,
                ActionId = actionId,
                Message = message
            });
        });
    }

    private void OnProgress(ExecutionProgressEvent progress)
    {
        PostToUi(() =>
        {
            if (progress.EventType == ExecutionProgressEventType.StepStatusChanged)
            {
                var step = Steps.FirstOrDefault(item => item.Id == progress.StepId);
                if (step is not null)
                {
                    step.Status = progress.Status;
                    if (progress.Status is ExecutionStatus.Failed or ExecutionStatus.TimedOut or ExecutionStatus.Cancelled)
                    {
                        step.LastError = progress.Message;
                    }

                    CurrentTool = step.ToolName;
                }
            }

            if (progress.EventType == ExecutionProgressEventType.ActionStatusChanged)
            {
                CurrentAction = $"{progress.ActionId} ({progress.ActionType})";
            }

            Logs.Add(new LogEntryViewModel
            {
                Timestamp = DateTimeOffset.Now,
                Level = progress.Level,
                StepId = progress.StepId,
                ToolId = progress.ToolId,
                ActionId = progress.ActionId,
                Message = progress.Message
            });
        });
    }

    private void PopulateTaskPlans()
    {
        TaskPlans.Clear();
        Steps.Clear();
        Actions.Clear();

        if (_config is null)
        {
            return;
        }

        foreach (var plan in _config.TaskPlans)
        {
            TaskPlans.Add(new TaskPlanItemViewModel(plan));
        }

        SelectedTaskPlan = TaskPlans.FirstOrDefault();
    }

    private void SelectTaskPlanById(string taskPlanId)
    {
        SelectedTaskPlan = TaskPlans.FirstOrDefault(plan =>
            string.Equals(plan.Id, taskPlanId, StringComparison.OrdinalIgnoreCase));
    }

    private void PopulateTools(string selectedToolId = "")
    {
        Tools.Clear();

        if (_config is null)
        {
            SelectedTool = null;
            return;
        }

        foreach (var tool in _config.Tools)
        {
            var toolViewModel = new ToolItemViewModel(tool);
            toolViewModel.PropertyChanged += OnToolPropertyChanged;
            Tools.Add(toolViewModel);
        }

        SelectedTool = !string.IsNullOrWhiteSpace(selectedToolId)
            ? Tools.FirstOrDefault(tool => string.Equals(tool.Id, selectedToolId, StringComparison.OrdinalIgnoreCase)) ?? Tools.FirstOrDefault()
            : Tools.FirstOrDefault();
    }

    private void ClearLoadedConfig()
    {
        _config = null;
        Tools.Clear();
        TaskPlans.Clear();
        Steps.Clear();
        Actions.Clear();
        SelectedTaskPlan = null;
        SelectedStep = null;
        SelectedAction = null;
        SelectedTool = null;
        CurrentTool = string.Empty;
        CurrentAction = string.Empty;
        IsDirty = false;
    }

    private void PopulateSteps()
    {
        Steps.Clear();
        Actions.Clear();

        if (_config is null || SelectedTaskPlan is null)
        {
            return;
        }

        foreach (var step in SelectedTaskPlan.Plan.Steps.OrderBy(step => step.Order))
        {
            var stepViewModel = new StepItemViewModel(step, _config.FindTool(step.ToolId));
            stepViewModel.PropertyChanged += OnStepPropertyChanged;
            Steps.Add(stepViewModel);
        }

        SelectedStep = Steps.FirstOrDefault();
    }

    private void SelectStepById(string stepId)
    {
        SelectedStep = Steps.FirstOrDefault(step =>
            string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase));
    }

    private void PopulateActions()
    {
        Actions.Clear();
        SelectedAction = null;

        if (SelectedStep is null)
        {
            return;
        }

        var index = 0;
        foreach (var action in SelectedStep.Step.Actions.OrderBy(action => action.Order))
        {
            var actionViewModel = new ActionItemViewModel(action, index++);
            actionViewModel.Editor = new ActionEditorViewModel(
                actionViewModel,
                _automationService,
                MarkDirty,
                (level, message) => AddLog(level, message));
            actionViewModel.PropertyChanged += OnActionPropertyChanged;
            Actions.Add(actionViewModel);
        }

        SelectedAction = Actions.FirstOrDefault();
    }

    private void SelectActionById(string actionId)
    {
        SelectedAction = Actions.FirstOrDefault(action =>
            string.Equals(action.Id, actionId, StringComparison.OrdinalIgnoreCase));
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StepItemViewModel.Enabled) or
            nameof(StepItemViewModel.ExecutablePath) or
            nameof(StepItemViewModel.WorkingDirectory))
        {
            IsDirty = true;
        }
    }

    private void OnToolPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ToolItemViewModel.ExecutablePath) or
            nameof(ToolItemViewModel.WorkingDirectory))
        {
            IsDirty = true;
        }
    }

    private void OnActionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActionItemViewModel.Enabled))
        {
            IsDirty = true;
        }
    }

    private void MarkDirty()
    {
        IsDirty = true;
    }

    private void SelectToolForStep(StepItemViewModel? step)
    {
        if (step is null)
        {
            return;
        }

        var matchingTool = Tools.FirstOrDefault(tool =>
            string.Equals(tool.Id, step.ToolId, StringComparison.OrdinalIgnoreCase));

        if (matchingTool is not null)
        {
            SelectedTool = matchingTool;
        }
    }

    private void ResetStepStatuses()
    {
        foreach (var step in Steps)
        {
            step.Status = ExecutionStatus.Pending;
            step.LastError = string.Empty;
        }
    }

    private void ApplyResult(ExecutionResult result)
    {
        var task = result.Tasks.FirstOrDefault();
        if (task is null)
        {
            return;
        }

        foreach (var stepResult in task.Steps)
        {
            var step = Steps.FirstOrDefault(item => item.Id == stepResult.StepId);
            if (step is null)
            {
                continue;
            }

            step.Status = stepResult.Status;
            step.LastError = stepResult.ErrorMessage ?? string.Empty;
        }
    }

    private bool ValidateAndCommitActionEditors()
    {
        foreach (var editor in Actions.Select(action => action.Editor).OfType<ActionEditorViewModel>())
        {
            if (editor.ValidateAndCommit())
            {
                continue;
            }

            SelectedAction = Actions.FirstOrDefault(action => ReferenceEquals(action.Editor, editor));
            StatusMessage = "Action editor validation failed.";
            AddLog("Error", $"Action editor validation failed: {editor.ValidationMessage}");
            return false;
        }

        return true;
    }

    private void AppendExecutionSummary(ExecutionResult result)
    {
        var task = result.Tasks.FirstOrDefault();
        var steps = task?.Steps ?? [];
        var taskPlanId = task?.TaskPlanId ?? SelectedTaskPlan?.Id ?? result.TaskPlanId;
        var taskPlanName = ValueOrNone(task?.TaskPlanName ?? SelectedTaskPlan?.Name);
        var completedAt = result.CompletedAt ?? DateTimeOffset.UtcNow;
        var elapsed = completedAt - result.StartedAt;
        var total = steps.Count;
        var succeeded = steps.Count(step => step.Status == ExecutionStatus.Succeeded);
        var skipped = steps.Count(step => step.Status == ExecutionStatus.Skipped);
        var failed = steps.Count(step => step.Status == ExecutionStatus.Failed);
        var timedOut = steps.Count(step => step.Status == ExecutionStatus.TimedOut);
        var cancelled = result.Status == ExecutionStatus.Cancelled || steps.Any(step => step.Status == ExecutionStatus.Cancelled);
        var firstProblem = steps.FirstOrDefault(step => step.Status is ExecutionStatus.Failed or ExecutionStatus.TimedOut or ExecutionStatus.Cancelled);

        AddLog("Information", "Execution summary:");
        AddLog("Information", $"TaskPlan: {taskPlanId} / {taskPlanName}; Status={result.Status}; Elapsed={FormatDuration(elapsed)}");
        AddLog("Information", $"Steps: total={total}, succeeded={succeeded}, skipped={skipped}, failed={failed}, timedOut={timedOut}, cancelled={cancelled}");

        if (firstProblem is not null)
        {
            AddLog(
                "Error",
                $"First problem step: {ValueOrNone(firstProblem.StepId)} ({ValueOrNone(firstProblem.StepName)}), status={firstProblem.Status}, error={ValueOrNone(firstProblem.ErrorMessage)}");
        }
    }

    private void AppendFailureAdvice(ExecutionResult result)
    {
        if (result.Status == ExecutionStatus.Succeeded)
        {
            return;
        }

        var firstFailure = FindFirstProblemAction(result);
        var combinedMessage = string.Join(
            " ",
            new[]
            {
                result.ErrorMessage,
                firstFailure.Step?.ErrorMessage,
                firstFailure.Action?.ErrorMessage
            }.Where(message => !string.IsNullOrWhiteSpace(message)));

        if (LooksLikePermissionError(combinedMessage))
        {
            AddLog("Warning", "Next step: check that GameToolOrchestrator and the target tool run at the same permission level. If the tool is elevated, run the orchestrator as administrator.");
        }

        var actionType = firstFailure.Action?.ActionType ?? string.Empty;
        if (string.Equals(actionType, "waitWindow", StringComparison.OrdinalIgnoreCase))
        {
            AddLog("Warning", "Next step: waitWindow failed. Use Refresh Window List to confirm the window title, then update titleEquals/titleContains.");
            return;
        }

        if (string.Equals(actionType, "clickButton", StringComparison.OrdinalIgnoreCase))
        {
            AddLog("Warning", "Next step: clickButton failed. Use Export Control Tree and Test Selector to calibrate automationId/nameEquals/nameContains.");
            return;
        }

        if (string.Equals(actionType, "waitProcessExit", StringComparison.OrdinalIgnoreCase) &&
            (firstFailure.Action?.Status == ExecutionStatus.TimedOut || firstFailure.Step?.Status == ExecutionStatus.TimedOut))
        {
            AddLog("Warning", "Next step: waitProcessExit timed out. Check whether the target tool exits automatically, or increase timeoutSeconds/timeoutMinutes.");
            return;
        }

        if (result.Status == ExecutionStatus.TimedOut)
        {
            AddLog("Warning", "Next step: execution timed out. Check task/action timeout settings and whether the target tool is still running.");
        }
    }

    private static (StepExecutionResult? Step, ActionExecutionResult? Action) FindFirstProblemAction(ExecutionResult result)
    {
        foreach (var task in result.Tasks)
        {
            foreach (var step in task.Steps)
            {
                var action = step.Actions.FirstOrDefault(actionResult =>
                    actionResult.Status is ExecutionStatus.Failed or ExecutionStatus.TimedOut or ExecutionStatus.Cancelled);

                if (action is not null)
                {
                    return (step, action);
                }

                if (step.Status is ExecutionStatus.Failed or ExecutionStatus.TimedOut or ExecutionStatus.Cancelled)
                {
                    return (step, null);
                }
            }
        }

        return (null, null);
    }

    private static bool LooksLikePermissionError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("administrator", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("elevated", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("\u6743\u9650", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("\u62d2\u7edd\u8bbf\u95ee", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }

    private static string ValueOrNone(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<none>" : value;
    }

    private bool CanLoadConfig()
    {
        return !IsExecuting && !string.IsNullOrWhiteSpace(ConfigPath);
    }

    private bool CanSaveConfig()
    {
        return _config is not null && !IsExecuting && !string.IsNullOrWhiteSpace(ConfigPath);
    }

    private bool CanOpenConfigFolder()
    {
        return _config is not null && !string.IsNullOrWhiteSpace(ConfigPath);
    }

    private bool CanMutateConfig()
    {
        return _config is not null && !IsExecuting;
    }

    private bool EnsureCanMutateConfig()
    {
        if (CanMutateConfig())
        {
            return true;
        }

        if (IsExecuting)
        {
            AddLog("Warning", "Cannot modify config while execution is running.");
            StatusMessage = "Cannot modify config while running.";
            return false;
        }

        AddLog("Warning", "No loaded config to modify.");
        StatusMessage = "No loaded config.";
        return false;
    }

    private bool CanTestSelector()
    {
        return !string.IsNullOrWhiteSpace(DiagnosticWindowTitleContains) &&
               (!string.IsNullOrWhiteSpace(DiagnosticAutomationId) ||
                !string.IsNullOrWhiteSpace(DiagnosticNameContains));
    }

    public bool ConfirmDiscardUnsavedChanges(string operationName)
    {
        if (!IsDirty)
        {
            return true;
        }

        var confirmed = _confirmationService?.ConfirmDiscardUnsavedChanges(operationName) ?? true;
        if (!confirmed)
        {
            StatusMessage = "Operation cancelled.";
            AddLog("Information", $"Cancelled {operationName} because there are unsaved changes.");
        }

        return confirmed;
    }

    private bool ConfirmDelete(string objectDescription)
    {
        var confirmed = _confirmationService?.ConfirmDelete(objectDescription) ?? true;
        if (!confirmed)
        {
            StatusMessage = "Delete cancelled.";
            AddLog("Information", $"Cancelled deleting {objectDescription}.");
        }

        return confirmed;
    }

    private void RaiseCommandStates()
    {
        LoadConfigCommand.RaiseCanExecuteChanged();
        SaveConfigCommand.RaiseCanExecuteChanged();
        RefreshConfigCommand.RaiseCanExecuteChanged();
        StartExecutionCommand.RaiseCanExecuteChanged();
        CancelExecutionCommand.RaiseCanExecuteChanged();
        OpenConfigFolderCommand.RaiseCanExecuteChanged();
        AddToolCommand.RaiseCanExecuteChanged();
        CopyToolCommand.RaiseCanExecuteChanged();
        DeleteToolCommand.RaiseCanExecuteChanged();
        AddTaskPlanCommand.RaiseCanExecuteChanged();
        CopyTaskPlanCommand.RaiseCanExecuteChanged();
        DeleteTaskPlanCommand.RaiseCanExecuteChanged();
        AddStepCommand.RaiseCanExecuteChanged();
        CopyStepCommand.RaiseCanExecuteChanged();
        DeleteStepCommand.RaiseCanExecuteChanged();
        NormalizeStepOrderCommand.RaiseCanExecuteChanged();
        AddActionCommand.RaiseCanExecuteChanged();
        CopyActionCommand.RaiseCanExecuteChanged();
        DeleteActionCommand.RaiseCanExecuteChanged();
    }

    private void RaiseDiagnosticCommandStates()
    {
        TestSelectorCommand.RaiseCanExecuteChanged();
        TestSelectorAndClickCommand.RaiseCanExecuteChanged();
    }

    private void PostToUi(Action action)
    {
        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            action();
            return;
        }

        _synchronizationContext.Post(_ => action(), null);
    }

}
