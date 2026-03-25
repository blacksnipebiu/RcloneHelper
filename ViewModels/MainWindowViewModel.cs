using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.Win32;
using RcloneHelper.Models;
using RcloneHelper.Views;

namespace RcloneHelper.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly string _configPath;
    private readonly string _settingsPath;
    private readonly string _rclonePath;

// 所有挂载的集合
    [ObservableProperty]
    private ObservableCollection<MountInfo> _allMounts = new();

    // 已挂载列表
    [ObservableProperty]
    private ObservableCollection<MountInfo> _mountedMounts = new();

    // 未挂载列表
    [ObservableProperty]
    private ObservableCollection<MountInfo> _unmountedMounts = new();

    [ObservableProperty]
    private MountInfo? _selectedMount;

    [ObservableProperty]
    private bool _autoStartEnabled;

    [ObservableProperty]
    private bool _autoMountOnStart;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private MountInfo _editingMount = new();

    // 导航状态
    [ObservableProperty]
    private bool _isHomeSelected = true;

    [ObservableProperty]
    private bool _isRcloneSelected = false;

    [ObservableProperty]
    private object? _currentPage;

    // 页面实例
    private readonly HomePage _homePage;

    private readonly RcloneConfigPage _rcloneConfigPage;

    public MainWindowViewModel()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appData, "RcloneHelper");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "mounts.json");
        _settingsPath = Path.Combine(configDir, "settings.json");
        _rclonePath = FindRclonePath();

        // 创建页面实例 - 主页是挂载管理，Rclone管理页是Rclone配置
        _homePage = new HomePage { DataContext = this };
        _rcloneConfigPage = new RcloneConfigPage { DataContext = new RcloneConfigPageViewModel() };

        // 默认显示主页（挂载管理）
        CurrentPage = _homePage;

        LoadMounts();
        LoadSettings();
        CheckAutoStartStatus();

        // 如果设置了启动时自动挂载，执行挂载
        if (AutoMountOnStart)
        {
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                await MountAllAsync();
            });
        }
    }

    [RelayCommand]
    private void NavigateHome()
    {
        IsHomeSelected = true;
        IsRcloneSelected = false;
        CurrentPage = _homePage;
    }

    [RelayCommand]
    private void NavigateRclone()
    {
        IsHomeSelected = false;
        IsRcloneSelected = true;
        CurrentPage = _rcloneConfigPage;
    }

    private string FindRclonePath()
    {
        // 首先检查环境变量 PATH 中是否有 rclone
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = pathEnv.Split(Path.PathSeparator);

        foreach (var path in paths)
        {
            var fullPath = Path.Combine(path, "rclone.exe");
            if (File.Exists(fullPath))
                return fullPath;
        }

        // 检查默认安装位置
        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "rclone", "rclone.exe");
        if (File.Exists(defaultPath))
            return defaultPath;

        // 检查当前目录
        var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rclone.exe");
        if (File.Exists(localPath))
            return localPath;

        return "rclone";
    }

    private void LoadMounts()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var configs = JsonSerializer.Deserialize<ObservableCollection<MountConfig>>(json);
                if (configs != null)
                {
                    foreach (var config in configs)
                    {
                        var mount = MountInfo.FromConfig(config);
                        AllMounts.Add(mount);
                        UnmountedMounts.Add(mount);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载配置失败: {ex.Message}";
            }
        }
    }

    private void LoadSettings()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    AutoMountOnStart = settings.AutoMountOnStart;
                }
            }
            catch { }
        }
    }

    private void SaveMountsToFile()
    {
        try
        {
            var configs = new ObservableCollection<MountConfig>(
                AllMounts.Select(m => m.ToConfig())
            );
            var json = JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存配置失败: {ex.Message}";
        }
    }

    private void SaveSettingsToFile()
    {
        try
        {
            var settings = new AppSettings { AutoMountOnStart = AutoMountOnStart };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存设置失败: {ex.Message}";
        }
    }

    private void CheckAutoStartStatus()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            AutoStartEnabled = key?.GetValue("RcloneHelper") != null;
        }
        catch
        {
            AutoStartEnabled = false;
        }
    }

    // 移动挂载到已挂载列表
    private void MoveToMounted(MountInfo mount)
    {
        if (UnmountedMounts.Contains(mount))
            UnmountedMounts.Remove(mount);
        if (!MountedMounts.Contains(mount))
            MountedMounts.Add(mount);
        mount.IsMounted = true;
        mount.Status = $"已挂载到 {mount.LocalDrive}";
    }

    // 移动挂载到未挂载列表
    private void MoveToUnmounted(MountInfo mount)
    {
        if (MountedMounts.Contains(mount))
            MountedMounts.Remove(mount);
        if (!UnmountedMounts.Contains(mount))
            UnmountedMounts.Add(mount);
        mount.IsMounted = false;
        mount.Status = "未挂载";
    }

    [RelayCommand]
    private void AddMount()
    {
        EditingMount = new MountInfo
        {
            LocalDrive = GetAvailableDrive()
        };
IsEditing = true;
    }

    [RelayCommand]
    private void EditMount()
    {
        if (SelectedMount == null) return;
        EditMountInternal(SelectedMount);
    }

    private void EditMountInternal(MountInfo mount)
    {
        EditingMount = new MountInfo
        {
            Name = mount.Name,
            RemotePath = mount.RemotePath,
            LocalDrive = mount.LocalDrive,
            Type = mount.Type,
            Url = mount.Url,
            User = mount.User,
            Password = mount.Password,
            Vendor = mount.Vendor,
            AutoMountOnStart = mount.AutoMountOnStart
        };
        IsEditing = true;
    }

    [RelayCommand]
    private void DeleteMount()
    {
        if (SelectedMount == null) return;
        DeleteMountInternal(SelectedMount);
    }

    private void DeleteMountInternal(MountInfo mount)
    {
        if (mount.IsMounted)
        {
            Unmount(mount);
        }

        AllMounts.Remove(mount);
        MountedMounts.Remove(mount);
        UnmountedMounts.Remove(mount);
        SelectedMount = null;
        SaveMountsToFile();
        StatusMessage = "挂载已删除";
    }

    [RelayCommand]
    private void SaveMount()
    {
        if (string.IsNullOrWhiteSpace(EditingMount.Name))
        {
            StatusMessage = "请输入名称";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditingMount.Url))
        {
            StatusMessage = "请输入 URL";
            return;
        }

        // 检查是否已存在同名挂载
        var existing = AllMounts.FirstOrDefault(m => m.Name == EditingMount.Name);
        if (existing != null)
        {
            // 更新现有挂载
            existing.RemotePath = EditingMount.RemotePath;
            existing.LocalDrive = EditingMount.LocalDrive;
            existing.Type = EditingMount.Type;
            existing.Url = EditingMount.Url;
            existing.User = EditingMount.User;
            existing.Password = EditingMount.Password;
            existing.Vendor = EditingMount.Vendor;
        }
        else
        {
            // 添加新挂载
            var newMount = new MountInfo
            {
                Name = EditingMount.Name,
                RemotePath = EditingMount.RemotePath,
                LocalDrive = EditingMount.LocalDrive,
                Type = EditingMount.Type,
                Url = EditingMount.Url,
                User = EditingMount.User,
                Password = EditingMount.Password,
                Vendor = EditingMount.Vendor,
                IsMounted = false,
                Status = "未挂载"
            };
            AllMounts.Add(newMount);
            UnmountedMounts.Add(newMount);
        }

        SaveMountsToFile();
        IsEditing = false;
        StatusMessage = "挂载已保存";
    }

    [RelayCommand]
private void CancelEdit()
    {
        IsEditing = false;
    }

    [RelayCommand]
    private async Task MountSelected()
    {
        if (SelectedMount == null || SelectedMount.IsMounted) return;
        await MountAsync(SelectedMount);
    }

    [RelayCommand]
    private async Task UnmountSelected()
    {
        if (SelectedMount == null || !SelectedMount.IsMounted) return;
        await UnmountAsync(SelectedMount);
    }

    [RelayCommand]
    private async Task MountAllAsync()
    {
        var toMount = UnmountedMounts.ToList();
        foreach (var mount in toMount)
        {
            await MountAsync(mount);
        }
    }

    [RelayCommand]
    private async Task UnmountAllAsync()
    {
        var toUnmount = MountedMounts.ToList();
        foreach (var mount in toUnmount)
        {
            await UnmountAsync(mount);
        }
    }

    private async Task MountAsync(MountInfo mount)
    {
        try
{
            mount.Status = "正在挂载...";

            // 先配置 rclone
            var configName = mount.Name;
            var configArgs = $"config create {configName} {mount.Type} ";
            configArgs += $"url \"{mount.Url}\" ";
            if (!string.IsNullOrWhiteSpace(mount.User))
                configArgs += $"user \"{mount.User}\" ";
            if (!string.IsNullOrWhiteSpace(mount.Password))
                configArgs += $"pass \"{mount.Password}\" ";
            if (mount.Type == "webdav" && !string.IsNullOrWhiteSpace(mount.Vendor))
                configArgs += $"vendor \"{mount.Vendor}\" ";

            var configProcess = new Process
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

            configProcess.Start();
            await configProcess.WaitForExitAsync();

            // 执行挂载
            var remotePath = string.IsNullOrWhiteSpace(mount.RemotePath)
                ? configName
                : $"{configName}:{mount.RemotePath}";

            var mountArgs = $"mount {remotePath} {mount.LocalDrive} --vfs-cache-mode writes --network-mode";

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
                // 进程退出时移动到未挂载列表
                if (mount.IsMounted)
                {
                    MoveToUnmounted(mount);
                }
                mount.MountProcess = null;
            };

            mountProcess.Start();
            mount.MountProcess = mountProcess;

            // 等待一下检查是否启动成功
            await Task.Delay(2000);

            if (!mountProcess.HasExited)
            {
                MoveToMounted(mount);
                StatusMessage = $"{mount.Name} 挂载成功";
            }
            else
            {
                var error = await mountProcess.StandardError.ReadToEndAsync();
                mount.Status = $"挂载失败";
                StatusMessage = $"挂载失败: {error}";
            }
        }
        catch (Exception ex)
        {
            mount.Status = "挂载失败";
            StatusMessage = $"挂载错误: {ex.Message}";
        }
    }

    private async Task UnmountAsync(MountInfo mount)
    {
        Unmount(mount);
    }

    private void Unmount(MountInfo mount)
    {
        try
        {
            if (mount.MountProcess != null && !mount.MountProcess.HasExited)
            {
                mount.MountProcess.Kill();
                mount.MountProcess.WaitForExit(5000);
            }

            // 使用 rclone 卸载
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

            MoveToUnmounted(mount);
            mount.MountProcess = null;
            StatusMessage = $"{mount.Name} 已卸载";
        }
        catch (Exception ex)
        {
            StatusMessage = $"卸载错误: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (AutoStartEnabled)
            {
                key.SetValue("RcloneHelper", $"\"{System.Reflection.Assembly.GetExecutingAssembly().Location}\"");
                StatusMessage = "已添加到开机启动";
            }
            else
            {
                key.DeleteValue("RcloneHelper", false);
                StatusMessage = "已移除开机启动";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"设置开机启动失败: {ex.Message}";
            AutoStartEnabled = !AutoStartEnabled;
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        SaveSettingsToFile();
        StatusMessage = "设置已保存";
    }

    private string GetAvailableDrive()
    {
        var usedDrives = AllMounts.Select(m => m.LocalDrive.ToUpper()).ToHashSet();
        for (char c = 'Z'; c >= 'D'; c--)
        {
            var drive = $"{c}:";
            if (!usedDrives.Contains(drive))
                return drive;
        }
        return "Z:";
    }
}