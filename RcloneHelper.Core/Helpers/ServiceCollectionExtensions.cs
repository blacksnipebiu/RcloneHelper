using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using RcloneHelper.Core.Pages;
using RcloneHelper.Core.Windows;
using RcloneHelper.Services;
using RcloneHelper.Services.Abstractions;
using RcloneHelper.Services.Implementations;

namespace RcloneHelper.Helpers;

/// <summary>
/// 服务注册扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册应用程序所有服务
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // 注册日志服务
        services.AddSingleton<ILoggerService, FileLoggerService>();

        // 注册通知服务
        services.AddSingleton<INotificationService, NotificationService>();

        // 注册配置服务
        services.AddSingleton<IConfigService>(sp => new ConfigService(
            sp.GetRequiredService<ILoggerService>()));

        // 注册系统服务（跨平台）
        services.AddSingleton<ISystemService>(sp => CreateSystemService(sp));

        // 注册服务层 - Singleton 确保全局唯一实例
        services.AddSingleton<MountService>(sp => new MountService(
            sp.GetRequiredService<ISystemService>(),
            sp.GetRequiredService<ILoggerService>(),
            sp.GetRequiredService<IConfigService>(),
            sp.GetRequiredService<INotificationService>()));

        // 注册 ViewModels - Singleton 保留桌面应用状态
        services.AddSingleton<HomePageViewModel>(sp => new HomePageViewModel(
            sp.GetRequiredService<MountService>(),
            sp.GetRequiredService<INotificationService>(),
            sp.GetRequiredService<IDialogService>()));
        services.AddSingleton<MainWindowViewModel>(sp => new MainWindowViewModel(
                    sp.GetRequiredService<HomePageViewModel>(),
                    sp.GetRequiredService<RcloneConfigPageViewModel>(),
                    sp.GetRequiredService<SettingsPageViewModel>(),
                    sp.GetRequiredService<ISystemService>(),
                    sp.GetRequiredService<INotificationService>(),
                    sp.GetRequiredService<IConfigService>()));
        services.AddSingleton<RcloneConfigPageViewModel>(sp => new RcloneConfigPageViewModel(
                    sp.GetRequiredService<ISystemService>(),
                    sp.GetRequiredService<INotificationService>(),
                    sp.GetRequiredService<IConfigService>(),
                    sp.GetRequiredService<MountService>()));
        services.AddSingleton<SettingsPageViewModel>(sp => new SettingsPageViewModel(
            sp.GetRequiredService<ISystemService>(),
            sp.GetRequiredService<INotificationService>(),
            sp.GetRequiredService<IConfigService>(),
            sp.GetRequiredService<MountService>()));

        return services;
    }

    /// <summary>
    /// 根据当前平台创建对应的系统服务实现
    /// </summary>
    [UnconditionalSuppressMessage("Interoperability", "CA1416:PlatformCompatibility",
        Justification = "已通过 RuntimeInformation.IsOSPlatform 进行平台检查")]
    private static ISystemService CreateSystemService(IServiceProvider sp)
    {
        var logger = sp.GetRequiredService<ILoggerService>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsSystemService(logger);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxSystemService(logger);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacOSSystemService(logger);

        // 不支持的平台，返回 Windows 实现作为默认
        return new WindowsSystemService(logger);
    }
}