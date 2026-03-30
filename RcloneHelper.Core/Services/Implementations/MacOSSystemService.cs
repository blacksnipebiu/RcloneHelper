using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

    #region ISystemService 实现

    public PlatformType Platform => PlatformType.macOS;

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
        catch
        {
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
            catch
            {
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
        catch
        {
            // 忽略错误
        }

        return usedMounts;
    }

    #endregion

    #region 进程管理

    public IEnumerable<ProcessInfo> FindProcesses(string processName)
    {
        var processes = new List<ProcessInfo>();

        try
        {
            // 使用 ps 命令获取进程信息
            var output = ExecuteCommand($"ps -eo pid,command | grep -i '{processName}' | grep -v grep");
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // 解析 "PID COMMAND" 格式
                var spaceIndex = trimmed.IndexOf(' ');
                if (spaceIndex > 0 && int.TryParse(trimmed.Substring(0, spaceIndex), out var pid))
                {
                    var commandLine = trimmed.Substring(spaceIndex + 1).Trim();

                    processes.Add(new ProcessInfo
                    {
                        ProcessId = pid,
                        ProcessName = processName,
                        CommandLine = commandLine,
                        StartTime = DateTime.MinValue
                    });
                }
            }
        }
        catch
        {
            // 使用 Process.GetProcessesByName 作为备选
            try
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    processes.Add(new ProcessInfo
                    {
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName,
                        CommandLine = "",
                        StartTime = SafeGetStartTime(process)
                    });
                }
            }
            catch
            {
                // 忽略错误
            }
        }

        return processes;
    }

    public bool TerminateProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            process.Kill();
            process.WaitForExit(5000);
            return true;
        }
        catch
        {
            return false;
        }
    }

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
        catch
        {
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
        catch
        {
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
        catch
        {
            // 忽略错误
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
        catch
        {
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
        catch
        {
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
        catch
        {
            return DateTime.MinValue;
        }
    }

    #endregion
}