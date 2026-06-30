namespace GameToolOrchestrator.Core.Configuration;

public sealed class MutationResult
{
    private MutationResult(bool succeeded, string errorMessage)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public string ErrorMessage { get; }

    public static MutationResult Success()
    {
        return new MutationResult(true, string.Empty);
    }

    public static MutationResult Failed(string errorMessage)
    {
        return new MutationResult(false, errorMessage);
    }
}
