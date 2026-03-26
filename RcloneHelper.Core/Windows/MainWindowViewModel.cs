using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RcloneHelper.Core.Pages;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Core.Windows;

public partial class MainWindowViewModel : ObservableObject
{
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

        CurrentPage = HomePageViewModel;
        _ = CheckDependenciesAndMountAsync(systemService);
    }

    private async Task CheckDependenciesAndMountAsync(ISystemService systemService)
    {
        await Task.Delay(500);

        // 检查 FUSE 依赖
        var fuseDependency = systemService.GetFuseDependency();
        if (fuseDependency?.NeedsInstallation == true)
        {
            // 强制导航到设置页面
            NavigateSettings();
            return;
        }

        // 刷新挂载状态
        HomePageViewModel.RefreshMountStatus();

        if (SettingsPageViewModel.AutoMountOnStart)
        {
            await Task.Delay(500);
            await HomePageViewModel.AutoMountAllAsync();
        }
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