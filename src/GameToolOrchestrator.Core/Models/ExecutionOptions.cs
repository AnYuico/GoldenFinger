namespace GameToolOrchestrator.Core.Models;

public sealed class ExecutionOptions
{
    public bool StopOnFailure { get; set; } = true;

    public bool KillOnTimeout { get; set; } = false;

    public bool WaitForExit { get; set; } = true;

    public int DefaultActionTimeoutSeconds { get; set; } = 30;

    public int StepTimeoutSeconds { get; set; } = 0;

    public int ProcessExitTimeoutSeconds { get; set; } = 0;

    public string LogDirectory { get; set; } = "logs";

    public string ScreenshotDirectory { get; set; } = "screenshots";

    public TimeSpan? GetStepTimeout(TaskStep step)
    {
        var seconds = step.TimeoutSeconds ?? step.TimeoutMinutes * 60 ?? StepTimeoutSeconds;
        return seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
    }

    public TimeSpan? GetProcessExitTimeout()
    {
        return ProcessExitTimeoutSeconds > 0 ? TimeSpan.FromSeconds(ProcessExitTimeoutSeconds) : null;
    }
}
