namespace RcloneHelper.Services.Abstractions;

/// <summary>
/// 通知类型
/// </summary>
public enum NotificationType
{
    Success,
    Error,
    Warning,
    Info
}

/// <summary>
/// 通知服务接口
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 显示通知
    /// </summary>
    void Show(string message, NotificationType type = NotificationType.Info, int duration = 3000);

    /// <summary>
    /// 显示成功通知
    /// </summary>
    void ShowSuccess(string message, int duration = 3000);

    /// <summary>
    /// 显示错误通知
    /// </summary>
    void ShowError(string message, int duration = 4000);

    /// <summary>
    /// 显示警告通知
    /// </summary>
    void ShowWarning(string message, int duration = 3500);

    /// <summary>
    /// 通知显示事件
    /// </summary>
    event Action<string, NotificationType, int>? NotificationRequested;
}