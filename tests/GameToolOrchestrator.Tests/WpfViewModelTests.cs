using GameToolOrchestrator.Core.Automation;
using GameToolOrchestrator.Core.Models;
using GameToolOrchestrator.Infrastructure.Configuration;
using GameToolOrchestrator.Wpf.Services;
using GameToolOrchestrator.Wpf.ViewModels;

namespace GameToolOrchestrator.Tests;

public sealed class WpfViewModelTests
{
    [Fact]
    public async Task LoadConfigAsync_LoadsTaskPlans()
    {
        var repository = new InMemoryConfigRepository { Config = CreateConfig() };
        var viewModel = CreateViewModel(repository);
        viewModel.ConfigPath = "config.json";

        await viewModel.LoadConfigAsync();

        Assert.Equal(2, viewModel.TaskPlans.Count);
        Assert.Equal("daily", viewModel.SelectedTaskPlan?.Id);
        Assert.Contains(viewModel.Logs, log => log.Message.Contains("Loaded config"));
    }

    [Fact]
    public async Task SelectingTaskPlan_ShowsSteps()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.SelectedTaskPlan = viewModel.TaskPlans[1];

        Assert.Single(viewModel.Steps);
        Assert.Equal("tool-b", viewModel.Steps[0].ToolId);
    }

    [Fact]
    public async Task SelectingStep_ShowsActions()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.SelectedStep = viewModel.Steps[0];

        Assert.Equal(2, viewModel.Actions.Count);
        Assert.Equal("waitWindow", viewModel.Actions[0].Type);
        Assert.Contains("BetterGI", viewModel.Actions[0].Summary);
    }

    [Fact]
    public async Task StartExecutionAsync_CallsExecutionEngine()
    {
        var factory = new FakeExecutionEngineFactory();
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() }, factory);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        await viewModel.StartExecutionAsync();

        Assert.Equal(1, factory.Engine.ExecuteCallCount);
        Assert.Equal("daily", factory.Engine.LastTaskPlanId);
        Assert.Equal(ExecutionStatus.Succeeded, viewModel.Steps[0].Status);
    }

    [Fact]
    public async Task CancelExecution_CancelsRunningToken()
    {
        var factory = new FakeExecutionEngineFactory();
        factory.Engine.CompletionSource = new TaskCompletionSource<ExecutionResult>();
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() }, factory);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        var runTask = viewModel.StartExecutionAsync();
        SpinWait.SpinUntil(() => factory.Engine.ExecuteCallCount == 1, TimeSpan.FromSeconds(1));

        viewModel.CancelExecution();

        Assert.True(factory.Engine.LastCancellationToken.IsCancellationRequested);
        factory.Engine.CompletionSource.SetResult(new ExecutionResult { TaskPlanId = "daily", Status = ExecutionStatus.Cancelled });
        await runTask;
    }

    [Fact]
    public async Task SaveConfigAsync_PersistsEditedExecutablePath()
    {
        var repository = new InMemoryConfigRepository { Config = CreateConfig() };
        var viewModel = CreateViewModel(repository);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.Steps[0].ExecutablePath = "D:\\NewPath\\BetterGI.exe";
        await viewModel.SaveConfigAsync();

        Assert.Equal("config.json", repository.LastSavePath);
        Assert.Equal("D:\\NewPath\\BetterGI.exe", repository.Config.Tools[0].ExecutablePath);
    }

    [Fact]
    public async Task EditingExecutablePath_SetsDirty()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.Steps[0].ExecutablePath = "D:\\NewPath\\BetterGI.exe";

        Assert.True(viewModel.IsDirty);
        Assert.Contains("\u672a\u4fdd\u5b58", viewModel.WindowTitle);
    }

    [Fact]
    public async Task EditingWorkingDirectory_SetsDirty()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.Steps[0].WorkingDirectory = "D:\\NewPath";

        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public async Task EditingStepEnabled_SetsDirty()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.Steps[0].Enabled = false;

        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public async Task EditingActionEnabled_SetsDirty()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.Actions[0].Enabled = false;

        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public async Task SaveConfigAsync_WhenSuccessful_ClearsDirty()
    {
        var repository = new InMemoryConfigRepository { Config = CreateConfig() };
        var viewModel = CreateViewModel(repository);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();
        viewModel.Steps[0].ExecutablePath = "D:\\NewPath\\BetterGI.exe";

        await viewModel.SaveConfigAsync();

        Assert.False(viewModel.IsDirty);
        Assert.Equal(string.Empty, viewModel.DirtyStatusText);
        Assert.DoesNotContain("\u672a\u4fdd\u5b58", viewModel.WindowTitle);
    }

    [Fact]
    public async Task ReloadConfigAsync_WhenConfirmed_ClearsDirty()
    {
        var repository = new InMemoryConfigRepository { Config = CreateConfig() };
        var confirmation = new FakeUserConfirmationService { Response = true };
        var viewModel = CreateViewModel(repository, confirmationService: confirmation);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();
        viewModel.Steps[0].ExecutablePath = "D:\\Dirty\\BetterGI.exe";
        repository.Config = CreateConfig();

        await viewModel.LoadConfigAsync();

        Assert.False(viewModel.IsDirty);
        Assert.Equal(1, confirmation.CallCount);
        Assert.Equal("D:\\Tools\\BetterGI\\BetterGI.exe", viewModel.Steps[0].ExecutablePath);
    }

    [Fact]
    public async Task ReloadConfigAsync_WhenDirtyAndUserCancels_KeepsCurrentConfig()
    {
        var repository = new InMemoryConfigRepository { Config = CreateConfig() };
        var confirmation = new FakeUserConfirmationService { Response = false };
        var viewModel = CreateViewModel(repository, confirmationService: confirmation);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();
        viewModel.Steps[0].ExecutablePath = "D:\\Dirty\\BetterGI.exe";
        repository.Config = CreateConfig();

        await viewModel.LoadConfigAsync();

        Assert.True(viewModel.IsDirty);
        Assert.Equal(1, confirmation.CallCount);
        Assert.Equal("D:\\Dirty\\BetterGI.exe", viewModel.Steps[0].ExecutablePath);
    }

    [Fact]
    public async Task SaveConfigAsync_WritesEditableFieldsToJsonAndReloadsConsistently()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "config.json");
        var repository = new JsonConfigRepository();
        await repository.SaveAsync(path, CreateConfig());

        var viewModel = new MainWindowViewModel(
            repository,
            new FakeExecutionEngineFactory(),
            new FakeUiAutomationService());
        viewModel.ConfigPath = path;
        await viewModel.LoadConfigAsync();

        viewModel.Steps[0].ExecutablePath = "D:\\Saved\\BetterGI.exe";
        viewModel.Steps[0].WorkingDirectory = "D:\\Saved";
        viewModel.Steps[0].Enabled = false;
        viewModel.Actions[0].Enabled = false;

        await viewModel.SaveConfigAsync();

        var saved = await repository.LoadAsync(path);
        Assert.Equal("D:\\Saved\\BetterGI.exe", saved.Tools[0].ExecutablePath);
        Assert.Equal("D:\\Saved", saved.Tools[0].WorkingDirectory);
        Assert.False(saved.TaskPlans[0].Steps[0].Enabled);
        Assert.False(saved.TaskPlans[0].Steps[0].Actions[0].Enabled);

        var reloadedViewModel = new MainWindowViewModel(
            repository,
            new FakeExecutionEngineFactory(),
            new FakeUiAutomationService());
        reloadedViewModel.ConfigPath = path;
        await reloadedViewModel.LoadConfigAsync();

        Assert.Equal("D:\\Saved\\BetterGI.exe", reloadedViewModel.Steps[0].ExecutablePath);
        Assert.Equal("D:\\Saved", reloadedViewModel.Steps[0].WorkingDirectory);
        Assert.False(reloadedViewModel.Steps[0].Enabled);
        Assert.False(reloadedViewModel.Actions[0].Enabled);
        Assert.False(reloadedViewModel.IsDirty);
    }

    [Fact]
    public async Task SaveConfigAsync_WhenRepositoryFails_LogsErrorAndKeepsDirty()
    {
        var repository = new InMemoryConfigRepository
        {
            Config = CreateConfig(),
            SaveException = new UnauthorizedAccessException("read-only file")
        };
        var viewModel = CreateViewModel(repository);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();
        viewModel.Steps[0].WorkingDirectory = "D:\\NewPath";

        await viewModel.SaveConfigAsync();

        Assert.True(viewModel.IsDirty);
        Assert.Contains(viewModel.Logs, log => log.Level == "Error" && log.Message.Contains("read-only file"));
        Assert.Contains(viewModel.Logs, log => log.Message.Contains("read-only") && log.Message.Contains("permissions"));
    }

    [Fact]
    public async Task SelectingWaitSecondsAction_ShowsWaitSecondsEditor()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository
        {
            Config = CreateConfigWithSingleAction(new AutomationActionDefinition
            {
                Id = "wait",
                Type = "waitSeconds",
                Parameters = new Dictionary<string, string> { ["seconds"] = "2" }
            })
        });
        viewModel.ConfigPath = "config.json";

        await viewModel.LoadConfigAsync();

        Assert.NotNull(viewModel.SelectedActionEditor);
        Assert.True(viewModel.SelectedActionEditor!.IsWaitSecondsEditor);
        Assert.Equal("2", viewModel.SelectedActionEditor.Seconds);
    }

    [Fact]
    public async Task EditingWaitSecondsSeconds_SetsDirtyAndSavesJson()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "config.json");
        var repository = new JsonConfigRepository();
        await repository.SaveAsync(path, CreateConfigWithSingleAction(new AutomationActionDefinition
        {
            Id = "wait",
            Type = "waitSeconds",
            Parameters = new Dictionary<string, string> { ["seconds"] = "1" }
        }));
        var viewModel = new MainWindowViewModel(repository, new FakeExecutionEngineFactory(), new FakeUiAutomationService());
        viewModel.ConfigPath = path;
        await viewModel.LoadConfigAsync();

        viewModel.SelectedActionEditor!.Seconds = "9.5";
        await viewModel.SaveConfigAsync();

        var saved = await repository.LoadAsync(path);
        Assert.False(viewModel.IsDirty);
        Assert.Equal("9.5", saved.TaskPlans[0].Steps[0].Actions[0].Parameters["seconds"]);
    }

    [Fact]
    public async Task SelectingWaitWindowAction_ShowsWindowFields()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository
        {
            Config = CreateConfigWithSingleAction(new AutomationActionDefinition
            {
                Id = "wait-window",
                Type = "waitWindow",
                TitleContains = "BetterGI",
                TimeoutSeconds = 30
            })
        });
        viewModel.ConfigPath = "config.json";

        await viewModel.LoadConfigAsync();

        Assert.True(viewModel.SelectedActionEditor!.IsWaitWindowEditor);
        Assert.Equal("BetterGI", viewModel.SelectedActionEditor.TitleContains);
        Assert.Equal("30", viewModel.SelectedActionEditor.TimeoutSeconds);
    }

    [Fact]
    public async Task WaitWindow_WithoutTitleMatcher_FailsValidationAndDoesNotSave()
    {
        var repository = new InMemoryConfigRepository
        {
            Config = CreateConfigWithSingleAction(new AutomationActionDefinition
            {
                Id = "wait-window",
                Type = "waitWindow",
                TitleContains = "BetterGI"
            })
        };
        var viewModel = CreateViewModel(repository);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.SelectedActionEditor!.TitleContains = string.Empty;
        await viewModel.SaveConfigAsync();

        Assert.Null(repository.LastSavePath);
        Assert.Contains(viewModel.Logs, log => log.Level == "Error" && log.Message.Contains("waitWindow"));
    }

    [Fact]
    public async Task EditingWaitWindowTitleContains_SavesJson()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "config.json");
        var repository = new JsonConfigRepository();
        await repository.SaveAsync(path, CreateConfigWithSingleAction(new AutomationActionDefinition
        {
            Id = "wait-window",
            Type = "waitWindow",
            TitleContains = "Old",
            TimeoutSeconds = 10
        }));
        var viewModel = new MainWindowViewModel(repository, new FakeExecutionEngineFactory(), new FakeUiAutomationService());
        viewModel.ConfigPath = path;
        await viewModel.LoadConfigAsync();

        viewModel.SelectedActionEditor!.TitleContains = "BetterGI";
        viewModel.SelectedActionEditor.TimeoutSeconds = "20";
        await viewModel.SaveConfigAsync();

        var saved = await repository.LoadAsync(path);
        Assert.Equal("BetterGI", saved.TaskPlans[0].Steps[0].Actions[0].TitleContains);
        Assert.Equal(20, saved.TaskPlans[0].Steps[0].Actions[0].TimeoutSeconds);
    }

    [Fact]
    public async Task SelectingClickButtonAction_ShowsClickButtonFields()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository
        {
            Config = CreateConfigWithSingleAction(CreateClickButtonAction())
        });
        viewModel.ConfigPath = "config.json";

        await viewModel.LoadConfigAsync();

        var editor = viewModel.SelectedActionEditor!;
        Assert.True(editor.IsClickButtonEditor);
        Assert.Equal("BetterGI", editor.WindowTitleContains);
        Assert.Equal("StartButton", editor.AutomationId);
        Assert.Equal("Start", editor.NameEquals);
        Assert.Equal("Start", editor.NameContains);
        Assert.Equal("Button", editor.ControlType);
        Assert.Equal("10", editor.TimeoutSeconds);
    }

    [Fact]
    public async Task ClickButton_WithoutSelectors_FailsValidation()
    {
        var repository = new InMemoryConfigRepository
        {
            Config = CreateConfigWithSingleAction(CreateClickButtonAction())
        };
        var viewModel = CreateViewModel(repository);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.SelectedActionEditor!.AutomationId = string.Empty;
        viewModel.SelectedActionEditor.NameEquals = string.Empty;
        viewModel.SelectedActionEditor.NameContains = string.Empty;
        await viewModel.SaveConfigAsync();

        Assert.Null(repository.LastSavePath);
        Assert.Contains(viewModel.Logs, log => log.Level == "Error" && log.Message.Contains("clickButton"));
    }

    [Fact]
    public async Task ClickButton_WithMultipleSelectors_ShowsPriorityHint()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository
        {
            Config = CreateConfigWithSingleAction(CreateClickButtonAction())
        });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        var hint = viewModel.SelectedActionEditor!.SelectorPriorityHint;

        Assert.Contains("automationId > nameEquals > nameContains", hint);
    }

    [Fact]
    public async Task ActionEditorClickButton_TestSelectorDoesNotClick()
    {
        var automation = new FakeUiAutomationService
        {
            ElementSearchResult = new UiElementSearchResult
            {
                Status = ExecutionStatus.Succeeded,
                Elements =
                [
                    new UiElementInfo { Index = 0, Name = "Start", ControlType = "Button", IsEnabled = true }
                ]
            }
        };
        var viewModel = CreateViewModel(
            new InMemoryConfigRepository { Config = CreateConfigWithSingleAction(CreateClickButtonAction()) },
            automationService: automation);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        await viewModel.SelectedActionEditor!.TestSelectorAsync(click: false);

        Assert.Equal(0, automation.ClickCount);
        Assert.Contains(viewModel.Logs, log => log.Message.Contains("without clicking"));
    }

    [Fact]
    public async Task ActionEditorClickButton_TestAndClickInvokesClick()
    {
        var automation = new FakeUiAutomationService
        {
            ElementSearchResult = new UiElementSearchResult
            {
                Status = ExecutionStatus.Succeeded,
                Elements =
                [
                    new UiElementInfo { Index = 0, Name = "Start", ControlType = "Button", IsEnabled = true, IsClickable = true }
                ]
            },
            ClickResult = UiClickResult.Succeeded(
                new UiWindowInfo { Title = "BetterGI" },
                new UiElementInfo { Index = 0, Name = "Start", ControlType = "Button", IsEnabled = true, IsClickable = true })
        };
        var viewModel = CreateViewModel(
            new InMemoryConfigRepository { Config = CreateConfigWithSingleAction(CreateClickButtonAction()) },
            automationService: automation);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        await viewModel.SelectedActionEditor!.TestSelectorAsync(click: true);

        Assert.Equal(1, automation.ClickCount);
    }

    [Fact]
    public async Task EditingWaitProcessExitTimeout_SavesJson()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "config.json");
        var repository = new JsonConfigRepository();
        await repository.SaveAsync(path, CreateConfigWithSingleAction(new AutomationActionDefinition
        {
            Id = "wait-exit",
            Type = "waitProcessExit",
            TimeoutMinutes = 30
        }));
        var viewModel = new MainWindowViewModel(repository, new FakeExecutionEngineFactory(), new FakeUiAutomationService());
        viewModel.ConfigPath = path;
        await viewModel.LoadConfigAsync();

        viewModel.SelectedActionEditor!.TimeoutSeconds = "120";
        viewModel.SelectedActionEditor.TimeoutMinutes = "5";
        await viewModel.SaveConfigAsync();

        var saved = await repository.LoadAsync(path);
        Assert.Equal(120, saved.TaskPlans[0].Steps[0].Actions[0].TimeoutSeconds);
        Assert.Equal(5, saved.TaskPlans[0].Steps[0].Actions[0].TimeoutMinutes);
    }

    [Fact]
    public async Task UnsupportedAction_ShowsUnsupportedMessage()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository
        {
            Config = CreateConfigWithSingleAction(new AutomationActionDefinition
            {
                Id = "custom",
                Type = "customAction"
            })
        });
        viewModel.ConfigPath = "config.json";

        await viewModel.LoadConfigAsync();

        Assert.True(viewModel.SelectedActionEditor!.IsUnsupportedEditor);
        Assert.Contains("JSON", viewModel.SelectedActionEditor.UnsupportedMessage);
    }

    [Fact]
    public async Task ReloadConfigAsync_PreservesEditedActionValues()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "config.json");
        var repository = new JsonConfigRepository();
        await repository.SaveAsync(path, CreateConfigWithSingleAction(CreateClickButtonAction()));
        var viewModel = new MainWindowViewModel(repository, new FakeExecutionEngineFactory(), new FakeUiAutomationService());
        viewModel.ConfigPath = path;
        await viewModel.LoadConfigAsync();

        viewModel.SelectedActionEditor!.NameContains = "开始执行";
        viewModel.SelectedActionEditor.TimeoutSeconds = "15";
        await viewModel.SaveConfigAsync();
        await viewModel.LoadConfigAsync();

        Assert.Equal("开始执行", viewModel.SelectedActionEditor!.NameContains);
        Assert.Equal("15", viewModel.SelectedActionEditor.TimeoutSeconds);
    }

    [Fact]
    public async Task TestSelectorAsync_DefaultDoesNotClick()
    {
        var automation = new FakeUiAutomationService
        {
            ElementSearchResult = new UiElementSearchResult
            {
                Status = ExecutionStatus.Succeeded,
                Elements =
                [
                    new UiElementInfo { Index = 0, Name = "Start", ControlType = "Button", IsEnabled = true }
                ]
            }
        };
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() }, automationService: automation);
        viewModel.DiagnosticWindowTitleContains = "BetterGI";
        viewModel.DiagnosticNameContains = "Start";

        await viewModel.TestSelectorAsync(click: false);

        Assert.Equal(0, automation.ClickCount);
        Assert.Contains(viewModel.Logs, log => log.Message.Contains("without clicking"));
    }

    [Fact]
    public async Task TestSelectorAsync_ClickTrueInvokesClick()
    {
        var automation = new FakeUiAutomationService
        {
            ElementSearchResult = new UiElementSearchResult
            {
                Status = ExecutionStatus.Succeeded,
                Elements =
                [
                    new UiElementInfo { Index = 0, Name = "Start", ControlType = "Button", IsEnabled = true, IsClickable = true }
                ]
            },
            ClickResult = UiClickResult.Succeeded(
                new UiWindowInfo { Title = "BetterGI" },
                new UiElementInfo { Index = 0, Name = "Start", ControlType = "Button", IsEnabled = true, IsClickable = true })
        };
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() }, automationService: automation);
        viewModel.DiagnosticWindowTitleContains = "BetterGI";
        viewModel.DiagnosticNameContains = "Start";

        await viewModel.TestSelectorAsync(click: true);

        Assert.Equal(1, automation.ClickCount);
    }

    [Fact]
    public async Task ExecutionCommands_ReflectRunningState()
    {
        var factory = new FakeExecutionEngineFactory();
        factory.Engine.CompletionSource = new TaskCompletionSource<ExecutionResult>();
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() }, factory);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        Assert.True(viewModel.StartExecutionCommand.CanExecute(null));
        Assert.False(viewModel.CancelExecutionCommand.CanExecute(null));

        var runTask = viewModel.StartExecutionAsync();
        SpinWait.SpinUntil(() => viewModel.IsExecuting, TimeSpan.FromSeconds(1));

        Assert.False(viewModel.StartExecutionCommand.CanExecute(null));
        Assert.False(viewModel.SaveConfigCommand.CanExecute(null));
        Assert.False(viewModel.LoadConfigCommand.CanExecute(null));
        Assert.False(viewModel.AddToolCommand.CanExecute(null));
        Assert.False(viewModel.CopyTaskPlanCommand.CanExecute(null));
        Assert.False(viewModel.AddStepCommand.CanExecute(null));
        Assert.False(viewModel.AddActionCommand.CanExecute(null));
        Assert.False(viewModel.CanBrowseConfig);
        Assert.True(viewModel.CancelExecutionCommand.CanExecute(null));

        viewModel.CancelExecution();
        factory.Engine.CompletionSource.SetResult(new ExecutionResult { TaskPlanId = "daily", Status = ExecutionStatus.Cancelled });
        await runTask;

        Assert.False(viewModel.CancelExecutionCommand.CanExecute(null));
    }

    [Fact]
    public async Task AddCopyDeleteTool_UpdateConfigAndDirtyState()
    {
        var repository = new InMemoryConfigRepository { Config = CreateConfig() };
        var viewModel = CreateViewModel(repository);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.AddTool();
        Assert.True(viewModel.IsDirty);
        Assert.Equal("tool-1", viewModel.SelectedTool?.Id);

        viewModel.CopyTool();
        Assert.Equal("tool-1-copy", viewModel.SelectedTool?.Id);

        viewModel.DeleteTool();
        Assert.DoesNotContain(viewModel.Tools, tool => tool.Id == "tool-1-copy");
    }

    [Fact]
    public async Task DeleteTool_WhenReferenced_IsBlockedBeforeConfirmation()
    {
        var confirmation = new FakeUserConfirmationService();
        var viewModel = CreateViewModel(
            new InMemoryConfigRepository { Config = CreateConfig() },
            confirmationService: confirmation);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();
        viewModel.SelectedTool = viewModel.Tools.First(tool => tool.Id == "bettergi");

        viewModel.DeleteTool();

        Assert.Contains(viewModel.Tools, tool => tool.Id == "bettergi");
        Assert.Equal(0, confirmation.DeleteCallCount);
        Assert.Contains(viewModel.Logs, log => log.Level == "Error" && log.Message.Contains("referenced"));
    }

    [Fact]
    public async Task AddCopyDeleteTaskPlan_UpdateSelectionAndDirtyState()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.AddTaskPlan();
        Assert.Equal("plan-1", viewModel.SelectedTaskPlan?.Id);
        Assert.True(viewModel.IsDirty);

        viewModel.CopyTaskPlan();
        Assert.Equal("plan-1-copy", viewModel.SelectedTaskPlan?.Id);

        viewModel.DeleteTaskPlan();
        Assert.DoesNotContain(viewModel.TaskPlans, plan => plan.Id == "plan-1-copy");
    }

    [Fact]
    public async Task AddStep_UsesSelectedToolAndNextOrder()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();
        viewModel.SelectedTool = viewModel.Tools.First(tool => tool.Id == "tool-b");

        viewModel.AddStep();

        Assert.True(viewModel.IsDirty);
        Assert.Equal("tool-b", viewModel.SelectedStep?.ToolId);
        Assert.Equal(2, viewModel.SelectedStep?.Order);
    }

    [Fact]
    public async Task AddStep_WhenNoTool_LogsError()
    {
        var config = new OrchestratorConfig
        {
            TaskPlans =
            [
                new TaskPlan { Id = "empty", Name = "Empty" }
            ]
        };
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = config });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.AddStep();

        Assert.Empty(viewModel.Steps);
        Assert.Contains(viewModel.Logs, log => log.Level == "Error" && log.Message.Contains("selected tool"));
    }

    [Fact]
    public async Task CopyDeleteAndNormalizeSteps_UpdateConfig()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.CopyStep();
        Assert.Equal(2, viewModel.SelectedStep?.Order);
        Assert.Equal("run-bettergi-copy", viewModel.SelectedStep?.Id);

        viewModel.SelectedTaskPlan!.Plan.Steps[0].Order = 20;
        viewModel.SelectedTaskPlan.Plan.Steps[1].Order = 10;
        viewModel.NormalizeStepOrder();
        Assert.Equal([1, 2], viewModel.Steps.Select(step => step.Order).ToArray());

        var copiedId = viewModel.SelectedStep!.Id;
        viewModel.DeleteStep();
        Assert.DoesNotContain(viewModel.Steps, step => step.Id == copiedId);
    }

    [Fact]
    public async Task AddActions_DefaultsAndValidationFailuresAreVisible()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfigWithEmptyStep() });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.SelectedNewActionType = "waitSeconds";
        viewModel.AddAction();
        Assert.Equal("waitSeconds", viewModel.SelectedAction?.Type);
        Assert.Equal("1", viewModel.SelectedActionEditor?.Seconds);

        viewModel.SelectedNewActionType = "waitWindow";
        viewModel.AddAction();
        await viewModel.SaveConfigAsync();

        Assert.Contains(viewModel.Logs, log => log.Level == "Error" && log.Message.Contains("waitWindow"));
    }

    [Fact]
    public async Task AddClickButtonAction_WithEmptySelector_FailsValidation()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfigWithEmptyStep() });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.SelectedNewActionType = "clickButton";
        viewModel.AddAction();
        await viewModel.SaveConfigAsync();

        Assert.Contains(viewModel.Logs, log => log.Level == "Error" && log.Message.Contains("clickButton"));
    }

    [Fact]
    public async Task CopyAndDeleteAction_UpdateConfig()
    {
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() });
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.CopyAction();
        Assert.Equal("wait-window-copy", viewModel.SelectedAction?.Id);
        Assert.Equal(3, viewModel.SelectedAction?.Action.Order);

        viewModel.DeleteAction();

        Assert.DoesNotContain(viewModel.Actions, action => action.Id == "wait-window-copy");
        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public async Task SaveAndReload_PreservesAddedConfigurationStructure()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "config.json");
        var repository = new JsonConfigRepository();
        await repository.SaveAsync(path, CreateConfigWithEmptyStep());

        var viewModel = new MainWindowViewModel(repository, new FakeExecutionEngineFactory(), new FakeUiAutomationService());
        viewModel.ConfigPath = path;
        await viewModel.LoadConfigAsync();

        viewModel.AddTool();
        viewModel.SelectedTool!.ExecutablePath = "D:\\Tools\\New.exe";
        viewModel.AddTaskPlan();
        viewModel.AddStep();
        viewModel.SelectedNewActionType = "waitSeconds";
        viewModel.AddAction();
        await viewModel.SaveConfigAsync();

        var reloadedViewModel = new MainWindowViewModel(repository, new FakeExecutionEngineFactory(), new FakeUiAutomationService());
        reloadedViewModel.ConfigPath = path;
        await reloadedViewModel.LoadConfigAsync();

        Assert.Contains(reloadedViewModel.Tools, tool => tool.Id == "tool-1" && tool.ExecutablePath == "D:\\Tools\\New.exe");
        Assert.Contains(reloadedViewModel.TaskPlans, plan => plan.Id == "plan-1");
        reloadedViewModel.SelectedTaskPlan = reloadedViewModel.TaskPlans.First(plan => plan.Id == "plan-1");
        Assert.Single(reloadedViewModel.Steps);
        Assert.Single(reloadedViewModel.Actions);
        Assert.Equal("waitSeconds", reloadedViewModel.Actions[0].Type);
        Assert.False(reloadedViewModel.IsDirty);
    }

    [Fact]
    public async Task CopyDiagnostics_IncludesCurrentContextAndLogs()
    {
        var clipboard = new FakeClipboardService();
        var viewModel = CreateViewModel(
            new InMemoryConfigRepository { Config = CreateConfig() },
            clipboardService: clipboard);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();
        viewModel.SelectedAction = viewModel.Actions[1];
        viewModel.AddLog("Error", "sample failure");

        viewModel.CopyDiagnostics();

        Assert.Equal(1, clipboard.CopyCount);
        Assert.Contains("ConfigPath: config.json", clipboard.LastText);
        Assert.Contains("TaskPlan: daily / Daily", clipboard.LastText);
        Assert.Contains("Step: run-bettergi / toolId=bettergi / toolName=BetterGI", clipboard.LastText);
        Assert.Contains("Action: index=1 / type=clickButton", clipboard.LastText);
        Assert.Contains("sample failure", clipboard.LastText);
    }

    [Fact]
    public void CopyDiagnostics_WithNoSelections_DoesNotThrow()
    {
        var clipboard = new FakeClipboardService();
        var viewModel = CreateViewModel(
            new InMemoryConfigRepository(),
            clipboardService: clipboard);

        viewModel.CopyDiagnostics();

        Assert.Equal(1, clipboard.CopyCount);
        Assert.Contains("TaskPlan: <none> / <none>", clipboard.LastText);
        Assert.Contains("Step: <none>", clipboard.LastText);
        Assert.Contains("Action: index=<none>", clipboard.LastText);
    }

    [Fact]
    public void OpenLogsFolder_WhenMissing_CreatesDirectory()
    {
        var root = CreateTempDirectory();
        var logsDirectory = Path.Combine(root, "logs");
        var launcher = new FakeFolderLauncherService();
        var viewModel = CreateViewModel(
            new InMemoryConfigRepository(),
            folderLauncherService: launcher,
            logsDirectory: logsDirectory);

        viewModel.OpenLogsFolder();

        Assert.True(Directory.Exists(logsDirectory));
        Assert.Contains(Path.GetFullPath(logsDirectory), launcher.OpenedFolders);
    }

    [Fact]
    public void OpenConfigFolder_WhenNoConfig_IsNotExecutableAndLogsWarning()
    {
        var launcher = new FakeFolderLauncherService();
        var viewModel = CreateViewModel(
            new InMemoryConfigRepository(),
            folderLauncherService: launcher);

        Assert.False(viewModel.OpenConfigFolderCommand.CanExecute(null));

        viewModel.OpenConfigFolder();

        Assert.Empty(launcher.OpenedFolders);
        Assert.Contains(viewModel.Logs, log => log.Level == "Warning" && log.Message.Contains("No loaded config"));
    }

    [Fact]
    public async Task OpenConfigFolder_WhenConfigLoaded_OpensContainingDirectory()
    {
        var launcher = new FakeFolderLauncherService();
        var directory = CreateTempDirectory();
        var configPath = Path.Combine(directory, "config.json");
        var viewModel = CreateViewModel(
            new InMemoryConfigRepository { Config = CreateConfig() },
            folderLauncherService: launcher);
        viewModel.ConfigPath = configPath;
        await viewModel.LoadConfigAsync();

        viewModel.OpenConfigFolder();

        Assert.Contains(Path.GetFullPath(directory), launcher.OpenedFolders);
    }

    [Fact]
    public async Task StartExecutionAsync_AppendsExecutionSummary()
    {
        var factory = new FakeExecutionEngineFactory();
        factory.Engine.ResultToReturn = CreateExecutionResult(
            ExecutionStatus.Succeeded,
            ExecutionStatus.Succeeded,
            "waitSeconds",
            errorMessage: null);
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() }, factory);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        await viewModel.StartExecutionAsync();

        Assert.Contains(viewModel.Logs, log => log.Message.Contains("Execution summary"));
        Assert.Contains(viewModel.Logs, log => log.Message.Contains("Steps: total=1, succeeded=1"));
    }

    [Fact]
    public async Task StartExecutionAsync_WhenWaitWindowFails_AppendsWindowDiagnosisAdvice()
    {
        var factory = new FakeExecutionEngineFactory();
        factory.Engine.ResultToReturn = CreateExecutionResult(
            ExecutionStatus.Failed,
            ExecutionStatus.TimedOut,
            "waitWindow",
            "Window not found.");
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() }, factory);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        await viewModel.StartExecutionAsync();

        Assert.Contains(viewModel.Logs, log => log.Message.Contains("waitWindow failed") && log.Message.Contains("Refresh Window List"));
    }

    [Fact]
    public async Task StartExecutionAsync_WhenClickButtonFails_AppendsSelectorAdvice()
    {
        var factory = new FakeExecutionEngineFactory();
        factory.Engine.ResultToReturn = CreateExecutionResult(
            ExecutionStatus.Failed,
            ExecutionStatus.Failed,
            "clickButton",
            "Button not found.");
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() }, factory);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        await viewModel.StartExecutionAsync();

        Assert.Contains(viewModel.Logs, log => log.Message.Contains("clickButton failed") && log.Message.Contains("Test Selector"));
    }

    [Fact]
    public async Task StartExecutionAsync_WhenWaitProcessExitTimesOut_AppendsTimeoutAdvice()
    {
        var factory = new FakeExecutionEngineFactory();
        factory.Engine.ResultToReturn = CreateExecutionResult(
            ExecutionStatus.TimedOut,
            ExecutionStatus.TimedOut,
            "waitProcessExit",
            "Process did not exit.");
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = CreateConfig() }, factory);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        await viewModel.StartExecutionAsync();

        Assert.Contains(viewModel.Logs, log => log.Message.Contains("waitProcessExit timed out") && log.Message.Contains("timeoutSeconds"));
    }

    [Fact]
    public async Task CopyDiagnostics_DoesNotTriggerClick()
    {
        var automation = new FakeUiAutomationService();
        var clipboard = new FakeClipboardService();
        var viewModel = CreateViewModel(
            new InMemoryConfigRepository { Config = CreateConfig() },
            automationService: automation,
            clipboardService: clipboard);
        viewModel.ConfigPath = "config.json";
        await viewModel.LoadConfigAsync();

        viewModel.CopyDiagnostics();

        Assert.Equal(0, automation.ClickCount);
        Assert.Equal(1, clipboard.CopyCount);
    }

    [Fact]
    public async Task LoadConfigAsync_PreservesChineseText()
    {
        var config = CreateConfig();
        config.TaskPlans[0].Name = "每日任务";
        config.TaskPlans[0].Steps[0].Actions[1].NameContains = "开始";
        var viewModel = CreateViewModel(new InMemoryConfigRepository { Config = config });
        viewModel.ConfigPath = "config.json";

        await viewModel.LoadConfigAsync();

        Assert.Equal("每日任务", viewModel.TaskPlans[0].Name);
        Assert.Contains("开始", viewModel.Actions[1].Summary);
    }

    private static MainWindowViewModel CreateViewModel(
        InMemoryConfigRepository repository,
        FakeExecutionEngineFactory? factory = null,
        FakeUiAutomationService? automationService = null,
        IUserConfirmationService? confirmationService = null,
        IClipboardService? clipboardService = null,
        IFolderLauncherService? folderLauncherService = null,
        string? logsDirectory = null)
    {
        return new MainWindowViewModel(
            repository,
            factory ?? new FakeExecutionEngineFactory(),
            automationService ?? new FakeUiAutomationService(),
            confirmationService: confirmationService,
            clipboardService: clipboardService,
            folderLauncherService: folderLauncherService,
            logsDirectory: logsDirectory);
    }

    private static OrchestratorConfig CreateConfig()
    {
        return new OrchestratorConfig
        {
            Tools =
            [
                new ToolDefinition
                {
                    Id = "bettergi",
                    Name = "BetterGI",
                    ExecutablePath = "D:\\Tools\\BetterGI\\BetterGI.exe",
                    WorkingDirectory = "D:\\Tools\\BetterGI"
                },
                new ToolDefinition
                {
                    Id = "tool-b",
                    Name = "Tool B",
                    ExecutablePath = "D:\\Tools\\B.exe"
                }
            ],
            TaskPlans =
            [
                new TaskPlan
                {
                    Id = "daily",
                    Name = "Daily",
                    Steps =
                    [
                        new TaskStep
                        {
                            Id = "run-bettergi",
                            ToolId = "bettergi",
                            Order = 1,
                            Actions =
                            [
                                new AutomationActionDefinition
                                {
                                    Id = "wait-window",
                                    Type = "waitWindow",
                                    Order = 1,
                                    TitleContains = "BetterGI"
                                },
                                new AutomationActionDefinition
                                {
                                    Id = "click-start",
                                    Type = "clickButton",
                                    Order = 2,
                                    NameContains = "Start"
                                }
                            ]
                        }
                    ]
                },
                new TaskPlan
                {
                    Id = "secondary",
                    Name = "Secondary",
                    Steps =
                    [
                        new TaskStep
                        {
                            Id = "run-b",
                            ToolId = "tool-b",
                            Order = 1,
                            Actions =
                            [
                                new AutomationActionDefinition
                                {
                                    Id = "wait",
                                    Type = "waitSeconds",
                                    Order = 1
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static OrchestratorConfig CreateConfigWithSingleAction(AutomationActionDefinition action)
    {
        return new OrchestratorConfig
        {
            Tools =
            [
                new ToolDefinition
                {
                    Id = "bettergi",
                    Name = "BetterGI",
                    ExecutablePath = "D:\\Tools\\BetterGI\\BetterGI.exe",
                    WorkingDirectory = "D:\\Tools\\BetterGI"
                }
            ],
            TaskPlans =
            [
                new TaskPlan
                {
                    Id = "daily",
                    Name = "Daily",
                    Steps =
                    [
                        new TaskStep
                        {
                            Id = "run-bettergi",
                            ToolId = "bettergi",
                            Order = 1,
                            Actions = [action]
                        }
                    ]
                }
            ]
        };
    }

    private static OrchestratorConfig CreateConfigWithEmptyStep()
    {
        return new OrchestratorConfig
        {
            Tools =
            [
                new ToolDefinition
                {
                    Id = "bettergi",
                    Name = "BetterGI",
                    ExecutablePath = "D:\\Tools\\BetterGI\\BetterGI.exe"
                }
            ],
            TaskPlans =
            [
                new TaskPlan
                {
                    Id = "daily",
                    Name = "Daily",
                    Steps =
                    [
                        new TaskStep
                        {
                            Id = "run-bettergi",
                            ToolId = "bettergi",
                            Order = 1
                        }
                    ]
                }
            ]
        };
    }

    private static AutomationActionDefinition CreateClickButtonAction()
    {
        return new AutomationActionDefinition
        {
            Id = "click-start",
            Type = "clickButton",
            WindowTitleContains = "BetterGI",
            AutomationId = "StartButton",
            NameEquals = "Start",
            NameContains = "Start",
            ControlType = "Button",
            TimeoutSeconds = 10
        };
    }

    private static ExecutionResult CreateExecutionResult(
        ExecutionStatus resultStatus,
        ExecutionStatus actionStatus,
        string actionType,
        string? errorMessage)
    {
        var startedAt = DateTimeOffset.UtcNow.AddSeconds(-3);
        var completedAt = DateTimeOffset.UtcNow;
        var stepStatus = resultStatus == ExecutionStatus.Succeeded
            ? ExecutionStatus.Succeeded
            : actionStatus;

        return new ExecutionResult
        {
            TaskPlanId = "daily",
            Status = resultStatus,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ErrorMessage = errorMessage,
            Tasks =
            [
                new TaskExecutionResult
                {
                    TaskPlanId = "daily",
                    TaskPlanName = "Daily",
                    Status = resultStatus,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    Steps =
                    [
                        new StepExecutionResult
                        {
                            StepId = "run-bettergi",
                            StepName = "Run BetterGI",
                            ToolId = "bettergi",
                            Status = stepStatus,
                            StartedAt = startedAt,
                            CompletedAt = completedAt,
                            ErrorMessage = errorMessage,
                            Actions =
                            [
                                new ActionExecutionResult
                                {
                                    ActionId = "action-1",
                                    ActionType = actionType,
                                    ActionName = actionType,
                                    Status = actionStatus,
                                    StartedAt = startedAt,
                                    CompletedAt = completedAt,
                                    ErrorMessage = errorMessage
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gto-wpf-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
