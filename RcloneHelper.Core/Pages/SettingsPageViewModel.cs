using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RcloneHelper.Helpers;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Core.Pages;

public partial class SettingsPageViewModel : ObservableObject
{
    private readonly ISystemService _systemService;
    private readonly INotificationService _notificationService;
    private readonly IConfigService _configService;
    private bool _isInitializing = true;

    [ObservableProperty]
    private bool _autoStartEnabled;

    [ObservableProperty]
    private bool _startSilently;

    /// <summary>
    /// 程序数据存放路径
    /// </summary>
    public string AppDataPath => PathUtil.AppDataDir;

    /// <summary>
    /// 日志文件路径
    /// </summary>
    public string LogPath => PathUtil.LogPath;

    public SettingsPageViewModel(
        ISystemService systemService,
        INotificationService notificationService,
        IConfigService configService)
    {
        _systemService = systemService;
        _notificationService = notificationService;
        _configService = configService;

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

    private void CheckAutoStartStatus()
    {
        AutoStartEnabled = _systemService.IsAutoStartEnabled;
    }

    private void LoadSettings()
    {
        var config = _configService.Current;
        StartSilently = config.StartSilently;
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
    private void OpenLogFolder()
    {
        try
        {
            var logDir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(logDir) && Directory.Exists(logDir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = logDir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"无法打开文件夹: {ex.Message}");
        }
    }
}