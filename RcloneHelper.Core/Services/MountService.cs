using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
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
    private readonly Dictionary<string, Process> _mountProcesses = new();
    private readonly SynchronizationContext? _syncContext;

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

    private string GetRclonePath()
    {
        // 1. 优先查找 %APPDATA%\RcloneHelper 目录
        var appDataRclone = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(PathUtil.AppDataDir, "rclone.exe")
            : Path.Combine(PathUtil.AppDataDir, "rclone");

        if (File.Exists(appDataRclone))
            return appDataRclone;

        // 2. 查找程序目录
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var localRclone = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(appDir, "rclone.exe")
            : Path.Combine(appDir, "rclone");

        if (File.Exists(localRclone))
            return localRclone;

        // 3. 从 PATH 环境变量查找实际路径
        var pathRclone = FindRcloneInPath();
        if (pathRclone != null)
            return pathRclone;

        // 4. 回退到使用 "rclone"，依赖系统 PATH
        return "rclone";
    }

    private static string? FindRcloneInPath()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
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
                // where/which 可能返回多行，取第一行
                var path = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
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
            var configs = JsonSerializer.Deserialize<List<MountConfig>>(json);
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
            var json = JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true });
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
            _logger.Debug($"删除 rclone 配置: {name}");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetRclonePath(),
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

            // 构建挂载参数
            var mountArgs = BuildMountArgs(mount);

            var mountProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetRclonePath(),
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
                                if (mount.IsMounted)
                                {
                                    mount.IsMounted = false;
                                    mount.Status = "未挂载";
                                    _logger.Warning($"挂载意外退出: {name}");
                                    _notificationService.ShowWarning($"挂载 \"{name}\" 已意外断开");
                                }
                                _mountProcesses.Remove(name);
                                mount.MountProcess = null;
                            }

                            // 如果有同步上下文，在 UI 线程执行；否则直接执行
                            if (_syncContext != null)
                                _syncContext.Post(_ => HandleExit(), null);
                            else
                                HandleExit();
                        };

            mountProcess.Start();
            mount.MountProcess = mountProcess;
            _mountProcesses[name] = mountProcess;
            _logger.Debug($"执行命令: {GetRclonePath()} {mountArgs}");

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
                    _mountProcesses.Remove(name);
                    mount.MountProcess = null;
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
                    mount.IsMounting = false;
                    mount.IsMounted = true;
                    mount.Status = $"已挂载到 {mount.LocalDrive}";
                    mount.MountCancellationTokenSource = null;
                    _logger.Info($"挂载成功: {name} -> {mount.LocalDrive}, 耗时 {(DateTime.UtcNow - startTime).TotalSeconds:F1}s");
                    return true;
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

        _logger.Debug($"代理已启用: {proxyUrl}");
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
    }/// <summary>
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

        if (!mount.IsMounted && mount.MountProcess == null)
            return;

        try
        {
            _logger.Info($"开始卸载: {name} ({mount.LocalDrive})");

            // 终止挂载进程
            if (mount.MountProcess != null && !mount.MountProcess.HasExited)
            {
                mount.MountProcess.Kill();
                mount.MountProcess.WaitForExit(5000);
                _logger.Debug($"已终止挂载进程: {name}");
            }

            // 执行 rclone umount
            var unmountProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetRclonePath(),
                    Arguments = $"umount {mount.LocalDrive}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            unmountProcess.Start();
            unmountProcess.WaitForExit(5000);
            _logger.Debug($"执行命令: {GetRclonePath()} umount {mount.LocalDrive}");

            mount.IsMounted = false;
            mount.Status = "未挂载";
            mount.MountProcess = null;
            _mountProcesses.Remove(name);
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
    /// 刷新所有挂载的状态
    /// </summary>
    public void RefreshMountStatus()
    {
        var activeMounts = GetActiveMounts();

        foreach (var mount in Mounts)
        {
            // remote 名称已经去掉了冒号，直接匹配配置名称
            var active = activeMounts.FirstOrDefault(a => a.Name == mount.Name);

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
            foreach (var process in _systemService.FindProcesses("rclone"))
            {
                var cmdLine = process.CommandLine;

                // 使用正则提取: mount 之后的两个非选项参数 (remote 和 mountpoint)
                // 格式: rclone [opts] mount [opts] remote:path X:
                var match = Regex.Match(cmdLine,
                    @"\bmount\b\s+(?:-[^\s]+\s+)*(\S+)\s+(?:-[^\s]+\s+)*([A-Za-z]:)",
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                    continue;

                var remote = match.Groups[1].Value;
                var mountpoint = match.Groups[2].Value;

                // 去掉 remote 的冒号部分 (myremote: → myremote, myremote:/path → myremote)
                var colonIndex = remote.IndexOf(':');
                if (colonIndex > 0)
                    remote = remote.Substring(0, colonIndex);

                mounts.Add(new ActiveMountInfo
                {
                    Name = remote,
                    LocalDrive = mountpoint,
                    ProcessId = process.ProcessId
                });
            }
        }
        catch { }

        return mounts;
    }

    /// <summary>
    /// 获取当前平台类型
    /// </summary>
    public PlatformType Platform => _systemService.Platform;

    #endregion

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

    private async Task CreateRcloneConfigAsync(MountInfo mount, string? oldName = null)
    {
        // 如果名称变更了，先删除旧的 remote 配置
        if (!string.IsNullOrEmpty(oldName) && oldName != mount.Name)
        {
            await DeleteRcloneConfigAsync(oldName);
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
                FileName = GetRclonePath(),
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

    private async Task DeleteRcloneConfigAsync(string name)
    {
        try
        {
            _logger.Debug($"删除旧的 rclone 配置: {name}");
            var escapedName = EscapeRcloneName(name);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetRclonePath(),
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