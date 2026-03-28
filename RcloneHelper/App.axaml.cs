using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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

        // 注册对话框服务（必须在 AddApplicationServices 之前，因为 ViewModels 需要它）
        services.AddSingleton<IDialogService, DialogService>();

        services.AddApplicationServices();

        // 注册 MainWindow（支持构造函数注入）
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        // 记录启动日志
        _logger = _serviceProvider.GetRequiredService<ILoggerService>();

        var args = Environment.GetCommandLineArgs();
        _logger.Info($"RcloneHelper 启动, 参数: {string.Join(" ", args)}");

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

    /// <summary>
    /// 处理来自其他实例的参数
    /// </summary>
    private void OnArgumentsReceived(string[] args)
    {
        _logger?.Info($"接收到来自其他实例的参数: {string.Join(" ", args)}");

        // 在 UI 线程上激活窗口
        Dispatcher.UIThread.Post(() =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow != null)
                {
                    BringWindowToFront(mainWindow);
                }
            }
        });
    }

    /// <summary>
    /// 将窗口带到前台
    /// </summary>
    private void BringWindowToFront(Window window)
    {
        // Windows 平台优先支持
        if (OperatingSystem.IsWindows())
        {
            BringWindowToFrontWindows(window);
        }
        else
        {
            // 其他平台使用 Avalonia 内置方法
            BringWindowToFrontGeneric(window);
        }
    }

    /// <summary>
    /// Windows 平台窗口激活（使用原生 API）
    /// </summary>
    private void BringWindowToFrontWindows(Window window)
    {
        try
        {
            // 先使用 Avalonia 方法
            BringWindowToFrontGeneric(window);

            // 如果窗口最小化，恢复窗口
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            // 获取窗口句柄
            var handle = window.TryGetPlatformHandle()?.Handle;
            if (handle != null)
            {
                // 使用 Windows API 强制激活窗口
                // SetForegroundWindow 需要先调用 AllowSetForegroundWindow
                var hwnd = (IntPtr)handle.Value;
                
                // 模拟按键输入以获取前台权限（Windows 安全机制）
                // 这是一种常用的技巧来绕过 Windows 的前台窗口限制
                UnsafeNativeMethods.SetForegroundWindow(hwnd);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"Windows 窗口激活失败: {ex.Message}");
            // 回退到通用方法
            BringWindowToFrontGeneric(window);
        }
    }

    /// <summary>
    /// 通用窗口激活方法（跨平台）
    /// </summary>
    private void BringWindowToFrontGeneric(Window window)
    {
        // 显示在任务栏
        window.ShowInTaskbar = true;
        
        // 显示窗口
        window.Show();
        
        // 如果窗口最小化，恢复窗口
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }
        
        // 激活窗口
        window.Activate();
        
        // 尝试聚焦窗口
        window.Focus();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _logger?.Info("RcloneHelper 关闭");
        
        // 取消订阅单实例服务事件
        var singleInstanceService = Program.GetSingleInstanceService();
        if (singleInstanceService != null)
        {
            singleInstanceService.ArgumentsReceived -= OnArgumentsReceived;
        }
    }

    /// <summary>
    /// Windows 原生 API 方法
    /// </summary>
    private static class UnsafeNativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}