using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            if (!GetUsedMountPoints().Contains(mountPoint))
                return mountPoint;
        }

        // 回退到用户目录
        var userMount = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "mnt", "rclone");
        Directory.CreateDirectory(Path.GetDirectoryName(userMount)!);
        return userMount;
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

    public Dictionary<int, string> GetRcloneProcessList()
    {
        var result = new Dictionary<int, string>();

        try
        {
            var output = ExecuteCommand("ps -eo pid,args");
            if (string.IsNullOrEmpty(output))
                return result;

            // 解析 ps 输出: "  1234 /usr/bin/rclone mount name: /path ..."
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.TrimStart();
                var spaceIdx = trimmed.IndexOf(' ');
                if (spaceIdx <= 0)
                    continue;

                if (!int.TryParse(trimmed[..spaceIdx], out var pid))
                    continue;

                var cmdLine = trimmed[(spaceIdx + 1)..].TrimStart();
                if (!string.IsNullOrEmpty(cmdLine))
                    result[pid] = cmdLine;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"获取 rclone 进程列表失败: {ex.Message}");
        }

        return result;
    }

    public IReadOnlyDictionary<string, RcloneMountInfo> ScanRcloneMounts()
    {
        var result = new Dictionary<string, RcloneMountInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var processList = GetRcloneProcessList();

            foreach (var (pid, commandLine) in processList)
            {
                var mountInfo = ParseRcloneMountCommandLine(pid, commandLine);
                if (mountInfo != null && !string.IsNullOrEmpty(mountInfo.MountName))
                {
                    result[mountInfo.MountName] = mountInfo;
                }
            }

            _logger.Debug($"[ScanRcloneMounts] 扫描到 {result.Count} 个 rclone 挂载进程");
        }
        catch (Exception ex)
        {
            _logger.Warning($"扫描 rclone 挂载进程失败: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 解析 rclone mount 命令行，提取挂载信息
    /// 命令行格式: /usr/bin/rclone mount &lt;remote&gt; &lt;mountpoint&gt; [options]
    /// 示例: /usr/bin/rclone mount remote: /Volumes/Rclone1 --vfs-cache-mode writes
    /// </summary>
    private RcloneMountInfo? ParseRcloneMountCommandLine(int pid, string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return null;

        try
        {
            // 查找 " mount " 关键字
            var mountIndex = commandLine.IndexOf(" mount ", StringComparison.OrdinalIgnoreCase);
            if (mountIndex < 0)
                return null;

            // 获取 mount 后的参数部分
            var argsPart = commandLine.Substring(mountIndex + " mount ".Length).TrimStart();

            // 解析第一个参数（remote，可能是带引号的名称或普通名称）
            string mountName;
            string remaining;

            if (argsPart.StartsWith('"'))
            {
                // 带引号的名称
                var endQuote = argsPart.IndexOf('"', 1);
                if (endQuote < 0)
                    return null;
                mountName = argsPart.Substring(1, endQuote - 1);
                remaining = argsPart.Substring(endQuote + 1).TrimStart();
            }
            else if (argsPart.StartsWith('\''))
            {
                // 单引号包裹的名称
                var endQuote = argsPart.IndexOf('\'', 1);
                if (endQuote < 0)
                    return null;
                mountName = argsPart.Substring(1, endQuote - 1);
                remaining = argsPart.Substring(endQuote + 1).TrimStart();
            }
            else
            {
                // 普通名称，查找空格或冒号结束
                var spaceIdx = argsPart.IndexOf(' ');
                var colonIdx = argsPart.IndexOf(':');

                int endIdx;
                if (spaceIdx < 0 && colonIdx < 0)
                    return null;
                else if (spaceIdx < 0)
                    endIdx = colonIdx;
                else if (colonIdx < 0)
                    endIdx = spaceIdx;
                else
                    endIdx = Math.Min(spaceIdx, colonIdx);

                mountName = argsPart.Substring(0, endIdx);
                remaining = argsPart.Substring(endIdx + 1).TrimStart();
            }

            // 解析第二个参数（mountpoint，可能是带引号的路径或普通路径）
            string mountPoint;
            if (remaining.StartsWith('"'))
            {
                // 带引号的路径
                var endQuote = remaining.IndexOf('"', 1);
                if (endQuote < 0)
                    return null;
                mountPoint = remaining.Substring(1, endQuote - 1);
            }
            else if (remaining.StartsWith('\''))
            {
                // 单引号包裹的路径
                var endQuote = remaining.IndexOf('\'', 1);
                if (endQuote < 0)
                    return null;
                mountPoint = remaining.Substring(1, endQuote - 1);
            }
            else
            {
                // 普通路径，找到下一个空格之前的内容
                var spaceIdx = remaining.IndexOf(' ');
                mountPoint = spaceIdx > 0 ? remaining.Substring(0, spaceIdx) : remaining;
            }

            if (string.IsNullOrEmpty(mountPoint))
                return null;

            return new RcloneMountInfo
            {
                Pid = pid,
                MountName = mountName,
                DriveLetter = mountPoint,  // macOS 上是路径而非盘符
                CommandLine = commandLine
            };
        }
        catch (Exception ex)
        {
            _logger.Debug($"解析 rclone 命令行失败 (PID: {pid}): {ex.Message}");
            return null;
        }
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
            var rclonePath = RcloneLocator.GetRclonePath();
            dependency.Status = RcloneLocator.TestExecution(rclonePath)
                ? DependencyStatus.Installed
                : DependencyStatus.NotInstalled;
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

    #endregion
}