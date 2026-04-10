using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using RcloneHelper.Helpers;
using RcloneHelper.Services;
using RcloneHelper.Services.Abstractions;
using RcloneHelper.Views.Windows;

namespace RcloneHelper;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private ILoggerService? _logger;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // 配置依赖注入容器
        var services = new ServiceCollection();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddApplicationServices();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        // 记录启动日志
        _logger = _serviceProvider.GetRequiredService<ILoggerService>();
        JsonHelper.Initialize(_logger);
        _logger.Info($"RcloneHelper 启动, 参数: {string.Join(" ", Environment.GetCommandLineArgs())}");

        // 订阅单实例服务的事件，接收来自其他实例的参数
        var singleInstanceService = Program.GetSingleInstanceService();
        if (singleInstanceService != null)
        {
            singleInstanceService.ArgumentsReceived += OnArgumentsReceived;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _logger?.Info("初始化主窗口");

            var mainWindow = _serviceProvider!.GetRequiredService<MainWindow>();

            // 静默启动处理 - 必须在设置 MainWindow 之前处理
            HandleSilentStart(mainWindow);

            desktop.MainWindow = mainWindow;
            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    #region 私有方法

    /// <summary>
    /// 处理静默启动
    /// </summary>
    private void HandleSilentStart(MainWindow mainWindow)
    {
        var isAutoStartLaunch = Environment.GetCommandLineArgs().Contains("--autostart");
        if (!isAutoStartLaunch) return;

        var configService = _serviceProvider!.GetRequiredService<IConfigService>();
        if (configService.Current.StartSilently)
        {
            // 先隐藏窗口防止闪现
            mainWindow.WindowState = WindowState.Minimized;
            mainWindow.ShowInTaskbar = false;

            // 在 UI 线程下一个周期执行隐藏操作
            Dispatcher.UIThread.Post(() =>
            {
                mainWindow.Hide();
            });
        }
    }

    /// <summary>
    /// 处理来自其他实例的参数
    /// </summary>
    private void OnArgumentsReceived(string[] args)
    {
        _logger?.Info($"接收到来自其他实例的参数: {string.Join(" ", args)}");

        Dispatcher.UIThread.Post(() =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow is MainWindow mainWindow)
            {
                mainWindow.BringToFront();
            }
        });
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _logger?.Info("RcloneHelper 关闭");

        var singleInstanceService = Program.GetSingleInstanceService();
        if (singleInstanceService != null)
        {
            singleInstanceService.ArgumentsReceived -= OnArgumentsReceived;
        }
    }

    #endregion
}