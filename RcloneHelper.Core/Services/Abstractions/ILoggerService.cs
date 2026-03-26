namespace RcloneHelper.Services.Abstractions;

/// <summary>
/// 日志级别
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// 日志服务接口
/// </summary>
public interface ILoggerService
{
    /// <summary>
    /// 记录日志
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="message">日志消息</param>
    void Log(LogLevel level, string message);

    /// <summary>
    /// 记录调试信息
    /// </summary>
    void Debug(string message);

    /// <summary>
    /// 记录一般信息
    /// </summary>
    void Info(string message);

    /// <summary>
    /// 记录警告信息
    /// </summary>
    void Warning(string message);

    /// <summary>
    /// 记录错误信息
    /// </summary>
    void Error(string message);
}
