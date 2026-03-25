using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RcloneHelper.ViewModels;

public partial class RcloneConfigPageViewModel : ObservableObject
{
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

    public RcloneConfigPageViewModel()
    {
        LoadRcloneInfo();
    }

    private void LoadRcloneInfo()
    {
        try
        {
            // 获取 Rclone 版本
            var rclonePath = FindRclonePath();
            if (File.Exists(rclonePath))
            {
                var versionInfo = GetFileVersion(rclonePath);
                RcloneVersion = $"v{versionInfo}";
            }
            else
            {
                RcloneVersion = "未找到 rclone";
            }

            // 获取配置文件路径
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            ConfigFilePath = Path.Combine(appData, "rclone", "rclone.conf");

            // 缓存目录
            CacheDir = Path.Combine(appData, "rclone", "cache");

            // 加载配置文件和挂载状态
            LoadRemotesWithMountStatus();

            // 加载日志
            var logPath = Path.Combine(appData, "RcloneHelper", "app.log");
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
        // 获取当前挂载
        var activeMounts = GetActiveMounts();

        // 解析配置文件
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

                    // 跳过空行和注释
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                        continue;

                    // 匹配 remote 节 [remote_name]
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

                    // 匹配 key = value
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

        // 更新挂载状态
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
            // 使用 WMI 获取正在运行的 rclone 进程及其命令行参数
            var searcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'rclone.exe'");
            foreach (var obj in searcher.Get())
            {
                var processId = Convert.ToInt32(obj["ProcessId"]);
                var commandLine = obj["CommandLine"]?.ToString() ?? "";

                // 解析 mount 命令: rclone mount remote:path drive:
                var mountMatch = Regex.Match(commandLine, @"mount\s+(\S+)\s+([A-Za-z]:)", RegexOptions.IgnoreCase);
                if (mountMatch.Success)
                {
                    mounts.Add(new ActiveMountInfo
                    {
                        Name = mountMatch.Groups[1].Value,
                        LocalDrive = mountMatch.Groups[2].Value,
                        ProcessId = processId
                    });
                }
            }
        }
        catch
        {
            // WMI 可能不可用，忽略错误
        }
        return mounts;
    }

    private string FindRclonePath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = pathEnv.Split(Path.PathSeparator);
        foreach (var path in paths)
        {
            var fullPath = Path.Combine(path, "rclone.exe");
            if (File.Exists(fullPath))
                return fullPath;
        }
        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "rclone", "rclone.exe");
        if (File.Exists(defaultPath))
            return defaultPath;
        return "rclone";
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
            var rclonePath = FindRclonePath();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = rclonePath,
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
            // 忽略错误
        }
    }

    [RelayCommand]
    private void UnmountSelected()
    {
        if (SelectedRemote == null || !SelectedRemote.IsMounted || SelectedRemote.ProcessId == 0)
            return;

        try
        {
            var process = Process.GetProcessById(SelectedRemote.ProcessId);
            process.Kill();
            process.WaitForExit(5000);

            SelectedRemote = null;
            LoadRemotesWithMountStatus();
        }
        catch
        {
            // 忽略错误
        }
    }
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

public class ActiveMountInfo
{
    public string Name { get; set; } = "";
    public string LocalDrive { get; set; } = "";
    public int ProcessId { get; set; }
}