using GameToolOrchestrator.Wpf.Services;
using GameToolOrchestrator.Wpf.ViewModels;

namespace GameToolOrchestrator.Tests;

public sealed class WpfStartupSafetyTests
{
    [Fact]
    public async Task InitializeDefaultConfigAsync_WhenDefaultConfigMissing_DoesNotThrow()
    {
        var tempRoot = CreateTempDirectory();
        var repository = new InMemoryConfigRepository();
        var viewModel = CreateViewModel(repository, new DefaultConfigResolver([tempRoot]));

        await viewModel.InitializeDefaultConfigAsync();

        Assert.Empty(viewModel.TaskPlans);
        Assert.Empty(viewModel.Steps);
        Assert.Empty(viewModel.Actions);
        Assert.Contains(viewModel.Logs, log => log.Message.Contains("未找到默认配置文件"));
        Assert.False(viewModel.StartExecutionCommand.CanExecute(null));
        Assert.True(viewModel.LoadConfigCommand.CanExecute(null));
    }

    [Fact]
    public async Task InitializeDefaultConfigAsync_WhenJsonIsBroken_DoesNotThrowAndLogsError()
    {
        var tempRoot = CreateTempDirectory();
        var configPath = Path.Combine(tempRoot, "config.json");
        await File.WriteAllTextAsync(configPath, "{ broken json");

        var repository = new InMemoryConfigRepository
        {
            LoadException = new InvalidOperationException("JSON format error")
        };
        var viewModel = CreateViewModel(repository, new DefaultConfigResolver([tempRoot]));

        await viewModel.InitializeDefaultConfigAsync();

        Assert.Empty(viewModel.TaskPlans);
        Assert.Contains(viewModel.Logs, log => log.Level == "Error" && log.Message.Contains("JSON format error"));
        Assert.True(viewModel.LoadConfigCommand.CanExecute(null));
    }

    [Fact]
    public void StartCommand_WhenNoTaskPlanSelected_IsDisabled()
    {
        var viewModel = CreateViewModel(
            new InMemoryConfigRepository(),
            new DefaultConfigResolver([CreateTempDirectory()]));

        Assert.False(viewModel.StartExecutionCommand.CanExecute(null));
    }

    [Fact]
    public void DefaultConfigResolver_ChecksProvidedCurrentAndBaseDirectories()
    {
        var baseDirectory = CreateTempDirectory();
        var currentDirectory = CreateTempDirectory();
        var resolver = new DefaultConfigResolver([baseDirectory, currentDirectory]);

        var result = resolver.FindDefaultConfig();

        Assert.Contains(Path.Combine(baseDirectory, "config.json"), result.CandidatePaths);
        Assert.Contains(Path.Combine(baseDirectory, "sample-config.json"), result.CandidatePaths);
        Assert.Contains(Path.Combine(currentDirectory, "config.json"), result.CandidatePaths);
        Assert.Contains(Path.Combine(currentDirectory, "sample-config.json"), result.CandidatePaths);
    }

    [Fact]
    public void DefaultConfigResolver_PrioritizesBaseDirectoryBeforeCurrentDirectory()
    {
        var baseDirectory = CreateTempDirectory();
        var currentDirectory = CreateTempDirectory();
        var resolver = new DefaultConfigResolver([baseDirectory, currentDirectory]);

        var result = resolver.FindDefaultConfig();

        Assert.Equal(Path.Combine(baseDirectory, "config.json"), result.CandidatePaths[0]);
        Assert.Equal(Path.Combine(baseDirectory, "sample-config.json"), result.CandidatePaths[1]);
        Assert.Equal(Path.Combine(currentDirectory, "config.json"), result.CandidatePaths[2]);
        Assert.Equal(Path.Combine(currentDirectory, "sample-config.json"), result.CandidatePaths[3]);
    }

    [Fact]
    public void DefaultConfigResolver_DefaultRootsIncludeAppBaseAndCurrentDirectory()
    {
        var resolver = new DefaultConfigResolver();

        var result = resolver.FindDefaultConfig();

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "config.json"), result.CandidatePaths[0]);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "sample-config.json"), result.CandidatePaths[1]);
        Assert.Contains(Path.Combine(Environment.CurrentDirectory, "config.json"), result.CandidatePaths);
        Assert.Contains(Path.Combine(Environment.CurrentDirectory, "sample-config.json"), result.CandidatePaths);
    }

    [Fact]
    public void DefaultConfigResolver_WhenConfigMissing_ReturnsCandidatesWithoutThrowing()
    {
        var publishDirectory = CreateTempDirectory();
        var resolver = new DefaultConfigResolver([publishDirectory]);

        var result = resolver.FindDefaultConfig();

        Assert.Null(result.FoundPath);
        Assert.NotEmpty(result.CandidatePaths);
    }

    [Fact]
    public void DefaultConfigResolver_CanFindSampleConfigBesidePublishedExe()
    {
        var publishDirectory = CreateTempDirectory();
        var sampleConfig = Path.Combine(publishDirectory, "sample-config.json");
        File.WriteAllText(sampleConfig, "{}");
        var resolver = new DefaultConfigResolver([publishDirectory]);

        var result = resolver.FindDefaultConfig();

        Assert.Equal(sampleConfig, result.FoundPath);
    }

    [Fact]
    public void DefaultConfigResolver_ExplicitPublishRootsDoNotDependOnSourceDirectories()
    {
        var publishDirectory = CreateTempDirectory();
        var currentDirectory = CreateTempDirectory();
        var resolver = new DefaultConfigResolver([publishDirectory, currentDirectory]);

        var result = resolver.FindDefaultConfig();

        Assert.DoesNotContain(result.CandidatePaths, path =>
            path.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AssemblyAppVersionProvider_ReturnsNonEmptyVersion()
    {
        var provider = new AssemblyAppVersionProvider();

        Assert.False(string.IsNullOrWhiteSpace(provider.Version));
        Assert.Contains("0.3.2", provider.Version);
    }

    [Fact]
    public void WpfStartupLogger_CreatesLogsDirectory()
    {
        var tempRoot = CreateTempDirectory();
        var logDirectory = Path.Combine(tempRoot, "logs");

        var logger = new WpfStartupLogger(logDirectory);
        logger.BeginSession();
        logger.Log("test");

        Assert.True(Directory.Exists(logDirectory));
        Assert.True(File.Exists(Path.Combine(logDirectory, "wpf-startup.log")));
    }

    [Fact]
    public void WpfCrashLogger_WritesCrashLog()
    {
        GameToolOrchestrator.Wpf.Services.WpfCrashLogger.Log(
            new InvalidOperationException("synthetic crash for test"),
            "unit-test");

        Assert.True(File.Exists(GameToolOrchestrator.Wpf.Services.WpfCrashLogger.LogPath));
        var text = File.ReadAllText(GameToolOrchestrator.Wpf.Services.WpfCrashLogger.LogPath);
        Assert.Contains("synthetic crash for test", text);
    }

    private static MainWindowViewModel CreateViewModel(
        InMemoryConfigRepository repository,
        IDefaultConfigResolver resolver)
    {
        return new MainWindowViewModel(
            repository,
            new FakeExecutionEngineFactory(),
            new FakeUiAutomationService(),
            resolver);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gto-wpf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
