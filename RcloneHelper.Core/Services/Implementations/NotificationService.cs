using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Services.Implementations;

/// <summary>
/// 通知服务实现
/// </summary>
public class NotificationService : INotificationService
{
    public event Action<string, NotificationType, int>? NotificationRequested;

    public void Show(string message, NotificationType type = NotificationType.Info, int duration = 3000)
    {
        NotificationRequested?.Invoke(message, type, duration);
    }

    public void ShowSuccess(string message, int duration = 3000)
    {
        Show(message, NotificationType.Success, duration);
    }

    public void ShowError(string message, int duration = 4000)
    {
        Show(message, NotificationType.Error, duration);
    }

    public void ShowWarning(string message, int duration = 3500)
    {
        Show(message, NotificationType.Warning, duration);
    }
}