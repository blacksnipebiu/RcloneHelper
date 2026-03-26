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
/// 服务注册扩展方法，支持裁剪友好的依赖注入
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册应用程序所有服务
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "使用具体的类型注册，裁剪器可以保留所有必需的类型")]
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // 注册通知服务
        services.AddSingleton<INotificationService, NotificationService>();

        // 注册系统服务（跨平台）
        services.AddSingleton<ISystemService>(sp => CreateSystemService());

        // 注册服务层 - Singleton 确保全局唯一实例
        services.AddSingleton<MountService>();

        // 注册 ViewModels - Singleton 保留桌面应用状态
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<HomePageViewModel>();
        services.AddSingleton<RcloneConfigPageViewModel>();
        services.AddSingleton<SettingsPageViewModel>();

        return services;
    }

    /// <summary>
    /// 根据当前平台创建对应的系统服务实现
    /// </summary>
    [UnconditionalSuppressMessage("Interoperability", "CA1416:PlatformCompatibility",
        Justification = "已通过 RuntimeInformation.IsOSPlatform 进行平台检查")]
    private static ISystemService CreateSystemService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsSystemService();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxSystemService();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacOSSystemService();

        // 不支持的平台，返回 Windows 实现作为默认
        return new WindowsSystemService();
    }
}