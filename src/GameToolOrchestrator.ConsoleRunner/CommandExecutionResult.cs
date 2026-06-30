namespace GameToolOrchestrator.ConsoleRunner;

public sealed record CommandExecutionResult(int ExitCode, string Output, string Error = "");
