namespace GameToolOrchestrator.Core.Models;

public enum CompletionStrategy
{
    ProcessExit,
    WindowClosed,
    ElementVisible,
    ElementNotVisible,
    LogContains,
    FileExists
}
