using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GameToolOrchestrator.Wpf.Services;

namespace GameToolOrchestrator.Wpf;

public partial class App : Application
{
    private WpfStartupLogger? _startupLogger;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            _startupLogger = new WpfStartupLogger();
            _startupLogger.BeginSession();
            _startupLogger.Log("OnStartup started.");
            base.OnStartup(e);

            _startupLogger.Log("开始创建 MainWindow。");
            var window = new MainWindow(_startupLogger);
            MainWindow = window;
            _startupLogger.Log("MainWindow 创建成功。");
            window.Show();
            _startupLogger.Log("MainWindow Show completed.");
        }
        catch (Exception exception)
        {
            WpfCrashLogger.Log(exception, "OnStartup failed while creating MainWindow.");
            _startupLogger?.LogException("MainWindow 创建失败", exception);
            WpfCrashLogger.ShowMessageBox("主窗口创建失败，程序无法继续启动。");
            Shutdown(1);
        }
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        WpfCrashLogger.Log(e.Exception, "DispatcherUnhandledException");
        WpfCrashLogger.ShowMessageBox("界面线程发生异常，程序已尝试继续运行。");
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            WpfCrashLogger.Log(exception, "AppDomain.CurrentDomain.UnhandledException");
        }
        else
        {
            WpfCrashLogger.Log(
                new InvalidOperationException($"Unhandled non-exception object: {e.ExceptionObject}"),
                "AppDomain.CurrentDomain.UnhandledException");
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WpfCrashLogger.Log(e.Exception, "TaskScheduler.UnobservedTaskException");
        e.SetObserved();
    }
}
