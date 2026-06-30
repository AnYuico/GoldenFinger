namespace GameToolOrchestrator.Wpf.Services;

public interface IUserConfirmationService
{
    bool ConfirmDiscardUnsavedChanges(string operationName);

    bool ConfirmDelete(string objectDescription);
}
