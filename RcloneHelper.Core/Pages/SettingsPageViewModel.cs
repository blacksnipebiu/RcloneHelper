using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RcloneHelper.Helpers;
using RcloneHelper.Models;
using RcloneHelper.Services;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Core.Pages;

public partial class SettingsPageViewModel : ObservableObject
{
    private readonly ISystemService _systemService;
    private readonly INotificationService _notificationService;
    private readonly IConfigService _configService;
    private readonly MountService _mountService;
    private bool _isInitializing = true;

    [ObservableProperty]
    private bool _autoStartEnabled;

    [ObservableProperty]
    private bool _startSilently;

    [ObservableProperty]
    private bool _proxyEnabled;

    [ObservableProperty]
    private string _proxyProtocol = "http";

    [ObservableProperty]
    private string _proxyHost = "";

    [ObservableProperty]
    private int _proxyPort = 7890;

    /// <summary>
    /// 代理协议选项列表
    /// </summary>
    public string[] ProxyProtocolOptions { get; } = new[] { "http", "https", "socks5" };

    /// <summary>
    /// 程序数据存放路径
    /// </summary>
    public string AppDataPath => PathUtil.AppDataDir;

    /// <summary>
    /// 程序所在位置
    /// </summary>
    public string AppLocation => PathUtil.AppLocation;

    public SettingsPageViewModel(
        ISystemService systemService,
        INotificationService notificationService,
        IConfigService configService,
        MountService mountService)
    {
        _systemService = systemService;
        _notificationService = notificationService;
        _configService = configService;
        _mountService = mountService;

        CheckAutoStartStatus();
        LoadSettings();
        _isInitializing = false;
    }

    partial void OnAutoStartEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        if (_systemService.SetAutoStart(value))
        {
            var message = value ? "已添加到开机启动" : "已移除开机启动";
            _notificationService.ShowSuccess(message);
        }
        else
        {
            _notificationService.ShowError("设置开机启动失败");
            AutoStartEnabled = !value;
        }
    }

    partial void OnStartSilentlyChanged(bool value)
    {
        if (_isInitializing) return;

        _configService.Update(c => c.StartSilently = value);
    }

    partial void OnProxyEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        _configService.Update(c => c.ProxyEnabled = value);
    }

    partial void OnProxyProtocolChanged(string value)
    {
        if (_isInitializing) return;

        _configService.Update(c => c.ProxyProtocol = value);
    }

    partial void OnProxyHostChanged(string value)
    {
        if (_isInitializing) return;

        _configService.Update(c => c.ProxyHost = value);
    }

    partial void OnProxyPortChanged(int value)
    {
        if (_isInitializing) return;

        _configService.Update(c => c.ProxyPort = value);
    }

    private void CheckAutoStartStatus()
    {
        AutoStartEnabled = _systemService.IsAutoStartEnabled;
    }

    private void LoadSettings()
    {
        var config = _configService.Current;
        StartSilently = config.StartSilently;
        ProxyEnabled = config.ProxyEnabled;
        ProxyProtocol = config.ProxyProtocol;
        ProxyHost = config.ProxyHost;
        ProxyPort = config.ProxyPort;
    }

    [RelayCommand]
    private void OpenAppDataFolder()
    {
        try
        {
            if (Directory.Exists(AppDataPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppDataPath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"无法打开文件夹: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenAppLocationFolder()
    {
        try
        {
            if (Directory.Exists(AppLocation))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppLocation,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"无法打开文件夹: {ex.Message}");
        }
    }

    /// <summary>
    /// 导出所有挂载配置到 JSON 文件
    /// </summary>
    [RelayCommand]
    private void ExportConfig()
    {
        try
        {
            var exportPath = Path.Combine(AppDataPath, $"rclonehelper_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            var configs = _mountService.Mounts.Select(m => m.ToConfig()).ToList();
            var json = JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(exportPath, json);

            _notificationService.ShowSuccess($"配置已导出到: {exportPath}");

            // 打开导出目录
            Process.Start(new ProcessStartInfo
            {
                FileName = AppDataPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"导出失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 JSON 文件导入挂载配置
    /// </summary>
    [RelayCommand]
    private void ImportConfig()
    {
        try
        {
            // 查找最新的备份文件
            var backupFiles = Directory.GetFiles(AppDataPath, "rclonehelper_backup_*.json")
                .OrderByDescending(f => f)
                .ToList();

            if (backupFiles.Count == 0)
            {
                _notificationService.ShowWarning("未找到备份文件，请将备份文件放入配置目录后重试");
                OpenAppDataFolder();
                return;
            }

            // 使用最新的备份文件
            var importPath = backupFiles.First();
            var fileName = Path.GetFileName(importPath);

            var json = File.ReadAllText(importPath);
            var configs = JsonSerializer.Deserialize<List<MountConfig>>(json);

            if (configs == null || configs.Count == 0)
            {
                _notificationService.ShowWarning($"备份文件 {fileName} 中没有配置数据");
                return;
            }

            // 导入配置（跳过已存在的名称）
            var imported = 0;
            var skipped = 0;
            foreach (var config in configs)
            {
                if (_mountService.Mounts.Any(m => m.Name == config.Name))
                {
                    skipped++;
                    continue;
                }

                var mount = MountInfo.FromConfig(config);
                _mountService.AddMount(mount);
                imported++;
            }

            _notificationService.ShowSuccess($"从 {fileName} 导入完成: 新增 {imported} 个，跳过 {skipped} 个已存在");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"导入失败: {ex.Message}");
        }
    }
}