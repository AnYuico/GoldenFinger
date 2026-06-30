using GameToolOrchestrator.Core.Models;
using GameToolOrchestrator.Wpf.ViewModels;

namespace GameToolOrchestrator.Tests;

public sealed class WpfBindingContractTests
{
    [Fact]
    public void TaskPlanEnabled_IsReadOnlyDisplayProperty()
    {
        var property = typeof(TaskPlanItemViewModel).GetProperty(nameof(TaskPlanItemViewModel.Enabled));
        var viewModel = new TaskPlanItemViewModel(new TaskPlan { Id = "daily", Name = "Daily" });

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
        Assert.True(viewModel.Enabled);
        Assert.Equal("\u542f\u7528", viewModel.EnabledText);
    }

    [Fact]
    public void MainWindowXaml_UsesDisplayOnlyTaskPlanEnabledBinding()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());

        Assert.Contains("Binding=\"{Binding EnabledText, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("DataGridCheckBoxColumn Header=\"Enabled\" Binding=\"{Binding Enabled}\"", xaml);
    }

    [Fact]
    public void StepEnabled_IsEditableAndWritesBackToModel()
    {
        var step = new TaskStep { Enabled = true };
        var viewModel = new StepItemViewModel(step, tool: null);

        viewModel.Enabled = false;

        Assert.False(step.Enabled);
    }

    [Fact]
    public void ActionEnabled_IsEditableAndWritesBackToModel()
    {
        var action = new AutomationActionDefinition { Enabled = true };
        var viewModel = new ActionItemViewModel(action, index: 0);

        viewModel.Enabled = false;

        Assert.False(action.Enabled);
    }

    [Fact]
    public void ToolExecutablePath_IsEditableAndWritesBackToModel()
    {
        var tool = new ToolDefinition { ExecutablePath = "D:\\Tools\\Old.exe" };
        var viewModel = new StepItemViewModel(new TaskStep(), tool);

        viewModel.ExecutablePath = "D:\\Tools\\New.exe";

        Assert.Equal("D:\\Tools\\New.exe", tool.ExecutablePath);
    }

    [Fact]
    public void ToolWorkingDirectory_IsEditableAndWritesBackToModel()
    {
        var tool = new ToolDefinition { WorkingDirectory = "D:\\Tools\\Old" };
        var viewModel = new StepItemViewModel(new TaskStep(), tool);

        viewModel.WorkingDirectory = "D:\\Tools\\New";

        Assert.Equal("D:\\Tools\\New", tool.WorkingDirectory);
    }

    private static string GetMainWindowXamlPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "GameToolOrchestrator.sln");
            if (File.Exists(candidate))
            {
                return Path.Combine(directory.FullName, "src", "GameToolOrchestrator.Wpf", "MainWindow.xaml");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
