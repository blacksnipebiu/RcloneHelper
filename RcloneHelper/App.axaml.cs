using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RcloneHelper.Core.Windows;
using RcloneHelper.Helpers;
using RcloneHelper.Services.Abstractions;
using RcloneHelper.Views.Windows;

namespace RcloneHelper;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // 配置依赖注入容器
        var services = new ServiceCollection();
        services.AddApplicationServices();
        _serviceProvider = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 从 DI 容器获取 MainWindowViewModel
            var mainWindowViewModel = _serviceProvider!.GetRequiredService<MainWindowViewModel>();

            // 创建 MainWindow
            var mainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };

            // 初始化通知服务
            var notificationService = _serviceProvider!.GetRequiredService<INotificationService>();
            mainWindow.InitializeNotificationService(notificationService);

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}