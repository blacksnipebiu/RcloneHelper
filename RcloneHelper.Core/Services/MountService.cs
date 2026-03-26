using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RcloneHelper.Helpers;
using RcloneHelper.Models;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Services;

/// <summary>
/// 挂载管理服务，负责管理所有挂载配置和相关操作
/// </summary>
public class MountService
{
    private readonly ISystemService _systemService;
    private readonly string _rclonePath;
    private readonly Dictionary<string, Process> _mountProcesses = new();

    public ObservableCollection<MountInfo> Mounts { get; } = new();

    public MountService(ISystemService systemService)
    {
        _systemService = systemService;
        _rclonePath = PathUtil.FindRclonePath();
        LoadMounts();
    }

    #region 文件操作

    /// <summary>
    /// 从本地文件加载挂载配置
    /// </summary>
    public void LoadMounts()
    {
        Mounts.Clear();

        var configPath = PathUtil.MountsConfigPath;
        if (!File.Exists(configPath))
            return;

        try
        {
            var json = File.ReadAllText(configPath);
            var configs = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListMountConfig);
            if (configs == null)
                return;

            foreach (var config in configs)
            {
                var mount = MountInfo.FromConfig(config);
                Mounts.Add(mount);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"加载配置失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 保存挂载配置到本地文件
    /// </summary>
    public void SaveMounts()
    {
        try
        {
            var configs = Mounts.Select(m => m.ToConfig()).ToList();
            var json = JsonSerializer.Serialize(configs, AppJsonContext.Default.ListMountConfig);
            File.WriteAllText(PathUtil.MountsConfigPath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"保存配置失败: {ex.Message}", ex);
        }
    }

    #endregion

    #region 挂载管理

    /// <summary>
    /// 添加新的挂载配置
    /// </summary>
    /// <param name="mount">挂载信息</param>
    public void AddMount(MountInfo mount)
    {
        if (mount == null)
            throw new ArgumentNullException(nameof(mount));

        if (string.IsNullOrWhiteSpace(mount.Name))
            throw new ArgumentException("挂载名称不能为空");

        if (string.IsNullOrWhiteSpace(mount.Url))
            throw new ArgumentException("URL 不能为空");

        // 检查是否已存在同名挂载
        if (Mounts.Any(m => m.Name == mount.Name))
            throw new InvalidOperationException($"已存在名为 {mount.Name} 的挂载");

        mount.IsMounted = false;
        mount.Status = "未挂载";
        Mounts.Add(mount);
        SaveMounts();
    }

    /// <summary>
    /// 更新挂载配置
    /// </summary>
    /// <param name="mount">已更新的挂载对象（直接修改了属性）</param>
    /// <param name="originalName">原始名称（用于更新 rclone 配置，如果名称被修改）</param>
    public async Task UpdateMountAsync(MountInfo mount, string? originalName = null)
    {
        if (mount == null)
            throw new ArgumentNullException(nameof(mount));

        // 验证挂载对象存在于集合中
        if (!Mounts.Contains(mount))
            throw new InvalidOperationException($"未找到挂载对象");

        // 如果名称被修改，需要删除旧的 rclone 配置
        var oldName = originalName ?? mount.Name;
        if (!string.IsNullOrEmpty(oldName) && oldName != mount.Name)
        {
            DeleteRcloneConfig(oldName);
        }

        // 更新 rclone 配置（create 命令会创建或更新）
        await CreateRcloneConfigAsync(mount);

        SaveMounts();
    }

    /// <summary>
    /// 删除 rclone 配置
    /// </summary>
    private void DeleteRcloneConfig(string name)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _rclonePath,
                    Arguments = $"config delete {name}",
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
            // 忽略删除失败的错误
        }
    }

    /// <summary>
    /// 添加或更新挂载配置
    /// </summary>
    /// <param name="mount">挂载信息</param>
    public async Task AddOrUpdateMountAsync(MountInfo mount)
    {
        var existing = Mounts.FirstOrDefault(m => m.Name == mount.Name);
        if (existing != null)
        {
            existing.RemotePath = mount.RemotePath;
            existing.LocalDrive = mount.LocalDrive;
            existing.Type = mount.Type;
            existing.Url = mount.Url;
            existing.User = mount.User;
            existing.Password = mount.Password;
            existing.Vendor = mount.Vendor;
            existing.AutoMountOnStart = mount.AutoMountOnStart;
            await UpdateMountAsync(existing);
        }
        else
        {
            AddMount(mount);
        }
    }

    /// <summary>
    /// 删除挂载配置
    /// </summary>
    /// <param name="name">挂载名称</param>
    public void DeleteMount(string name)
    {
        var mount = Mounts.FirstOrDefault(m => m.Name == name);
        if (mount == null)
            return;

        // 如果已挂载，先卸载
        if (mount.IsMounted)
        {
            Unmount(name);
        }

        Mounts.Remove(mount);
        SaveMounts();
    }

    /// <summary>
    /// 根据名称获取挂载信息
    /// </summary>
    /// <param name="name">挂载名称</param>
    /// <returns>挂载信息，如果不存在则返回 null</returns>
    public MountInfo? GetMount(string name)
    {
        return Mounts.FirstOrDefault(m => m.Name == name);
    }

    /// <summary>
    /// 获取可用的挂载点（跨平台）
    /// </summary>
    /// <returns>Windows: 盘符如 "Z:"; Linux/macOS: 目录路径如 "/mnt/rclone"</returns>
    public string GetAvailableMountPoint()
    {
        // 先检查已配置的挂载点
        var usedMountPoints = Mounts.Select(m => m.LocalDrive.ToUpper()).ToHashSet();
        var systemMountPoint = _systemService.GetAvailableMountPoint();
        
        // 如果系统建议的挂载点未被使用，直接返回
        if (!usedMountPoints.Contains(systemMountPoint.ToUpper()))
            return systemMountPoint;
        
        // 否则基于平台生成新的挂载点
        return GenerateAvailableMountPoint(usedMountPoints);
    }

    private string GenerateAvailableMountPoint(HashSet<string> usedMountPoints)
    {
        if (_systemService.Platform == PlatformType.Windows)
        {
            // Windows: 生成盘符
            for (char c = 'Z'; c >= 'D'; c--)
            {
                var drive = $"{c}:";
                if (!usedMountPoints.Contains(drive))
                    return drive;
            }
            return "Z:";
        }
        else
        {
            // Linux/macOS: 生成目录路径
            var baseDir = _systemService.Platform == PlatformType.macOS ? "/Volumes" : "/mnt";
            for (int i = 1; i <= 26; i++)
            {
                var mountPoint = $"{baseDir}/rclone{i}";
                if (!usedMountPoints.Contains(mountPoint.ToUpper()))
                    return mountPoint;
            }
            return $"{baseDir}/rclone";
        }
    }

    #endregion

    #region 挂载操作

    /// <summary>
    /// 执行挂载操作
    /// </summary>
    /// <param name="name">挂载名称</param>
    /// <returns>挂载是否成功</returns>
    public async Task<bool> MountAsync(string name)
    {
        var mount = GetMount(name);
        if (mount == null)
            throw new InvalidOperationException($"未找到名为 {name} 的挂载");

        if (mount.IsMounted)
            return true;

        try
        {
            mount.Status = "正在挂载...";

            // 创建 rclone 配置
            await CreateRcloneConfigAsync(mount);

            // 执行挂载
            var remotePath = string.IsNullOrWhiteSpace(mount.RemotePath)
                ? mount.Name
                : $"{mount.Name}:{mount.RemotePath}";

            // 构建挂载参数
            var mountArgs = $"mount {remotePath} {mount.LocalDrive} --vfs-cache-mode writes";
            
            if (mount.UseNetworkMode)
            {
                // 网络驱动器模式：显示为 "名称 (\\rclone)"
                mountArgs += $" --network-mode --volname \\\\rclone\\{mount.Name}";
            }
            else
            {
                // 普通磁盘模式：显示为 "名称"
                mountArgs += $" --volname \"{mount.Name}\"";
            }

            var mountProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _rclonePath,
                    Arguments = mountArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            mountProcess.Exited += (s, e) =>
            {
                if (mount.IsMounted)
                {
                    mount.IsMounted = false;
                    mount.Status = "未挂载";
                }
                _mountProcesses.Remove(mount.Name);
                mount.MountProcess = null;
            };

            mountProcess.Start();
            mount.MountProcess = mountProcess;
            _mountProcesses[mount.Name] = mountProcess;

            // 等待挂载完成
            await Task.Delay(2000);

            if (!mountProcess.HasExited)
            {
                mount.IsMounted = true;
                mount.Status = $"已挂载到 {mount.LocalDrive}";
                return true;
            }
            else
            {
                var error = await mountProcess.StandardError.ReadToEndAsync();
                mount.Status = "挂载失败";
                throw new InvalidOperationException($"挂载失败: {error}");
            }
        }
        catch (Exception ex)
        {
            mount.Status = "挂载失败";
            throw new InvalidOperationException($"挂载错误: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 执行卸载操作
    /// </summary>
    /// <param name="name">挂载名称</param>
    public void Unmount(string name)
    {
        var mount = GetMount(name);
        if (mount == null)
            return;

        if (!mount.IsMounted && mount.MountProcess == null)
            return;

        try
        {
            // 终止挂载进程
            if (mount.MountProcess != null && !mount.MountProcess.HasExited)
            {
                mount.MountProcess.Kill();
                mount.MountProcess.WaitForExit(5000);
            }

            // 执行 rclone umount
            var unmountProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _rclonePath,
                    Arguments = $"umount {mount.LocalDrive}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            unmountProcess.Start();
            unmountProcess.WaitForExit(5000);

            mount.IsMounted = false;
            mount.Status = "未挂载";
            mount.MountProcess = null;
            _mountProcesses.Remove(name);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"卸载错误: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 异步卸载操作
    /// </summary>
    /// <param name="name">挂载名称</param>
    public Task UnmountAsync(string name)
    {
        return Task.Run(() => Unmount(name));
    }

    /// <summary>
    /// 挂载所有未挂载的配置
    /// </summary>
    public async Task MountAllAsync()
    {
        var toMount = Mounts.Where(m => !m.IsMounted).ToList();
        foreach (var mount in toMount)
        {
            try
            {
                await MountAsync(mount.Name);
            }
            catch
            {
                // 继续挂载其他项
            }
        }
    }

    /// <summary>
    /// 卸载所有已挂载的配置
    /// </summary>
    public void UnmountAll()
    {
        var toUnmount = Mounts.Where(m => m.IsMounted).ToList();
        foreach (var mount in toUnmount)
        {
            try
            {
                Unmount(mount.Name);
            }
            catch
            {
                // 继续卸载其他项
            }
        }
    }

    /// <summary>
    /// 异步卸载所有已挂载的配置
    /// </summary>
    public Task UnmountAllAsync()
    {
        return Task.Run(UnmountAll);
    }

    #endregion

    #region 状态检查

    /// <summary>
    /// 检查挂载状态（通过查询 rclone 进程）
    /// </summary>
    /// <returns>当前活跃的挂载列表</returns>
    public List<ActiveMountInfo> GetActiveMounts()
    {
        var mounts = new List<ActiveMountInfo>();

        try
        {
            // 使用 ISystemService 查找 rclone 进程
            var processes = _systemService.FindProcesses("rclone");

            foreach (var process in processes)
            {
                // 解析命令行获取挂载信息
                var mountMatch = Regex.Match(
                    process.CommandLine, 
                    @"mount\s+(\S+)\s+(\S+)", 
                    RegexOptions.IgnoreCase);

                if (mountMatch.Success)
                {
                    mounts.Add(new ActiveMountInfo
                    {
                        Name = mountMatch.Groups[1].Value,
                        LocalDrive = mountMatch.Groups[2].Value,
                        ProcessId = process.ProcessId
                    });
                }
            }
        }
        catch
        {
            // 忽略错误
        }

        return mounts;
    }

    /// <summary>
    /// 刷新所有挂载的状态
    /// </summary>
    public void RefreshMountStatus()
    {
        var activeMounts = GetActiveMounts();

        foreach (var mount in Mounts)
        {
            var active = activeMounts.FirstOrDefault(a =>
                a.Name.StartsWith(mount.Name) || a.Name == mount.Name);

            if (active != null)
            {
                mount.IsMounted = true;
                // 使用实际挂载的盘符
                mount.LocalDrive = active.LocalDrive;
                mount.Status = $"已挂载到 {mount.LocalDrive}";

                // 尝试关联进程
                try
                {
                    if (mount.MountProcess == null || mount.MountProcess.HasExited)
                    {
                        mount.MountProcess = Process.GetProcessById(active.ProcessId);
                    }
                }
                catch
                {
                    // 进程可能已终止
                }
            }
            else
            {
                mount.IsMounted = false;
                mount.Status = "未挂载";
                mount.MountProcess = null;
            }
        }
    }

    /// <summary>
    /// 获取当前平台类型
    /// </summary>
    public PlatformType Platform => _systemService.Platform;

    #endregion

    #region 私有方法

    private async Task CreateRcloneConfigAsync(MountInfo mount)
    {
        var configArgs = $"config create {mount.Name} {mount.Type} ";
        configArgs += $"url \"{mount.Url}\" ";

        if (!string.IsNullOrWhiteSpace(mount.User))
            configArgs += $"user \"{mount.User}\" ";

        if (!string.IsNullOrWhiteSpace(mount.Password))
            configArgs += $"pass \"{mount.Password}\" ";

        if (mount.Type == "webdav" && !string.IsNullOrWhiteSpace(mount.Vendor))
            configArgs += $"vendor \"{mount.Vendor}\" ";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _rclonePath,
                Arguments = configArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();
    }

    #endregion
}

/// <summary>
/// 活跃挂载信息
/// </summary>
public class ActiveMountInfo
{
    public string Name { get; set; } = "";
    public string LocalDrive { get; set; } = "";
    public int ProcessId { get; set; }
}