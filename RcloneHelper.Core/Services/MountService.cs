using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
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
    private readonly ILoggerService _logger;
    private readonly IConfigService _configService;
    private readonly INotificationService _notificationService;
    private readonly ConcurrentDictionary<string, Process> _mountProcesses = new();
    private readonly SynchronizationContext? _syncContext;
    private readonly SemaphoreSlim _configLock = new(1, 1);

    /// <summary>
    /// 挂载列表变化事件（新增或删除时触发）
    /// </summary>
    public event Action? MountsChanged;

    public ObservableCollection<MountInfo> Mounts { get; } = new();

    public MountService(
        ISystemService systemService,
        ILoggerService logger,
        IConfigService configService,
        INotificationService notificationService)
    {
        _systemService = systemService;
        _logger = logger;
        _configService = configService;
        _notificationService = notificationService;
        _syncContext = SynchronizationContext.Current; // 捕获 UI 线程的同步上下文
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
            var configs = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.ListMountConfig);
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
            var json = JsonSerializer.Serialize(configs, AppJsonSerializerContext.Default.ListMountConfig);
            PathUtil.AtomicWriteAllText(PathUtil.MountsConfigPath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"保存配置失败: {ex.Message}", ex);
        }
    }

    #endregion 文件操作

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
        MountsChanged?.Invoke();
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
            await DeleteRcloneConfigAsync(oldName);
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
            _configLock.Wait();
            try
            {
                _logger.Debug($"删除 rclone 配置: {name}");
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = RcloneLocator.GetRclonePath(),
                        Arguments = $"config delete {EscapeRcloneName(name)}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit(5000);
            }
            finally
            {
                _configLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"删除 rclone 配置失败: {name}, 错误: {ex.Message}");
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
        MountsChanged?.Invoke();
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
        if (_systemService.Platform == OSPlatform.Windows)
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
            var baseDir = _systemService.Platform == OSPlatform.OSX ? "/Volumes" : "/mnt";
            for (int i = 1; i <= 26; i++)
            {
                var mountPoint = $"{baseDir}/rclone{i}";
                if (!usedMountPoints.Contains(mountPoint.ToUpper()))
                    return mountPoint;
            }
            return $"{baseDir}/rclone";
        }
    }

    #endregion 挂载管理

    #region 挂载操作

    /// <summary>
    /// 执行挂载操作
    /// </summary>
    /// <param name="name">挂载名称</param>
    /// <param name="oldName">旧名称（如果名称变更需要删除旧的 rclone 配置）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>挂载是否成功</returns>
    public async Task<bool> MountAsync(string name, string? oldName = null, CancellationToken cancellationToken = default)
    {
        var mount = GetMount(name);
        if (mount == null)
        {
            _logger.Error($"挂载失败: 未找到名为 {name} 的挂载");
            throw new InvalidOperationException($"未找到名为 {name} 的挂载");
        }

        if (mount.IsMounted)
            return true;

        // 设置挂载中状态
        mount.IsMounting = true;
        mount.Status = "正在挂载...";

        try
        {
            _logger.Info($"开始挂载: {name} ({mount.Type}) -> {mount.LocalDrive}");

            // 创建 rclone 配置（如果名称变更，删除旧的配置）
            await CreateRcloneConfigAsync(mount, oldName);

            // 清理可能的残留挂载状态（僵尸进程占用的 WinFsp 网络共享名等）
            await CleanupStaleMountAsync(mount);

            // 构建挂载参数
            var mountArgs = BuildMountArgs(mount);

            var mountProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = RcloneLocator.GetRclonePath(),
                    Arguments = mountArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            // 设置代理环境变量
            SetProxyEnvironment(mountProcess.StartInfo);

            mountProcess.Exited += (s, e) =>
                        {
                            // 在 UI 线程上执行状态更新和通知
                            void HandleExit()
                            {
                                _mountProcesses.TryRemove(name, out _);
                                if (mount.IsMounted)
                                {
                                    mount.IsMounted = false;
                                    mount.Status = "未挂载";
                                    _logger.Warning($"挂载意外退出: {name}");
                                    _notificationService.ShowWarning($"挂载 \"{name}\" 已意外断开");
                                }
                            }

                            // 如果有同步上下文，在 UI 线程执行；否则直接执行
                            if (_syncContext != null)
                                _syncContext.Post(_ => HandleExit(), null);
                            else
                                HandleExit();
                        };

            mountProcess.Start();
            _mountProcesses[name] = mountProcess;

            // 等待挂载完成：轮询检查挂载点是否可访问
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(30);

            while (DateTime.UtcNow - startTime < timeout)
            {
                // 检查取消请求
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Info($"挂载被取消: {name}");
                    // 终止进程
                    if (!mountProcess.HasExited)
                    {
                        mountProcess.Kill();
                        mountProcess.WaitForExit(5000);
                    }
                    _mountProcesses.TryRemove(name, out _);
                    mount.IsMounting = false;
                    mount.Status = "已取消";
                    mount.MountCancellationTokenSource = null;
                    throw new OperationCanceledException("挂载操作已取消", cancellationToken);
                }

                if (mountProcess.HasExited)
                {
                    var error = await mountProcess.StandardError.ReadToEndAsync();
                    mount.IsMounting = false;
                    mount.Status = "挂载失败";
                    _logger.Error($"挂载失败: {name}, 进程已退出, 错误: {error}");
                    throw new InvalidOperationException($"挂载失败: {error}");
                }

                if (IsMountPointAccessible(mount.LocalDrive))
                {
                    // 进一步验证挂载点是否真正可用（能读取内容）
                    // 避免盘符存在但网络共享名损坏的情况
                    if (IsMountPointFunctional(mount.LocalDrive))
                    {
                        mount.IsMounting = false;
                        mount.IsMounted = true;
                        mount.Status = $"已挂载到 {mount.LocalDrive}";
                        mount.MountCancellationTokenSource = null;
                        _logger.Info($"挂载成功: {name} -> {mount.LocalDrive}");

                        return true;
                    }
                    else
                    {
                        // 盘符存在但无法读取内容，可能是 WinFsp 网络共享名损坏
                        // 继续等待或超时
                        _logger.Debug($"挂载点 {mount.LocalDrive} 存在但无法读取内容，继续等待...");
                    }
                }

                await Task.Delay(200, cancellationToken);
            }

            mount.IsMounting = false;
            mount.Status = "挂载超时";
            mount.MountCancellationTokenSource = null;
            _logger.Error($"挂载超时: {name}, 挂载点 {mount.LocalDrive} 未能访问");
            throw new InvalidOperationException($"挂载超时: 挂载点 {mount.LocalDrive} 在 {timeout.TotalSeconds} 秒内未能访问");
        }
        catch (OperationCanceledException)
        {
            // 取消操作已经在上面处理，直接抛出
            throw;
        }
        catch (Exception ex)
        {
            mount.IsMounting = false;
            mount.Status = "挂载失败";
            mount.MountCancellationTokenSource = null;
            _logger.Error($"挂载异常: {name}, 错误: {ex.Message}");
            throw new InvalidOperationException($"挂载错误: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 为 ProcessStartInfo 设置代理环境变量
    /// </summary>
    private void SetProxyEnvironment(ProcessStartInfo startInfo)
    {
        var config = _configService.Current;
        var proxyUrl = config.GetProxyUrl();

        if (string.IsNullOrWhiteSpace(proxyUrl))
            return;

        // 设置代理环境变量
        startInfo.Environment["HTTP_PROXY"] = proxyUrl;
        startInfo.Environment["HTTPS_PROXY"] = proxyUrl;
    }

    /// <summary>
    /// 构建 rclone mount 命令参数
    /// </summary>
    private static string BuildMountArgs(MountInfo mount)
    {
        // remote path: name: 或 name:/path (rclone remote 名称需要转义特殊字符)
        // 对于包含特殊字符的名称，使用引号包裹
        var escapedName = EscapeRcloneName(mount.Name);
        var remotePath = string.IsNullOrWhiteSpace(mount.RemotePath)
            ? $"{escapedName}:"
            : $"{escapedName}:{mount.RemotePath}";

        // 基础参数
        var args = $"mount {remotePath} {mount.LocalDrive} --vfs-cache-mode writes --links";

        // 网络模式
        if (mount.UseNetworkMode)
        {
            args += $" --network-mode --volname \\\\rclone\\{mount.Name}";
        }
        else
        {
            args += $" --volname \"{mount.Name}\"";
        }

        return args;
    }

    /// <summary>
    /// 转义 rclone remote 名称中的特殊字符
    /// rclone 会自动处理中文和特殊字符，但如果名称包含空格或特殊字符，需要用引号包裹
    /// </summary>
    private static string EscapeRcloneName(string name)
    {
        // 如果名称包含空格或特殊字符，用引号包裹
        if (name.Any(c => char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == '\\'))
        {
            return $"\"{name}\"";
        }
        return name;
    }

    /// <summary>
    /// 取消正在进行的挂载操作
    /// </summary>
    /// <param name="name">挂载名称</param>
    public void CancelMount(string name)
    {
        var mount = GetMount(name);
        if (mount == null)
        {
            _logger.Warning($"取消挂载失败: 未找到名为 {name} 的挂载");
            return;
        }

        if (!mount.IsMounting)
        {
            _logger.Warning($"取消挂载失败: {name} 未在挂载中");
            return;
        }

        // 触发取消令牌
        mount.MountCancellationTokenSource?.Cancel();
        _logger.Info($"已请求取消挂载: {name}");
    }

    /// <summary>
    /// 执行卸载操作
    /// </summary>
    /// <param name="name">挂载名称</param>
    public void Unmount(string name)
    {
        var mount = GetMount(name);
        if (mount == null)
        {
            _logger.Error($"卸载失败: 未找到名为 {name} 的挂载");
            return;
        }

        if (!mount.IsMounted && !_mountProcesses.ContainsKey(name))
            return;

        if (string.IsNullOrWhiteSpace(mount.LocalDrive))
        {
            _logger.Error($"卸载失败: {name} 的挂载点为空，请刷新挂载状态后重试");
            return;
        }

        try
        {
            _logger.Info($"开始卸载: {name} ({mount.LocalDrive})");

            // 1. 终止挂载进程：优先从进程字典获取（窗口启动时已扫描填充）
            var processKilled = false;

            if (_mountProcesses.TryGetValue(name, out var trackedProcess) && !trackedProcess.HasExited)
            {
                trackedProcess.Kill();
                trackedProcess.WaitForExit(5000);
                processKilled = true;
            }

            // 2. Kill 后等待 WinFsp 释放驱动器号
            if (processKilled)
            {
                var waitStart = DateTime.UtcNow;
                while (DateTime.UtcNow - waitStart < TimeSpan.FromSeconds(3))
                {
                    if (!IsMountPointAccessible(mount.LocalDrive))
                        break;
                    Thread.Sleep(300);
                }
            }

            // 3. 执行 rclone umount（处理 Kill 后残留或无进程时的情况）
            if (IsMountPointAccessible(mount.LocalDrive))
            {
                var unmountProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = RcloneLocator.GetRclonePath(),
                        Arguments = $"umount {mount.LocalDrive}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                unmountProcess.Start();
                unmountProcess.WaitForExit(5000);

                var exitCode = unmountProcess.ExitCode;

                // 退出码: 0=成功, 2=非致命(挂载点已释放), 其他=错误
                if (exitCode != 0 && exitCode != 2)
                {
                    var error = unmountProcess.StandardError.ReadToEnd();
                    _logger.Error($"rclone umount 失败，退出码: {exitCode}, 错误: {error}");
                    throw new InvalidOperationException($"卸载失败: rclone umount 返回错误码 {exitCode}");
                }
                else if (exitCode == 2)
                {
                    var error = unmountProcess.StandardError.ReadToEnd();
                    _logger.Warning($"rclone umount 退出码 2（挂载点可能已释放）: {error}");
                }
            }

            // 4. 最终等待释放
            var maxWait = TimeSpan.FromSeconds(5);
            var finalWaitStart = DateTime.UtcNow;
            while (DateTime.UtcNow - finalWaitStart < maxWait)
            {
                if (!IsMountPointAccessible(mount.LocalDrive))
                    break;
                Thread.Sleep(300);
            }

            if (IsMountPointAccessible(mount.LocalDrive))
            {
                _logger.Error($"挂载点 {mount.LocalDrive} 在卸载后仍可访问");
                throw new InvalidOperationException($"卸载失败: 挂载点 {mount.LocalDrive} 仍然可访问");
            }

            mount.IsMounted = false;
            mount.Status = "未挂载";
            _mountProcesses.TryRemove(name, out _);
            _logger.Info($"卸载成功: {name}");
        }
        catch (Exception ex)
        {
            _logger.Error($"卸载异常: {name}, 错误: {ex.Message}");
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
    /// 刷新所有挂载的状态
    /// </summary>
    public void RefreshMountStatus()
    {
        var activeMounts = _systemService.GetActiveMountNames();

        foreach (var mount in Mounts)
        {
            // 查找匹配的挂载点 (名称完全匹配或名称以 mount.Name 开头)
            var mountPoint = activeMounts.FirstOrDefault(m =>
                m.Key.Equals(mount.Name, StringComparison.OrdinalIgnoreCase) ||
                m.Key.StartsWith(mount.Name + ":", StringComparison.OrdinalIgnoreCase)).Value;

            if (!string.IsNullOrEmpty(mountPoint))
            {
                mount.IsMounted = true;
                mount.LocalDrive = mountPoint;
                mount.Status = $"已挂载到 {mount.LocalDrive}";
            }
            else
            {
                mount.IsMounted = false;
                mount.Status = "未挂载";
            }
        }
    }

    /// <summary>
    /// 恢复进程字典：扫描系统中的 rclone 进程并与配置的挂载匹配
    /// （窗口启动后调用，直接扫描进程而不依赖保存的状态文件）
    /// </summary>
    public async Task RestoreMountProcessesAsync()
    {
        _logger.Info("开始扫描并恢复进程追踪");

        // 扫描系统中的 rclone 挂载进程（异步执行）
        var runningMounts = await Task.Run(() => _systemService.ScanRcloneMounts());
        if (runningMounts.Count == 0)
        {
            _logger.Info("未发现运行中的 rclone 挂载进程");
            return;
        }

        _logger.Info($"发现 {runningMounts.Count} 个运行中的 rclone 挂载进程");

        foreach (var (mountName, mountInfo) in runningMounts)
        {
            // 查找匹配的挂载配置
            var mountConfig = Mounts.FirstOrDefault(m =>
                m.Name.Equals(mountName, StringComparison.OrdinalIgnoreCase));

            if (mountConfig == null)
            {
                continue;
            }

            Process? proc = null;
            try
            {
                proc = Process.GetProcessById(mountInfo.Pid);
                if (proc.HasExited)
                {
                    continue;
                }

                if (_mountProcesses.TryAdd(mountConfig.Name, proc))
                {
                    // 更新挂载状态
                    mountConfig.IsMounted = true;
                    mountConfig.LocalDrive = mountInfo.DriveLetter;
                    mountConfig.Status = $"已挂载到 {mountInfo.DriveLetter}";
                    _logger.Info($"已恢复进程追踪: {mountConfig.Name} (PID: {mountInfo.Pid})");
                    proc = null; // 成功接管，不需要释放
                }
            }
            catch
            {
                // 进程不存在或无法访问，跳过
            }
            finally
            {
                proc?.Dispose();
            }
        }

        _logger.Info($"进程恢复完成，已追踪 {_mountProcesses.Count} 个进程");
    }

    #endregion 挂载操作

    #region 私有方法

    /// <summary>
    /// 检查挂载点是否可访问（用于判断挂载是否成功）
    /// </summary>
    /// <param name="mountPoint">挂载点路径（如 "Z:" 或 "/mnt/rclone"）</param>
    /// <returns>是否可访问</returns>
    private bool IsMountPointAccessible(string mountPoint)
    {
        if (string.IsNullOrWhiteSpace(mountPoint))
            return false;

        try
        {
            // Windows: 检查盘符是否存在且可访问
            // Linux/macOS: 检查目录是否存在且可访问
            var info = new DirectoryInfo(mountPoint);
            return info.Exists;
        }
        catch
        {
            // 目录不存在或无法访问
            return false;
        }
    }

    /// <summary>
    /// 检查挂载点是否真正可用（能读取目录内容）
    /// 用于区分：挂载点存在但网络共享名损坏的情况
    /// </summary>
    private bool IsMountPointFunctional(string mountPoint)
    {
        if (string.IsNullOrWhiteSpace(mountPoint))
            return false;

        try
        {
            var info = new DirectoryInfo(mountPoint);
            if (!info.Exists)
                return false;

            // 尝试获取目录内容，验证挂载点是否真正可用
            // GetFileSystemInfos() 会实际访问文件系统
            info.GetFileSystemInfos();
            return true;
        }
        catch
        {
            // 目录存在但无法读取内容，可能是网络共享名损坏
            return false;
        }
    }

    /// <summary>
    /// 清理挂载前的残留状态：僵尸进程占用的 WinFsp 网络共享名和残留挂载点
    /// </summary>
    /// <param name="mount">要挂载的配置信息</param>
    private async Task CleanupStaleMountAsync(MountInfo mount)
    {
        // 1. 检查挂载点是否已被占用（可能有其他挂载残留）
        if (!IsMountPointAccessible(mount.LocalDrive))
            return; // 挂载点未被占用，无需清理

        _logger.Info($"检测到挂载点 {mount.LocalDrive} 已被占用，尝试清理残留");

        // 2. 检查是否有对应的 rclone 进程在运行
        var activeRcloneMounts = _systemService.ScanRcloneMounts();

        // 查找是否有同名的活跃 rclone 进程
        var hasActiveProcess = activeRcloneMounts.TryGetValue(mount.Name, out var activeMountInfo);

        if (hasActiveProcess)
        {
            // 有活跃进程，尝试验证该进程是否真的正常工作
            try
            {
                var proc = Process.GetProcessById(activeMountInfo!.Pid);
                if (!proc.HasExited)
                {
                    // 进程仍然存活，检查挂载点是否可正常访问
                    if (IsMountPointAccessible(mount.LocalDrive))
                    {
                        // 进一步验证：尝试列出目录内容，确保挂载点真正可用
                        // 有些情况下挂载点存在但网络共享名损坏，导致无法读取内容
                        if (IsMountPointFunctional(mount.LocalDrive))
                        {
                            _logger.Info($"挂载点 {mount.LocalDrive} 已有活跃的 rclone 进程 (PID: {activeMountInfo.Pid})，接管该进程");

                        // 启用事件监听并注册退出事件
                        proc.EnableRaisingEvents = true;
                        proc.Exited += (s, e) =>
                        {
                            void HandleExit()
                            {
                                _mountProcesses.TryRemove(mount.Name, out _);
                                if (mount.IsMounted)
                                {
                                    mount.IsMounted = false;
                                    mount.Status = "未挂载";
                                    _logger.Warning($"挂载意外退出: {mount.Name}");
                                    _notificationService.ShowWarning($"挂载 \"{mount.Name}\" 已意外断开");
                                }
                            }

                            if (_syncContext != null)
                                _syncContext.Post(_ => HandleExit(), null);
                            else
                                HandleExit();
                        };

                        // 接管现有进程：加入追踪字典并更新状态
                        if (_mountProcesses.TryAdd(mount.Name, proc))
                        {
                            // 成功接管，更新挂载状态
                            mount.IsMounted = true;
                            mount.Status = $"已挂载到 {mount.LocalDrive}";
                            _logger.Info($"已接管现有挂载进程: {mount.Name} (PID: {activeMountInfo.Pid})");
                        }
                        else
                        {
                            // 已有同名进程在追踪中，释放当前进程句柄
                            proc.Dispose();
                        }
                        return;
                        }
                        else
                        {
                            // 挂载点存在但无法读取内容，可能是网络共享名损坏
                            _logger.Warning($"挂载点 {mount.LocalDrive} 存在但无法读取内容，可能是残留损坏，将清理后重新挂载");
                            proc.Dispose();
                        }
                        return;
                    }
                }
                proc.Dispose();
            }
            catch
            {
                // 进程已退出，继续清理
            }
        }

        // 3. 没有活跃进程但挂载点仍被占用 → 僵尸残留，尝试 rclone umount
        _logger.Info($"清理残留挂载点: {mount.LocalDrive}（无活跃 rclone 进程）");
        try
        {
            using var unmountProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = RcloneLocator.GetRclonePath(),
                    Arguments = $"umount {mount.LocalDrive}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            unmountProcess.Start();
            await unmountProcess.WaitForExitAsync();

            var exitCode = unmountProcess.ExitCode;
            if (exitCode == 0 || exitCode == 2)
            {
                _logger.Info($"残留挂载点清理完成: {mount.LocalDrive} (退出码: {exitCode})");
            }
            else
            {
                var error = await unmountProcess.StandardError.ReadToEndAsync();
                _logger.Warning($"清理残留挂载点返回非零退出码: {exitCode}, 错误: {error}");
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"清理残留挂载点失败: {mount.LocalDrive}, 错误: {ex.Message}");
        }

        // 4. 等待挂载点释放
        var waitStart = DateTime.UtcNow;
        while (DateTime.UtcNow - waitStart < TimeSpan.FromSeconds(5))
        {
            if (!IsMountPointAccessible(mount.LocalDrive))
            {
                _logger.Info($"残留挂载点 {mount.LocalDrive} 已释放");
                return;
            }
            await Task.Delay(300);
        }

        _logger.Warning($"残留挂载点 {mount.LocalDrive} 在清理后仍被占用");
    }

    private async Task CreateRcloneConfigAsync(MountInfo mount, string? oldName = null)
    {
        // 序列化对 rclone.conf 的写入，防止并发 config create 导致文件锁冲突
        await _configLock.WaitAsync();
        try
        {
            // 如果名称变更了，先删除旧的 remote 配置
            if (!string.IsNullOrEmpty(oldName) && oldName != mount.Name)
            {
                await DeleteRcloneConfigAsyncCore(oldName);
            }

            // 转义名称中的特殊字符
            var escapedName = EscapeRcloneName(mount.Name);
            var configArgs = $"config create {escapedName} {mount.Type} ";
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
                    FileName = RcloneLocator.GetRclonePath(),
                    Arguments = configArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            // 设置代理环境变量
            SetProxyEnvironment(process.StartInfo);

            _logger.Debug($"创建/更新 rclone 配置: {mount.Name}");
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.Error($"创建 rclone 配置失败: {mount.Name}, 错误: {error}");
            }
        }
        finally
        {
            _configLock.Release();
        }
    }

    private async Task DeleteRcloneConfigAsync(string name)
    {
        await _configLock.WaitAsync();
        try
        {
            await DeleteRcloneConfigAsyncCore(name);
        }
        finally
        {
            _configLock.Release();
        }
    }

    /// <summary>
    /// 删除 rclone 配置的内部实现（不含锁，由调用方负责加锁）
    /// </summary>
    private async Task DeleteRcloneConfigAsyncCore(string name)
    {
        try
        {
            var escapedName = EscapeRcloneName(name);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = RcloneLocator.GetRclonePath(),
                    Arguments = $"config delete {escapedName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            _logger.Error($"删除 rclone 配置失败: {name}, 错误: {ex.Message}");
        }
    }

    #endregion 私有方法
}