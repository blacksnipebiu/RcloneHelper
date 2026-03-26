using System;
using System.IO;
using System.Text.Json;
using RcloneHelper.Models;

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

    public static string AppDataDir => _appDataDir.Value;
    public static string MountsConfigPath => Path.Combine(AppDataDir, "mounts.json");
    public static string SettingsPath => Path.Combine(AppDataDir, "settings.json");
    public static string LogPath => Path.Combine(AppDataDir, "log");
    public static string RcloneConfigPath => Path.Combine(_rcloneConfigDir.Value, "rclone.conf");
    public static string RcloneCacheDir => Path.Combine(_rcloneConfigDir.Value, "cache");

    /// <summary>
    /// 获取配置的 rclone 路径
    /// </summary>
    public static string GetConfiguredRclonePath()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize(File.ReadAllText(SettingsPath), RcloneHelper.Helpers.AppJsonContext.Default.AppConfig);
                if (!string.IsNullOrEmpty(settings?.RclonePath) && File.Exists(settings.RclonePath))
                    return settings.RclonePath;
            }
        }
        catch { }
        return "";
    }
}