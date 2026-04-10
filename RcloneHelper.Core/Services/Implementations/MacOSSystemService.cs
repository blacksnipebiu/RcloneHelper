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
/// macOS 平台系统服务实现
/// </summary>
[SupportedOSPlatform("osx")]
public class MacOSSystemService : ISystemService
{
    private const string AppName = "com.rclonehelper.app";
    private const string LaunchAgentsDir = "Library/LaunchAgents";
    private const string DefaultMountBase = "/Volumes";
    private readonly ILoggerService _logger;

    public MacOSSystemService(ILoggerService logger)
    {
        _logger = logger;
    }

    #region ISystemService 实现

    public OSPlatform Platform => OSPlatform.OSX;

    public string AppExecutablePath => System.Reflection.Assembly.GetExecutingAssembly().Location;

    #region 开机自启

    public bool IsAutoStartEnabled
    {
        get
        {
            var plistPath = GetLaunchAgentPlistPath();
            return File.Exists(plistPath);
        }
    }

    public bool SetAutoStart(bool enabled)
    {
        try
        {
            if (enabled)
            {
                return CreateLaunchAgent();
            }
            else
            {
                return RemoveLaunchAgent();
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
        // macOS 默认使用 /Volumes 目录
        var baseDir = DefaultMountBase;

        // 查找可用的挂载点
        for (int i = 1; i <= 26; i++)
        {
            var mountPoint = Path.Combine(baseDir, $"Rclone{i}");
            if (!IsMountPointOccupied(mountPoint))
                return mountPoint;
        }

        // 回退到用户目录
        var userMount = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "mnt", "rclone");
        Directory.CreateDirectory(Path.GetDirectoryName(userMount)!);
        return userMount;
    }

    public bool IsMountPointOccupied(string mountPoint)
    {
        if (string.IsNullOrEmpty(mountPoint))
            return true;

        // 检查目录是否存在
        if (Directory.Exists(mountPoint))
        {
            // 使用 mount 命令检查是否已挂载
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
            // 使用 mount 命令获取已挂载的文件系统
            var mountOutput = ExecuteCommand("mount");
            var lines = mountOutput.Split('\n');

            foreach (var line in lines)
            {
                // macOS mount 输出格式: /dev/diskX on /Volumes/Name (type, ...)
                var onIndex = line.IndexOf(" on ");
                if (onIndex > 0)
                {
                    var rest = line.Substring(onIndex + 4);
                    var parenIndex = rest.IndexOf(" (");
                    if (parenIndex > 0)
                    {
                        var mountPoint = rest.Substring(0, parenIndex);
                        usedMounts.Add(mountPoint);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"获取挂载点列表失败: {ex.Message}");
        }

        return usedMounts;
    }

    public IReadOnlyDictionary<string, string> GetActiveMountNames()
    {
        var mounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // 使用 mount 命令获取挂载信息
            // macOS格式: rclone:挂载名 on /Volumes/RcloneName (osxfuse, ...)
            // 或: rclone:挂载名 on /Users/xxx/mnt/rclone (macfuse, ...)
            var output = ExecuteCommand("mount 2>/dev/null | grep '^rclone:'");
            if (string.IsNullOrWhiteSpace(output))
            {
                // 如果grep没有结果，尝试解析完整mount输出
                output = ExecuteCommand("mount 2>/dev/null");
            }
            
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            _logger.Debug($"[GetActiveMounts] mount 输出 {lines.Length} 行");

            foreach (var line in lines)
            {
                // 查找rclone挂载: "rclone:挂载名 on /挂载点 ..."
                if (!line.StartsWith("rclone:")) continue;
                
                var onIndex = line.IndexOf(" on ");
                if (onIndex > 0)
                {
                    var remote = line.Substring(0, onIndex);
                    var rest = line.Substring(onIndex + 4);
                    
                    if (remote.StartsWith("rclone:"))
                    {
                        var name = remote.Substring("rclone:".Length);
                        // 提取挂载点 (在 "(" 或 " type" 之前)
                        var parenIndex = rest.IndexOf(" (");
                        var typeIndex = rest.IndexOf(" type");
                        var endIndex = Math.Min(
                            parenIndex >= 0 ? parenIndex : int.MaxValue,
                            typeIndex >= 0 ? typeIndex : int.MaxValue
                        );
                        
                        var mountPoint = endIndex < int.MaxValue ? rest.Substring(0, endIndex) : rest.Trim();
                        mountPoint = mountPoint.Trim();
                        mounts[name] = mountPoint;
                        _logger.Debug($"[GetActiveMounts] 发现 rclone 挂载: {name} -> {mountPoint}");
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
        // 检查 macFUSE 是否安装（通过检查 /usr/local/lib/libfuse*.dylib 或 kext）
        var dependency = new SystemDependency
        {
            Name = "macFUSE",
            Description = "macOS FUSE 驱动，rclone mount 必需",
            InstallUrl = "https://github.com/osxfuse/macfuse/releases",
            Icon = "🍎"
        };

        try
        {
            // 检查关键文件或库是否存在
            dependency.Status = (
                File.Exists("/usr/local/lib/libfuse.2.dylib") ||
                File.Exists("/usr/local/lib/libfuse.1.dylib") ||
                Directory.Exists("/System/Library/Extensions/fusefs.kext")
            ) ? DependencyStatus.Installed : DependencyStatus.NotInstalled;
        }
        catch (Exception ex)
        {
            _logger.Debug($"检测 macFUSE 失败: {ex.Message}");
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

    private static string GetLaunchAgentPlistPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, LaunchAgentsDir, $"{AppName}.plist");
    }

    private bool CreateLaunchAgent()
    {
        try
        {
            var plistDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                LaunchAgentsDir);

            Directory.CreateDirectory(plistDir);

            var plistPath = Path.Combine(plistDir, $"{AppName}.plist");

            // 创建 LaunchAgent plist 文件
            var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>{AppName}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{AppExecutablePath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <false/>
</dict>
</plist>";

            File.WriteAllText(plistPath, plistContent);

            // 加载 LaunchAgent
            ExecuteCommand($"launchctl load {plistPath}");

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"创建 LaunchAgent 失败: {ex.Message}");
            return false;
        }
    }

    private bool RemoveLaunchAgent()
    {
        try
        {
            var plistPath = GetLaunchAgentPlistPath();

            if (File.Exists(plistPath))
            {
                // 卸载 LaunchAgent
                ExecuteCommand($"launchctl unload {plistPath}");
                File.Delete(plistPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"删除 LaunchAgent 失败: {ex.Message}");
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

    #endregion
}