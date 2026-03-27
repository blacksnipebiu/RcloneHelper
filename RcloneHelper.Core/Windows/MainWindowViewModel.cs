using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RcloneHelper.Core.Pages;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Core.Windows;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISystemService _systemService;
    private bool _hasInitialized;

    [ObservableProperty]
    private bool _isHomeSelected = true;

    [ObservableProperty]
    private bool _isRcloneSelected = false;

    [ObservableProperty]
    private bool _isSettingsSelected = false;

    [ObservableProperty]
    private object? _currentPage;

    public HomePageViewModel HomePageViewModel { get; }
    public RcloneConfigPageViewModel RcloneConfigPageViewModel { get; }
    public SettingsPageViewModel SettingsPageViewModel { get; }

    public MainWindowViewModel(
        HomePageViewModel homePageViewModel,
        RcloneConfigPageViewModel rcloneConfigPageViewModel,
        SettingsPageViewModel settingsPageViewModel,
        ISystemService systemService)
    {
        HomePageViewModel = homePageViewModel;
        RcloneConfigPageViewModel = rcloneConfigPageViewModel;
        SettingsPageViewModel = settingsPageViewModel;
        _systemService = systemService;

        CurrentPage = HomePageViewModel;
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