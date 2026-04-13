using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using RcloneHelper.Helpers;
using RcloneHelper.Models;
using RcloneHelper.Services;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Core.Pages;

public partial class RcloneConfigPageViewModel : ObservableObject
{
    private readonly ISystemService _systemService;
    private readonly INotificationService _notificationService;
    private readonly IConfigService _configService;
    private readonly MountService _mountService;

    [ObservableProperty]
    private string _rcloneVersion = "正在获取...";

    [ObservableProperty]
    private string _configFilePath = "正在获取...";

    [ObservableProperty]
    private string _cacheDir = "正在获取...";

    [ObservableProperty]
    private string _configContent = "";

    [ObservableProperty]
    private ObservableCollection<RemoteConfig> _remotes = new();

    [ObservableProperty]
    private RemoteConfig? _selectedRemote;

    [ObservableProperty]
    private SystemDependency? _fuseDependency;

    [ObservableProperty]
    private SystemDependency? _rcloneDependency;

    public RcloneConfigPageViewModel(
        ISystemService systemService,
        INotificationService notificationService,
        IConfigService configService,
        MountService mountService)
    {
        _systemService = systemService;
        _notificationService = notificationService;
        _configService = configService;
        _mountService = mountService;

        CheckDependencies();
        LoadRcloneInfo();
    }

    private void LoadRcloneInfo()
    {
        try
        {
            var rclonePath = RcloneLocator.GetRclonePath();
            if (rclonePath != null && File.Exists(rclonePath))
            {
                var versionInfo = GetFileVersion(rclonePath);
                RcloneVersion = $"v{versionInfo}";
            }
            else
            {
                RcloneVersion = "未找到 rclone";
            }

            ConfigFilePath = PathUtil.RcloneConfigPath;
            CacheDir = PathUtil.RcloneCacheDir;

            LoadRemotesWithMountStatus();
        }
        catch (Exception ex)
        {
            RcloneVersion = $"获取失败: {ex.Message}";
        }
    }

    private void LoadRemotesWithMountStatus()
    {
        var activeMounts = _systemService.GetActiveMountNames();

        Remotes.Clear();
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                ConfigContent = File.ReadAllText(ConfigFilePath);
                var lines = ConfigContent.Split('\n');
                RemoteConfig? currentRemote = null;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                        continue;

                    var remoteMatch = Regex.Match(trimmed, @"^\[([^\]]+)\]$");
                    if (remoteMatch.Success)
                    {
                        if (currentRemote != null)
                        {
                            Remotes.Add(currentRemote);
                        }
                        currentRemote = new RemoteConfig { Name = remoteMatch.Groups[1].Value };
                        continue;
                    }

                    var kvMatch = Regex.Match(trimmed, @"^([^=]+)=(.*)$");
                    if (kvMatch.Success && currentRemote != null)
                    {
                        var key = kvMatch.Groups[1].Value.Trim();
                        var value = kvMatch.Groups[2].Value.Trim();
                        currentRemote.Properties[key] = value;
                    }
                }

                if (currentRemote != null)
                {
                    Remotes.Add(currentRemote);
                }
            }
            else
            {
                ConfigContent = "配置文件不存在";
            }
        }
        catch (Exception ex)
        {
            ConfigContent = $"无法读取配置: {ex.Message}";
        }

        foreach (var remote in Remotes)
        {
            // 查找匹配的挂载点 (名称完全匹配或名称以 remote.Name 开头)
            var mountPoint = activeMounts.FirstOrDefault(m =>
                m.Key.Equals(remote.Name, StringComparison.OrdinalIgnoreCase) ||
                m.Key.StartsWith(remote.Name + ":", StringComparison.OrdinalIgnoreCase)).Value;
            
            if (!string.IsNullOrEmpty(mountPoint))
            {
                remote.IsMounted = true;
                remote.LocalDrive = mountPoint;
            }
            else
            {
                remote.IsMounted = false;
                remote.LocalDrive = "";
            }
        }
    }

    private string GetFileVersion(string filePath)
    {
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
            return versionInfo.FileVersion ?? "未知";
        }
        catch
        {
            return "未知";
        }
    }

    [RelayCommand]
    private void OpenConfigFolder()
    {
        try
        {
            var folder = Path.GetDirectoryName(ConfigFilePath);
            if (folder != null && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }

    [RelayCommand]
    private void OpenCacheFolder()
    {
        if (!Directory.Exists(CacheDir))
        {
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = CacheDir,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void CheckUpdate()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://rclone.org/downloads/",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void RefreshConfig()
    {
        LoadRemotesWithMountStatus();
    }

    [RelayCommand]
    private void DeleteRemote()
    {
        if (SelectedRemote == null || string.IsNullOrWhiteSpace(SelectedRemote.Name))
            return;

        try
        {
            var remoteName = SelectedRemote.Name;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = RcloneLocator.GetRclonePath(),
                    Arguments = $"config delete \"{remoteName}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(5000);

            SelectedRemote = null;
            LoadRemotesWithMountStatus();
        }
        catch
        {
        }
    }

    [RelayCommand]
    private void UnmountSelected()
    {
        if (SelectedRemote == null || !SelectedRemote.IsMounted)
            return;

        if (string.IsNullOrWhiteSpace(SelectedRemote.LocalDrive))
        {
            _notificationService.ShowError("卸载失败: 挂载点信息无效，请刷新后重试");
            return;
        }

        try
        {
            var rclonePath = RcloneLocator.GetRclonePath();
            if (string.IsNullOrEmpty(rclonePath))
            {
                _notificationService.ShowError("卸载失败: 未找到 rclone");
                return;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = rclonePath,
                    Arguments = $"umount {SelectedRemote.LocalDrive}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var exited = process.WaitForExit(5000);

            if (!exited)
            {
                // WaitForExit 超时，进程仍在运行
                try
                {
                    process.Kill();
                }
                catch
                {
                    // 忽略杀死进程时的异常
                }
                _notificationService.ShowError("卸载超时（5秒），请稍后重试或手动卸载");
                return;
            }

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                _notificationService.ShowError($"卸载失败: rclone umount 返回 {process.ExitCode}");
                return;
            }

            _notificationService.ShowSuccess($"已卸载 {SelectedRemote.Name}");
            LoadRemotesWithMountStatus();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"卸载异常: {ex.Message}");
        }
    }

    #region 设置和依赖

    private void CheckDependencies()
    {
        FuseDependency = _systemService.GetFuseDependency();
        RcloneDependency = _systemService.GetRcloneDependency();
    }

    [RelayCommand]
    private void OpenInstallUrl()
    {
        // 优先处理需要安装的依赖
        var dependency = FuseDependency?.NeedsInstallation == true ? FuseDependency :
                         RcloneDependency?.NeedsInstallation == true ? RcloneDependency :
                         FuseDependency?.IsInstalled == false ? FuseDependency :
                         RcloneDependency?.IsInstalled == false ? RcloneDependency : null;

        if (dependency == null || string.IsNullOrEmpty(dependency.InstallUrl))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = dependency.InstallUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"无法打开链接: {ex.Message}");
        }
    }

    [RelayCommand]
    private void RefreshDependencies()
    {
        CheckDependencies();
        _notificationService.ShowSuccess("依赖状态已刷新");
    }

    #endregion
}

public partial class RemoteConfig : ObservableObject
{
    public string Name { get; set; } = "";
    public Dictionary<string, string> Properties { get; set; } = new();

    public string Type => Properties.TryGetValue("type", out var t) ? t : "未知";

    [ObservableProperty]
    private bool _isMounted;

    [ObservableProperty]
    private string _localDrive = "";
}