using System.Threading.Tasks;

namespace RcloneHelper.Services.Abstractions;

/// <summary>
/// 对话框服务接口
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 显示确认对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息内容</param>
    /// <returns>用户是否点击了确认</returns>
    Task<bool> ShowConfirmationAsync(string title, string message);
}
