using System.Collections.Generic;

namespace RcloneHelper.Services.Abstractions;

/// <summary>
/// 系统服务接口，提供跨平台的系统级功能
/// </summary>
public interface ISystemService
{
    #region 开机自启

    /// <summary>
    /// 检查是否已设置开机自启
    /// </summary>
    bool IsAutoStartEnabled { get; }

    /// <summary>
    /// 设置开机自启
    /// </summary>
    /// <param name="enabled">是否启用</param>
    /// <returns>操作是否成功</returns>
    bool SetAutoStart(bool enabled);

    #endregion

    #region 挂载点管理

    /// <summary>
    /// 获取可用的挂载点
    /// </summary>
    /// <returns>Windows: 盘符如 "Z:"; Linux/macOS: 目录路径如 "/mnt/rclone"</returns>
    string GetAvailableMountPoint();

    /// <summary>
    /// 检查挂载点是否已被占用
    /// </summary>
    /// <param name="mountPoint">挂载点路径</param>
    /// <returns>是否已被占用</returns>
    bool IsMountPointOccupied(string mountPoint);

    /// <summary>
    /// 获取系统已使用的挂载点列表
    /// </summary>
    /// <returns>已使用的挂载点列表</returns>
    IReadOnlySet<string> GetUsedMountPoints();

    #endregion

    #region 进程管理

    /// <summary>
    /// 查找指定名称的进程
    /// </summary>
    /// <param name="processName">进程名称（不含扩展名）</param>
    /// <returns>进程信息列表</returns>
    IEnumerable<ProcessInfo> FindProcesses(string processName);

    /// <summary>
    /// 终止指定进程
    /// </summary>
    /// <param name="processId">进程ID</param>
    /// <returns>操作是否成功</returns>
    bool TerminateProcess(int processId);

    #endregion

    #region 系统信息

    /// <summary>
    /// 获取当前平台类型
    /// </summary>
    PlatformType Platform { get; }

    /// <summary>
    /// 获取应用程序可执行文件路径
    /// </summary>
    string AppExecutablePath { get; }

    #endregion
}

/// <summary>
/// 进程信息
/// </summary>
public class ProcessInfo
{
    /// <summary>
    /// 进程ID
    /// </summary>
    public int ProcessId { get; init; }

    /// <summary>
    /// 进程名称
    /// </summary>
    public string ProcessName { get; init; } = "";

    /// <summary>
    /// 命令行参数
    /// </summary>
    public string CommandLine { get; init; } = "";

    /// <summary>
    /// 启动时间
    /// </summary>
    public DateTime StartTime { get; init; }
}

/// <summary>
/// 平台类型
/// </summary>
public enum PlatformType
{
    Windows,
    Linux,
    macOS,
    Unknown
}