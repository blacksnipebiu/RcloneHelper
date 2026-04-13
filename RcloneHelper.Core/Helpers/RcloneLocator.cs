using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace RcloneHelper.Helpers;

/// <summary>
/// rclone 路径查找辅助类
/// </summary>
public static class RcloneLocator
{
    private const string WindowsExe = "rclone.exe";
    private const string UnixBinary = "rclone";

    /// <summary>
    /// 获取 rclone 可执行文件路径
    /// </summary>
    /// <returns>rclone 路径，如果未找到则返回 "rclone" 作为回退</returns>
    public static string GetRclonePath()
    {
        // 1. 优先查找 %APPDATA%\RcloneHelper 目录
        var appDataRclone = Path.Combine(PathUtil.AppDataDir, GetExecutableName());
        if (File.Exists(appDataRclone))
            return appDataRclone;

        // 2. 查找程序目录
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var localRclone = Path.Combine(appDir, GetExecutableName());
        if (File.Exists(localRclone))
            return localRclone;

        // 3. 从 PATH 环境变量查找
        var pathRclone = FindInPath();
        if (pathRclone != null)
            return pathRclone;

        // 4. 回退到使用 "rclone"，依赖系统 PATH
        return GetExecutableName();
    }

    /// <summary>
    /// 在系统 PATH 中查找 rclone
    /// </summary>
    /// <returns>rclone 路径，如果未找到则返回 null</returns>
    public static string? FindInPath()
    {
        try
        {
            var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "rclone",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var path = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                if (File.Exists(path))
                    return path;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RcloneLocator: 查找 rclone 失败 - {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 测试 rclone 是否可用
    /// </summary>
    /// <param name="rclonePath">rclone 路径</param>
    /// <returns>是否可用</returns>
    public static bool TestExecution(string rclonePath)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = rclonePath,
                    Arguments = "version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RcloneLocator: 测试 rclone 失败 ({rclonePath}) - {ex.Message}");
            return false;
        }
    }

    private static string GetExecutableName()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? WindowsExe : UnixBinary;
    }
}
