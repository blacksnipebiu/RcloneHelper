using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RcloneHelper.Helpers;
using RcloneHelper.Models;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Core.Pages;

public partial class SettingsPageViewModel : ObservableObject
{
    private readonly ISystemService _systemService;
    private readonly INotificationService _notificationService;
    private bool _isInitializing = true;

    [ObservableProperty]
    private bool _autoStartEnabled;

    [ObservableProperty]
    private bool _autoMountOnStart;
    
    [ObservableProperty]
    private string _rclonePath = "";

    [ObservableProperty]
    private SystemDependency? _fuseDependency;

    public SettingsPageViewModel(ISystemService systemService, INotificationService notificationService)
    {
        _systemService = systemService;
        _notificationService = notificationService;
        LoadSettings();
        CheckAutoStartStatus();
        CheckDependencies();
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

    partial void OnAutoMountOnStartChanged(bool value)
    {
        if (_isInitializing) return;
        SaveSettingsToFile();
    }
    
    partial void OnRclonePathChanged(string value)
    {
        if (_isInitializing) return;
        SaveSettingsToFile();
    }

    private void LoadSettings()
    {
        var settingsPath = PathUtil.SettingsPath;
        if (File.Exists(settingsPath))
        {
            try
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig);
                if (settings != null)
                {
                    AutoMountOnStart = settings.AutoMountOnStart;
                    RclonePath = settings.RclonePath;
                }
            }
            catch { }
        }
    }

    private void SaveSettingsToFile()
    {
        try
        {
            var settings = new AppConfig
            {
                AutoMountOnStart = AutoMountOnStart,
                RclonePath = RclonePath
            };
            var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.AppConfig);
            File.WriteAllText(PathUtil.SettingsPath, json);
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"保存设置失败: {ex.Message}");
        }
    }

    private void CheckAutoStartStatus()
    {
        AutoStartEnabled = _systemService.IsAutoStartEnabled;
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
}