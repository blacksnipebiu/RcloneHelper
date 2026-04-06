using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RcloneHelper.Core.Models;
using RcloneHelper.Core.Pages;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Core.Windows;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISystemService _systemService;
    private readonly INotificationService _notificationService;
    private readonly IConfigService _configService;
    private bool _hasInitialized;

    [ObservableProperty]
    private bool _isHomeSelected = true;

    [ObservableProperty]
    private bool _isRcloneSelected = false;

    [ObservableProperty]
    private bool _isSettingsSelected = false;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private bool _isDarkMode;

    public string AppVersion => $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    /// <summary>
    /// Toast 通知集合
    /// </summary>
    public ObservableCollection<ToastItem> Toasts { get; } = new();

    public HomePageViewModel HomePageViewModel { get; }
    public RcloneConfigPageViewModel RcloneConfigPageViewModel { get; }
    public SettingsPageViewModel SettingsPageViewModel { get; }

    public MainWindowViewModel(
        HomePageViewModel homePageViewModel,
        RcloneConfigPageViewModel rcloneConfigPageViewModel,
        SettingsPageViewModel settingsPageViewModel,
        ISystemService systemService,
        INotificationService notificationService,
        IConfigService configService)
    {
        HomePageViewModel = homePageViewModel;
        RcloneConfigPageViewModel = rcloneConfigPageViewModel;
        SettingsPageViewModel = settingsPageViewModel;
        _systemService = systemService;
        _notificationService = notificationService;
        _configService = configService;

        CurrentPage = HomePageViewModel;
        IsDarkMode = configService.Current.IsDarkMode;

        // 订阅通知事件
        _notificationService.NotificationRequested += OnNotificationRequested;
    }

    /// <summary>
    /// 处理通知请求
    /// </summary>
    private void OnNotificationRequested(string message, NotificationType type, int duration)
    {
        var toast = new ToastItem(message, type.ToString().ToLower(), t =>
        {
            Toasts.Remove(t);
        });
        Toasts.Insert(0, toast);
        toast.StartTimer(duration);
    }

    [RelayCommand]
    private async Task WindowOpened()
    {
        // 只执行一次
        if (_hasInitialized) return;
        _hasInitialized = true;

        // 检查 FUSE 依赖
        var fuseDependency = _systemService.GetFuseDependency();
        if (fuseDependency?.NeedsInstallation == true)
        {
            // 强制导航到 Rclone 管理界面
            NavigateRclone();
            return;
        }

        // 刷新挂载状态
        HomePageViewModel.RefreshMountStatus();

        // 自动挂载所有配置了"启动时自动挂载"的存储
        await HomePageViewModel.AutoMountConfiguredAsync();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        _configService.Update(c => c.IsDarkMode = IsDarkMode);
    }

    [RelayCommand]
    private void NavigateHome()
    {
        IsHomeSelected = true;
        IsRcloneSelected = false;
        IsSettingsSelected = false;
        CurrentPage = HomePageViewModel;
    }

    [RelayCommand]
    private void NavigateRclone()
    {
        IsHomeSelected = false;
        IsRcloneSelected = true;
        IsSettingsSelected = false;
        CurrentPage = RcloneConfigPageViewModel;
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        IsHomeSelected = false;
        IsRcloneSelected = false;
        IsSettingsSelected = true;
        CurrentPage = SettingsPageViewModel;
    }
}