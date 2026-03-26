using System;
using System.IO;

namespace RcloneHelper.Helpers;

/// <summary>
/// 路径管理工具类，统一管理应用程序的所有文件路径
/// </summary>
public static class PathUtil
{
    private static readonly Lazy<string> _appDataDir = new(() =>
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "RcloneHelper");
        Directory.CreateDirectory(dir);
        return dir;
    });

    private static readonly Lazy<string> _rcloneConfigDir = new(() =>
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "rclone");
    });

    /// <summary>
    /// 应用程序数据目录 (%APPDATA%\RcloneHelper)
    /// </summary>
    public static string AppDataDir => _appDataDir.Value;

    /// <summary>
    /// 挂载配置文件路径 (mounts.json)
    /// </summary>
    public static string MountsConfigPath => Path.Combine(AppDataDir, "mounts.json");

    /// <summary>
    /// 应用设置文件路径 (settings.json)
    /// </summary>
    public static string SettingsPath => Path.Combine(AppDataDir, "settings.json");

    /// <summary>
    /// 应用日志文件路径 (app.log)
    /// </summary>
    public static string LogPath => Path.Combine(AppDataDir, "app.log");

    /// <summary>
    /// rclone 配置文件路径 (rclone.conf)
    /// </summary>
    public static string RcloneConfigPath => Path.Combine(_rcloneConfigDir.Value, "rclone.conf");

    /// <summary>
    /// rclone 缓存目录路径
    /// </summary>
    public static string RcloneCacheDir => Path.Combine(_rcloneConfigDir.Value, "cache");

    /// <summary>
    /// 查找 rclone 可执行文件路径
    /// </summary>
    /// <returns>rclone.exe 的完整路径，如果找不到则返回 "rclone"</returns>
    public static string FindRclonePath()
    {
        // 1. 检查 PATH 环境变量
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = pathEnv.Split(Path.PathSeparator);

        foreach (var path in paths)
        {
            var fullPath = Path.Combine(path, "rclone.exe");
            if (File.Exists(fullPath))
                return fullPath;
        }

        // 2. 检查 Program Files
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "rclone",
            "rclone.exe");
        if (File.Exists(defaultPath))
            return defaultPath;

        // 3. 检查应用程序目录
        var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rclone.exe");
        if (File.Exists(localPath))
            return localPath;

        // 4. 返回默认值，依赖系统 PATH
        return "rclone";
    }

    /// <summary>
    /// 确保目录存在
    /// </summary>
    /// <param name="path">目录路径</param>
    public static void EnsureDirectoryExists(string path)
    {
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// 获取应用程序可执行文件路径
    /// </summary>
    public static string AppExecutablePath => System.Reflection.Assembly.GetExecutingAssembly().Location;
}