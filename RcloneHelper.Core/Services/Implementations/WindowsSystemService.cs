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
            _logger.Info($"[AutoStart] 设置开机启动: {enabled}, 程序路径: {exePath}");

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
            _logger.Debug($"[AutoStart] 执行命令: schtasks {arguments}");

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
                _logger.Error($"[AutoStart] 开机启动任务创建失败, 退出码: {process.ExitCode}");
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
        catch (Exception ex)
        {
            _logger.Debug($"静默删除开机启动任务失败: {ex.Message}");
        }
    }

    #endregion

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

    public bool IsMountPointOccupied(string mountPoint)
    {
        if (string.IsNullOrEmpty(mountPoint))
            return true;

        // 检查盘符是否存在
        if (mountPoint.Length >= 2 && mountPoint[1] == ':')
        {
            return Directory.Exists(mountPoint);
        }

        // 检查目录是否存在
        return Directory.Exists(mountPoint);
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
        catch (Exception ex)
        {
            _logger.Debug($"获取驱动器列表失败: {ex.Message}");
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
            _logger.Debug($"[GetActiveMounts] net use 输出 {lines.Length} 行");

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
                    _logger.Debug($"[GetActiveMounts] 发现 rclone 挂载: {name} -> {localDrive}");
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
        catch (Exception ex)
        {
            _logger.Debug($"检测 WinFsp 失败: {ex.Message}");
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

            // 1. 优先查找 %APPDATA%\RcloneHelper 目录
            var appDataRclone = Path.Combine(PathUtil.AppDataDir, "rclone.exe");
            if (File.Exists(appDataRclone))
            {
                rclonePath = appDataRclone;
            }

            // 2. 查找程序目录
            if (rclonePath == null)
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var localRclone = Path.Combine(appDir, "rclone.exe");
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

    private static string? FindRcloneInPath()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where",
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
            // 查找 rclone 路径失败，返回 null 让调用者使用默认路径
            System.Diagnostics.Debug.WriteLine($"FindRcloneInPath 失败: {ex.Message}");
        }

        return null;
    }

    private static bool TestRcloneExecution(string rclonePath)
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
            System.Diagnostics.Debug.WriteLine($"TestRcloneExecution 失败: {rclonePath}, 错误: {ex.Message}");
            return false;
        }
    }

    #endregion

    #endregion

    #region 私有方法

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