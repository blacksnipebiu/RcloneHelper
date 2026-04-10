using System;
using System.IO;
using System.Reflection;

/// <summary>
/// 路径管理工具类
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
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "rclone"));

    private static readonly Lazy<string> _appLocation = new(() =>
    {
        var location = Assembly.GetExecutingAssembly().Location;
        return string.IsNullOrEmpty(location) ? AppContext.BaseDirectory : Path.GetDirectoryName(location) ?? AppContext.BaseDirectory;
    });

    public static string AppDataDir => _appDataDir.Value;
    public static string AppLocation => _appLocation.Value;
    public static string MountsConfigPath => Path.Combine(AppDataDir, "mounts.json");
    public static string SettingsPath => Path.Combine(AppDataDir, "settings.json");
    public static string LogPath => Path.Combine(AppDataDir, "log");
    public static string RcloneConfigPath => Path.Combine(_rcloneConfigDir.Value, "rclone.conf");
    public static string RcloneCacheDir => Path.Combine(_rcloneConfigDir.Value, "cache");

    /// <summary>
    /// 原子写入文件内容
    /// 使用临时文件+重命名的方式确保写入失败不会损坏原文件
    /// </summary>
    /// <param name="filePath">目标文件路径</param>
    /// <param name="content">要写入的内容</param>
    public static void AtomicWriteAllText(string filePath, string content)
    {
        var directory = Path.GetDirectoryName(filePath) ?? throw new ArgumentException("无法获取文件目录", nameof(filePath));

        // 确保目录存在
        Directory.CreateDirectory(directory);

        // 使用随机文件名避免冲突
        var tempPath = Path.Combine(directory, $".tmp_{Guid.NewGuid():N}");

        try
        {
            // 写入临时文件
            File.WriteAllText(tempPath, content);

            // 验证临时文件写入成功
            if (!File.Exists(tempPath))
            {
                throw new IOException("临时文件创建失败");
            }

            // 使用 Replace 方法进行原子替换（在 Windows 上是原子的）
            // 如果目标文件不存在，直接移动
            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, null);
            }
            else
            {
                File.Move(tempPath, filePath);
            }
        }
        finally
        {
            // 清理可能残留的临时文件
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }
}