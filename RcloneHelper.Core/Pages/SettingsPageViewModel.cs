using System;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
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

    public SettingsPageViewModel(ISystemService systemService, INotificationService notificationService)
    {
        _systemService = systemService;
        _notificationService = notificationService;
        LoadSettings();
        CheckAutoStartStatus();
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
            // 恢复原状态
            AutoStartEnabled = !value;
        }
    }

    partial void OnAutoMountOnStartChanged(bool value)
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
                var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);
                if (settings != null)
                {
                    AutoMountOnStart = settings.AutoMountOnStart;
                }
            }
            catch { }
        }
    }

    private void SaveSettingsToFile()
    {
        try
        {
            var settings = new AppSettings
            {
                AutoMountOnStart = AutoMountOnStart
            };
            var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings);
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
}