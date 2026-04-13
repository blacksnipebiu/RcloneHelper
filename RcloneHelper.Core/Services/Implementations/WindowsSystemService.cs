using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using RcloneHelper.Helpers;
using RcloneHelper.Models;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Services.Implementations;

/// <summary>
/// Windows 平台系统服务实现
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsSystemService : ISystemService
{
    private const string TaskName = "RcloneHelper_AutoStart";
    private readonly ILoggerService _logger;

    public WindowsSystemService(ILoggerService logger)
    {
        _logger = logger;
    }

    #region ISystemService 实现

    public OSPlatform Platform => OSPlatform.Windows;

    public string AppExecutablePath => Environment.ProcessPath ?? "";

    #region 开机自启（使用 Windows 任务计划程序）

    public bool IsAutoStartEnabled
    {
        get
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks",
                        Arguments = $"/Query /TN \"{TaskName}\"",
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
                _logger.Warning($"检查开机自启状态失败: {ex.Message}");
                return false;
            }
        }
    }

    public bool SetAutoStart(bool enabled)
    {
        try
        {
            var exePath = AppExecutablePath;

            if (enabled)
            {
                return CreateAutoStartTask(exePath);
            }
            else
            {
                return DeleteAutoStartTask();
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[AutoStart] 设置开机启动失败: {ex.Message}");
            return false;
        }
    }

    private bool CreateAutoStartTask(string exePath)
    {
        try
        {
            // 先删除已存在的任务
            DeleteAutoStartTaskSilent();

            // 添加 --autostart 参数，用于区分开机自启启动和用户手动启动
            var arguments = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\" --autostart\" /SC ONLOGON /F";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = arguments,
                    UseShellExecute = true,  // 必须为 true 才能使用 runas
                    Verb = "runas",          // 触发 UAC 提权
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };
            process.Start();
            process.WaitForExit(30000);  // UAC 对话框可能需要用户响应

            if (process.ExitCode == 0)
            {
                _logger.Info("[AutoStart] 开机启动任务创建成功");
                return true;
            }
            else
            {
                _logger.Warning("[AutoStart] 开机启动任务创建失败");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[AutoStart] 创建开机启动任务异常: {ex.Message}");
            return false;
        }
    }

    private bool DeleteAutoStartTask()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/Delete /TN \"{TaskName}\" /F",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };
            process.Start();
            process.WaitForExit(30000);

            _logger.Info("[AutoStart] 开机启动任务已删除");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"[AutoStart] 删除开机启动任务异常: {ex.Message}");
            return false;
        }
    }

    private void DeleteAutoStartTaskSilent()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/Delete /TN \"{TaskName}\" /F",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(5000);
        }
        catch
        {
            // 静默失败，不影响主流程
        }
    }

    #endregion 开机自启（使用 Windows 任务计划程序）

    #region 挂载点管理

    public string GetAvailableMountPoint()
    {
        var usedDrives = GetUsedMountPoints();

        // 从 Z: 到 D: 查找可用盘符
        for (char c = 'Z'; c >= 'D'; c--)
        {
            var drive = $"{c}:";
            if (!usedDrives.Contains(drive))
                return drive;
        }

        return "Z:";
    }

    public IReadOnlySet<string> GetUsedMountPoints()
    {
        var usedDrives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 获取系统中已存在的驱动器
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                usedDrives.Add(drive.Name.TrimEnd('\\'));
            }
        }
        catch
        {
            // 获取驱动器列表失败不影响主流程
        }

        return usedDrives;
    }

    public IReadOnlyDictionary<string, string> GetActiveMountNames()
    {
        var mounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // 使用 net use 命令获取网络驱动器映射
            // rclone 挂载的驱动器格式: \\rclone\<挂载名> -> 盘符
            var systemEncoding = CodePagesEncodingProvider.Instance.GetEncoding(0);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = "use",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = systemEncoding,
                    StandardErrorEncoding = systemEncoding
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            // 解析输出
            // 格式示例 (Windows 中文):
            // 状态       本地        远程                      网络
            // -------------------------------------------------------------------------------
            //            Y:        \\rclone\飞牛             WinFsp.Np
            //            Z:        \\rclone\alist            WinFsp.Np
            // OK           Y:        \\rclone\飞牛             WinFsp.Np (已连接状态)
            var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // 跳过标题行和分隔线
                if (line.Contains("本地") || line.Contains("Local") || line.TrimStart().StartsWith("-") || string.IsNullOrWhiteSpace(line))
                    continue;

                // 解析: "           Z:        \\rclone\alist            WinFsp.Np"
                // 或: "OK           Z:        \\rclone\alist            WinFsp.Np"
                var trimmedLine = line.TrimStart();

                // 查找盘符 (格式: X:)
                var colonIndex = trimmedLine.IndexOf(':');
                if (colonIndex <= 0) continue;

                // 盘符可能是 "X:" 或在状态之后 "OK       X:"
                // 找到盘符位置
                int driveStart;
                for (driveStart = colonIndex - 1; driveStart >= 0; driveStart--)
                {
                    if (!char.IsLetter(trimmedLine[driveStart]))
                    {
                        driveStart++;
                        break;
                    }
                }
                if (driveStart < 0) driveStart = 0;

                var localDrive = trimmedLine.Substring(driveStart, colonIndex - driveStart + 1);

                // 盘符后的内容
                var afterDrive = trimmedLine.Substring(colonIndex + 1).TrimStart();
                if (string.IsNullOrEmpty(afterDrive)) continue;

                // 下一个字段是远程路径
                var parts = afterDrive.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                var remote = parts[0];

                // 检查是否是 rclone 挂载: \\rclone\<名称>
                if (remote.StartsWith(@"\\rclone\", StringComparison.OrdinalIgnoreCase))
                {
                    var name = remote.Substring(@"\\rclone\".Length);
                    mounts[name] = localDrive;
                }
            }
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
            // .NET Core 3.0+ 可通过 Process.GetProcessesByName 获取命令行参数
            foreach (var proc in Process.GetProcessesByName("rclone"))
            {
                try
                {
                    if (proc.Id == 0)
                    {
                        proc.Dispose();
                        continue;
                    }

                    // Arguments 为空（权限不足等），尝试通过 PowerShell 获取
                    var psResult = GetProcessCommandLineByPowerShell(proc.Id);
                    if (!string.IsNullOrEmpty(psResult))
                    {
                        result[proc.Id] = psResult;
                    }
                    proc.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Warning($"获取进程 {proc.Id} 命令行失败: {ex.Message}");
                    proc.Dispose();
                }
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
        }
        catch (Exception ex)
        {
            _logger.Warning($"扫描 rclone 挂载进程失败: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 解析 rclone mount 命令行，提取挂载信息
    /// 命令行格式: "path/to/rclone.exe" mount &lt;remote&gt; &lt;drive&gt;: [options]
    /// 示例: "D:\program\rclone-v1.73.3-windows-amd64\rclone.exe" mount 飞牛: Y: --vfs-cache-mode writes --links --network-mode --volname \\rclone\飞牛
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
                // 普通名称，查找冒号结束
                var colonIndex = argsPart.IndexOf(':');
                if (colonIndex < 0)
                    return null;
                mountName = argsPart.Substring(0, colonIndex);
                remaining = argsPart.Substring(colonIndex + 1).TrimStart();
            }

            // 解析第二个参数（drive letter，如 "Y:" 或 "Z:"）
            // drive 是以冒号结尾的单个字母
            var driveMatch = System.Text.RegularExpressions.Regex.Match(remaining, @"^([A-Za-z]):\s*");
            if (!driveMatch.Success)
                return null;

            var driveLetter = driveMatch.Groups[1].Value.ToUpper() + ":";

            return new RcloneMountInfo
            {
                Pid = pid,
                MountName = mountName,
                DriveLetter = driveLetter,
                CommandLine = commandLine
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 通过 PowerShell 获取指定 PID 进程的完整命令行
    /// </summary>
    private string GetProcessCommandLineByPowerShell(int pid)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"(Get-CimInstance Win32_Process -Filter 'ProcessId={pid}').CommandLine\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return output.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    #endregion 挂载点管理

    #region 系统信息

    public SystemDependency? GetFuseDependency()
    {
        var dependency = new SystemDependency
        {
            Name = "WinFsp",
            Description = "Windows FUSE 驱动，rclone mount 必需",
            InstallUrl = "https://github.com/winfsp/winfsp/releases",
            Icon = "🪟"
        };

        try
        {
            bool isInstalled = false;

            // 1. 检查注册表 InstallDir（最可靠的方法）
            // WinFsp 安装后会在这里设置 InstallDir
            string[] registryPaths = {
                @"SOFTWARE\WinFsp",
                @"SOFTWARE\WOW6432Node\WinFsp"
            };

            foreach (var regPath in registryPaths)
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, false);
                if (key != null)
                {
                    var installDir = key.GetValue("InstallDir") as string;
                    if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                    {
                        isInstalled = true;
                        break;
                    }
                }
            }

            // 2. 如果注册表未找到，检查服务键和文件
            if (!isInstalled)
            {
                using var serviceKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\WinFsp", false);
                if (serviceKey != null)
                {
                    // 服务存在，检查 bin 目录
                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    var possiblePaths = new[] {
                        Path.Combine(programFiles, "WinFsp", "bin", "winfsp-x64.dll"),
                        Path.Combine(programFiles, "WinFsp", "bin", "winfsp.dll"),
                        @"C:\Program Files (x86)\WinFsp\bin\winfsp-x64.dll"
                    };

                    foreach (var path in possiblePaths)
                    {
                        if (File.Exists(path))
                        {
                            isInstalled = true;
                            break;
                        }
                    }
                }
            }

            dependency.Status = isInstalled ? DependencyStatus.Installed : DependencyStatus.NotInstalled;
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

    #endregion 系统信息

    #endregion ISystemService 实现
}