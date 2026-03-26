using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using RcloneHelper.Helpers;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Services.Implementations;

/// <summary>
/// Windows 平台系统服务实现
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsSystemService : ISystemService
{
    private const string AutoStartRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "RcloneHelper";

    #region ISystemService 实现

    public PlatformType Platform => PlatformType.Windows;

    public string AppExecutablePath => PathUtil.AppExecutablePath;

    #region 开机自启

    public bool IsAutoStartEnabled
    {
        get
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, true);
            if (key == null) return false;

            if (enabled)
            {
                key.SetValue(AppName, $"\"{AppExecutablePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
            return true;
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
        catch
        {
            // 忽略权限错误
        }

        return usedDrives;
    }

    #endregion

    #region 进程管理

    public IEnumerable<ProcessInfo> FindProcesses(string processName)
    {
        var processes = new List<ProcessInfo>();

        try
        {
            // 使用 WMI 获取进程命令行（Windows 特有）
            var searcher = new ManagementObjectSearcher(
                $"SELECT ProcessId, Name, CommandLine FROM Win32_Process WHERE Name = '{processName}.exe'");

            foreach (var obj in searcher.Get())
            {
                try
                {
                    var processId = Convert.ToInt32(obj["ProcessId"]);
                    var commandLine = obj["CommandLine"]?.ToString() ?? "";
                    
                    DateTime startTime = DateTime.MinValue;
                    try
                    {
                        var process = Process.GetProcessById(processId);
                        startTime = process.StartTime;
                    }
                    catch { }

                    processes.Add(new ProcessInfo
                    {
                        ProcessId = processId,
                        ProcessName = processName,
                        CommandLine = commandLine,
                        StartTime = startTime
                    });
                }
                catch
                {
                    // 忽略单个进程的错误
                }
            }
        }
        catch
        {
            // WMI 查询失败，尝试使用 Process.GetProcesses 作为备选方案
            try
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    processes.Add(new ProcessInfo
                    {
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName,
                        CommandLine = "", // 无法获取命令行
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

    #endregion

    #endregion

    #region 私有方法

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