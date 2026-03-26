namespace RcloneHelper.Models;

/// <summary>
/// 应用程序配置
/// </summary>
public class AppConfig
{
    /// <summary>
    /// 启动时自动挂载所有存储
    /// </summary>
    public bool AutoMountOnStart { get; set; } = false;

    /// <summary>
    /// rclone 可执行文件路径
    /// </summary>
    public string RclonePath { get; set; } = "";

    /// <summary>
    /// 是否使用深色模式
    /// </summary>
    public bool IsDarkMode { get; set; } = true;
}
