using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using RcloneHelper.Helpers;
using RcloneHelper.Models;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Services.Implementations;

/// <summary>
/// Linux 平台系统服务实现
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxSystemService : ISystemService
{
    private const string AppName = "rclonehelper";
    private const string AutostartDir = ".config/autostart";
    private const string SystemdUserDir = ".config/systemd/user";
    private const string DefaultMountBase = "/mnt";
    private readonly ILoggerService _logger;

    public LinuxSystemService(ILoggerService logger)
    {
        _logger = logger;
    }

    #region ISystemService 实现

    public OSPlatform Platform => OSPlatform.Linux;

    public string AppExecutablePath => System.Reflection.Assembly.GetExecutingAssembly().Location;

    #region 开机自启

    public bool IsAutoStartEnabled
    {
        get
        {
            // 方案1: 检查 systemd user service
            var systemdServicePath = GetSystemdServicePath();
            if (File.Exists(systemdServicePath))
                return true;

            // 方案2: 检查 XDG autostart
            var autostartPath = GetAutostartDesktopPath();
            return File.Exists(autostartPath);
        }
    }

    public bool SetAutoStart(bool enabled)
    {
        try
        {
            if (enabled)
            {
                return CreateAutostartEntry();
            }
            else
            {
                return RemoveAutostartEntry();
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"设置开机启动失败: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region 挂载点管理

    public string GetAvailableMountPoint()
    {
        var baseDir = DefaultMountBase;

        // 检查用户是否有权限在 /mnt 下创建目录
        // 如果没有，使用 ~/mnt
        if (!HasWritePermission(baseDir))
        {
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "mnt");
            Directory.CreateDirectory(baseDir);
        }

        // 查找可用的挂载点
        for (int i = 1; i <= 26; i++)
        {
            var mountPoint = Path.Combine(baseDir, $"rclone{i}");
            if (!IsMountPointOccupied(mountPoint))
                return mountPoint;
        }

        // 回退到默认
        return Path.Combine(baseDir, "rclone");
    }

    public bool IsMountPointOccupied(string mountPoint)
    {
        if (string.IsNullOrEmpty(mountPoint))
            return true;

        // 检查目录是否存在
        if (Directory.Exists(mountPoint))
        {
            // 检查是否为挂载点
            try
            {
                var mountOutput = ExecuteCommand("mount");
                return mountOutput.Contains(mountPoint);
            }
            catch (Exception ex)
            {
                _logger.Debug($"检查挂载点状态失败: {ex.Message}");
                return true;
            }
        }

        return false;
    }

    public IReadOnlySet<string> GetUsedMountPoints()
    {
        var usedMounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // 读取 /proc/mounts 获取已挂载的文件系统
            if (File.Exists("/proc/mounts"))
            {
                var lines = File.ReadAllLines("/proc/mounts");
                foreach (var line in lines)
                {
                    var parts = line.Split(' ');
                    if (parts.Length >= 2)
                    {
                        usedMounts.Add(parts[1]);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"读取 /proc/mounts 失败: {ex.Message}");
        }

        return usedMounts;
    }

    public IReadOnlyDictionary<string, string> GetActiveMountNames()
    {
        var mounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // 从 /proc/mounts 获取挂载信息
            // rclone 挂载格式: rclone:<挂载名> <挂载点> fuse.rclone ...
            if (File.Exists("/proc/mounts"))
            {
                var lines = File.ReadAllLines("/proc/mounts");
                _logger.Debug($"[GetActiveMounts] /proc/mounts 共有 {lines.Length} 行");

                foreach (var line in lines)
                {
                    // 格式: rclone:挂载名 /mnt/rclone fuse.rclone ...
                    // 或: rclone:挂载名 /home/user/mnt fuse.rclone ...
                    if (line.StartsWith("rclone:"))
                    {
                        var parts = line.Split(' ');
                        if (parts.Length >= 2)
                        {
                            var remote = parts[0]; // rclone:挂载名
                            var mountPoint = parts[1];

                            if (remote.StartsWith("rclone:"))
                            {
                                var name = remote.Substring("rclone:".Length);
                                mounts[name] = mountPoint;
                                _logger.Debug($"[GetActiveMounts] 发现 rclone 挂载: {name} -> {mountPoint}");
                            }
                        }
                    }
                }
            }
            else
            {
                // 备用方案: 使用 mount 命令
                _logger.Debug("[GetActiveMounts] /proc/mounts 不存在，使用 mount 命令");
                var output = ExecuteCommand("mount 2>/dev/null | grep '^rclone:'");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        // 格式: rclone:挂载名 on /mnt/rclone type fuse.rclone (...)
                        var onIndex = line.IndexOf(" on ");
                        if (onIndex > 0)
                        {
                            var remote = line.Substring(0, onIndex);
                            var rest = line.Substring(onIndex + 4);
                            
                            if (remote.StartsWith("rclone:"))
                            {
                                var name = remote.Substring("rclone:".Length);
                                // 提取挂载点 (在 " type" 之前)
                                var typeIndex = rest.IndexOf(" type");
                                var mountPoint = typeIndex > 0 ? rest.Substring(0, typeIndex) : rest.Split(' ')[0];
                                mounts[name] = mountPoint;
                                _logger.Debug($"[GetActiveMounts] (mount命令) 发现 rclone 挂载: {name} -> {mountPoint}");
                            }
                        }
                    }
                }
            }

            _logger.Debug($"[GetActiveMounts] 共发现 {mounts.Count} 个 rclone 挂载");
        }
        catch (Exception ex)
        {
            _logger.Warning($"获取活动挂载失败: {ex.Message}");
        }

        return mounts;
    }

    #endregion

    #region 系统信息

    public SystemDependency? GetFuseDependency()
    {
        // 检查 /dev/fuse 是否存在
        var dependency = new SystemDependency
        {
            Name = "FUSE",
            Description = "Linux FUSE 驱动，rclone mount 必需",
            InstallUrl = "https://github.com/libfuse/libfuse",
            Icon = "🐧"
        };

        try
        {
            dependency.Status = File.Exists("/dev/fuse") ? DependencyStatus.Installed : DependencyStatus.NotInstalled;
        }
        catch (Exception ex)
        {
            _logger.Debug($"检查 FUSE 失败: {ex.Message}");
            dependency.Status = DependencyStatus.NotInstalled;
        }

        return dependency;
    }

    public SystemDependency? GetRcloneDependency()
    {
        var dependency = new SystemDependency
        {
            Name = "rclone",
            Description = "rclone 命令行工具，核心依赖",
            InstallUrl = "https://rclone.org/downloads/",
            Icon = "☁️"
        };

        try
        {
            string? rclonePath = null;

            // 1. 优先查找 ~/.config/rclonehelper 目录
            var appDataRclone = Path.Combine(PathUtil.AppDataDir, "rclone");
            if (File.Exists(appDataRclone))
            {
                rclonePath = appDataRclone;
            }

            // 2. 查找程序目录
            if (rclonePath == null)
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var localRclone = Path.Combine(appDir, "rclone");
                if (File.Exists(localRclone))
                {
                    rclonePath = localRclone;
                }
            }

            // 3. 从 PATH 环境变量查找
            if (rclonePath == null)
            {
                rclonePath = FindRcloneInPath();
            }

            // 4. 验证 rclone 是否可用
            var isInstalled = false;
            if (rclonePath != null)
            {
                isInstalled = TestRcloneExecution(rclonePath);
            }

            dependency.Status = isInstalled ? DependencyStatus.Installed : DependencyStatus.NotInstalled;
        }
        catch (Exception ex)
        {
            _logger.Debug($"检测 rclone 失败: {ex.Message}");
            dependency.Status = DependencyStatus.NotInstalled;
        }

        return dependency;
    }

    private string? FindRcloneInPath()
    {
        try
        {
            var output = ExecuteCommand("which rclone 2>/dev/null");
            if (!string.IsNullOrWhiteSpace(output))
            {
                var path = output.Trim().Split('\n')[0].Trim();
                if (File.Exists(path))
                    return path;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FindRcloneInPath 失败: {ex.Message}");
        }

        return null;
    }

    private bool TestRcloneExecution(string rclonePath)
    {
        try
        {
            var output = ExecuteCommand($"{rclonePath} version 2>/dev/null");
            return !string.IsNullOrWhiteSpace(output);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TestRcloneExecution 失败: {rclonePath}, 错误: {ex.Message}");
            return false;
        }
    }

    #endregion

    #endregion

    #region 私有方法

    private static string GetAutostartDesktopPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, AutostartDir, $"{AppName}.desktop");
    }

    private static string GetSystemdServicePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, SystemdUserDir, $"{AppName}.service");
    }

    private bool CreateAutostartEntry()
    {
        try
        {
            // 创建 XDG autostart .desktop 文件
            var autostartDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                AutostartDir);

            Directory.CreateDirectory(autostartDir);

            var desktopPath = Path.Combine(autostartDir, $"{AppName}.desktop");
            var desktopContent = $@"[Desktop Entry]
Type=Application
Name=RcloneHelper
Exec={AppExecutablePath}
Icon=rclonehelper
Comment=Rclone Mount Manager
Terminal=false
Categories=Network;FileTransfer;
X-GNOME-Autostart-enabled=true
";
            File.WriteAllText(desktopPath, desktopContent);

            // 设置可执行权限
            ExecuteCommand($"chmod +x {desktopPath}");

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"创建开机启动项失败: {ex.Message}");
            return false;
        }
    }

    private bool RemoveAutostartEntry()
    {
        try
        {
            var autostartPath = GetAutostartDesktopPath();
            if (File.Exists(autostartPath))
                File.Delete(autostartPath);

            var systemdPath = GetSystemdServicePath();
            if (File.Exists(systemdPath))
                File.Delete(systemdPath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"删除开机启动项失败: {ex.Message}");
            return false;
        }
    }

    private static bool HasWritePermission(string path)
    {
        try
        {
            var testFile = Path.Combine(path, $".write_test_{Guid.NewGuid()}");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ExecuteCommand(string command)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output;
    }

    private static DateTime SafeGetStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取进程启动时间失败: {ex.Message}");
            return DateTime.MinValue;
        }
    }

    private static DateTime GetProcessStartTime(string pid)
    {
        try
        {
            var statPath = $"/proc/{pid}/stat";
            if (File.Exists(statPath))
            {
                var stat = File.ReadAllText(statPath);
                var parts = stat.Split(' ');
                if (parts.Length > 21)
                {
                    // starttime 是第22个字段（从0开始索引是21）
                    // 需要转换为实际时间
                    // 简化处理，返回当前时间
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取进程启动时间失败: {pid}, 错误: {ex.Message}");
        }

        return DateTime.MinValue;
    }

    #endregion
}