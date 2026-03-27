using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    private bool _isInitializing = true;

    [ObservableProperty]
    private string _rcloneVersion = "正在获取...";

    [ObservableProperty]
    private string _configFilePath = "正在获取...";

    [ObservableProperty]
    private string _cacheDir = "正在获取...";

    [ObservableProperty]
    private string _logContent = "";

    [ObservableProperty]
    private string _configContent = "";

    [ObservableProperty]
    private ObservableCollection<RemoteConfig> _remotes = new();

    [ObservableProperty]
    private RemoteConfig? _selectedRemote;

    [ObservableProperty]
    private string _customRclonePath = "";

    [ObservableProperty]
    private SystemDependency? _fuseDependency;

    public RcloneConfigPageViewModel(
        ISystemService systemService,
        INotificationService notificationService,
        IConfigService configService)
    {
        _systemService = systemService;
        _notificationService = notificationService;
        _configService = configService;

        LoadSettings();
        CheckDependencies();
        LoadRcloneInfo();
        _isInitializing = false;
    }

    private string GetRclonePath()
    {
        var configuredPath = _configService.Current.RclonePath;
        if (!string.IsNullOrEmpty(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        // 默认路径：程序目录或 PATH
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var localPath = Path.Combine(appDir, "rclone.exe");
        if (File.Exists(localPath))
            return localPath;

        return "rclone"; // 依赖 PATH
    }

private void LoadRcloneInfo()
    {
        try
        {
            var rclonePath = GetRclonePath();
            if (File.Exists(rclonePath))
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

            var logPath = PathUtil.LogPath;
            if (File.Exists(logPath))
            {
                LogContent = ReadLastLines(logPath, 100);
            }
            else
            {
                LogContent = "暂无日志";
            }
        }
        catch (Exception ex)
        {
            RcloneVersion = $"获取失败: {ex.Message}";
        }
    }

    private void LoadRemotesWithMountStatus()
    {
        var activeMounts = GetActiveMounts();

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
            var mountInfo = activeMounts.FirstOrDefault(m =>
                m.Name.StartsWith(remote.Name) || m.Name == remote.Name);
            if (mountInfo != null)
            {
                remote.IsMounted = true;
                remote.LocalDrive = mountInfo.LocalDrive;
                remote.ProcessId = mountInfo.ProcessId;
            }
            else
            {
                remote.IsMounted = false;
                remote.LocalDrive = "";
                remote.ProcessId = 0;
            }
        }
    }

    private List<ActiveMountInfo> GetActiveMounts()
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

    private string ReadLastLines(string filePath, int lines)
    {
        try
        {
            var allLines = File.ReadAllLines(filePath);
            var lastLines = allLines.Length > lines
                ? allLines[^lines..]
                : allLines;
            return string.Join(Environment.NewLine, lastLines);
        }
        catch
        {
            return "无法读取日志";
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
        try
        {
            if (Directory.Exists(CacheDir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = CacheDir,
                    UseShellExecute = true
                });
            }
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
                    FileName = GetRclonePath(),
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
        if (SelectedRemote == null || !SelectedRemote.IsMounted || SelectedRemote.ProcessId == 0)
            return;

        try
        {
            _systemService.TerminateProcess(SelectedRemote.ProcessId);

            SelectedRemote = null;
            LoadRemotesWithMountStatus();
        }
        catch
        {
        }
    }

#region 设置和依赖

    partial void OnCustomRclonePathChanged(string value)
    {
        if (_isInitializing) return;
        _configService.Update(c => c.RclonePath = CustomRclonePath);
    }

    private void LoadSettings()
    {
        CustomRclonePath = _configService.Current.RclonePath;
    }

    private void CheckDependencies()
    {
        FuseDependency = _systemService.GetFuseDependency();
    }

    [RelayCommand]
    private void OpenInstallUrl()
    {
        if (FuseDependency == null || string.IsNullOrEmpty(FuseDependency.InstallUrl))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = FuseDependency.InstallUrl,
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

    [ObservableProperty]
    private int _processId;
}