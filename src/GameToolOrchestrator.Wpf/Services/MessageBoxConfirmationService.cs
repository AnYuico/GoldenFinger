using System.Windows;

namespace GameToolOrchestrator.Wpf.Services;

public sealed class MessageBoxConfirmationService : IUserConfirmationService
{
    private readonly Window? _owner;

    public MessageBoxConfirmationService(Window? owner = null)
    {
        _owner = owner;
    }

    public bool ConfirmDiscardUnsavedChanges(string operationName)
    {
        var result = MessageBox.Show(
            _owner,
            $"当前配置有未保存修改。是否放弃修改并继续{operationName}？",
            "未保存修改",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    public bool ConfirmDelete(string objectDescription)
    {
        var result = MessageBox.Show(
            _owner,
            $"确认删除 {objectDescription}？此操作只会修改当前配置，保存前仍可通过重新加载配置放弃修改。",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }
}
