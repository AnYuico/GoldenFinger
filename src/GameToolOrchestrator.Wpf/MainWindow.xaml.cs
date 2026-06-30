using System.Windows;
using GameToolOrchestrator.Core.Abstractions;
using GameToolOrchestrator.Infrastructure.Automation;
using GameToolOrchestrator.Infrastructure.Configuration;
using GameToolOrchestrator.Infrastructure.Logging;
using GameToolOrchestrator.Wpf.Services;
using GameToolOrchestrator.Wpf.ViewModels;
using Microsoft.Win32;

namespace GameToolOrchestrator.Wpf;

public partial class MainWindow : Window
{
    private readonly UiExecutionLogger _logger;
    private readonly MainWindowViewModel _viewModel;
    private readonly WpfStartupLogger _startupLogger;

    public MainWindow()
        : this(new WpfStartupLogger())
    {
    }

    public MainWindow(WpfStartupLogger startupLogger)
    {
        _startupLogger = startupLogger;
        _startupLogger.Log("MainWindow constructor started.");

        _startupLogger.Log("开始创建 IConfigRepository / IUiAutomationService / IExecutionLogger / IExecutionEngineFactory。");

        var configRepository = new JsonConfigRepository();
        _startupLogger.Log("IConfigRepository 创建成功。");

        IUiAutomationService automationService = new FlaUiAutomationService();
        _startupLogger.Log("IUiAutomationService 创建成功。");

        var fileLogger = new SerilogExecutionLogger("logs");
        _startupLogger.Log("IExecutionLogger 创建成功。");

        MainWindowViewModel? viewModel = null;
        _logger = new UiExecutionLogger(fileLogger, (level, message) => viewModel?.AddLog(level, message));

        var factory = new DefaultExecutionEngineFactory(automationService, _logger);
        _startupLogger.Log("IExecutionEngine factory 创建成功。");
        _startupLogger.Log("IProcessLauncher 和 IActionExecutorFactory 将在执行任务时由 IExecutionEngineFactory 创建。");

        _viewModel = new MainWindowViewModel(
            configRepository,
            factory,
            automationService,
            new DefaultConfigResolver(),
            new MessageBoxConfirmationService(this),
            new SystemClipboardService(),
            new ExplorerFolderLauncherService());
        viewModel = _viewModel;
        _startupLogger.Log("MainViewModel 创建成功。");

        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        _startupLogger.Log("MainWindow InitializeComponent completed.");
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _startupLogger.Log("MainWindow Loaded started.");
            await _viewModel.InitializeDefaultConfigAsync(_startupLogger.Log);
            _startupLogger.Log("MainWindow Loaded completed.");
        }
        catch (Exception exception)
        {
            WpfCrashLogger.Log(exception, "MainWindow.Loaded initialization failed.");
            _startupLogger.LogException("默认配置初始化失败", exception);
            _viewModel.AddLog("Error", $"启动初始化失败：{exception.Message}");
        }
    }

    private async void BrowseConfig_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanBrowseConfig)
        {
            _viewModel.AddLog("Warning", "Cannot switch config while execution is running.");
            return;
        }

        if (!_viewModel.ConfirmDiscardUnsavedChanges("\u5207\u6362\u914d\u7f6e"))
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "JSON config (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.LoadConfigFromPathAsync(dialog.FileName, confirmUnsavedChanges: false);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _logger.Dispose();
        base.OnClosed(e);
    }
}
