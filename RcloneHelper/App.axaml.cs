using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RcloneHelper.Core.Windows;
using RcloneHelper.Helpers;
using RcloneHelper.Models;
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
        services.AddApplicationServices();

        // 注册对话框服务（必须在 BuildServiceProvider 之前）
        services.AddSingleton<IDialogService, DialogService>();

        // 注册 MainWindow（支持构造函数注入）
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        // 记录启动日志
        _logger = _serviceProvider.GetRequiredService<ILoggerService>();
        
        var args = Environment.GetCommandLineArgs();
        _logger.Info($"RcloneHelper 启动, 参数: {string.Join(" ", args)}");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 记录主窗口初始化
            _logger?.Info("初始化主窗口");

            // 从 DI 容器获取 MainWindow（所有依赖已通过构造函数注入）
            var mainWindow = _serviceProvider!.GetRequiredService<MainWindow>();

            desktop.MainWindow = mainWindow;

            // 订阅关闭事件
            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _logger?.Info("RcloneHelper 关闭");
    }
}