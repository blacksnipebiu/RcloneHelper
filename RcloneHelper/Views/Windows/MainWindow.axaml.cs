using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using RcloneHelper.Helpers;
using RcloneHelper.Models;
using RcloneHelper.Services;
using RcloneHelper.Services.Abstractions;
using Path = Avalonia.Controls.Shapes.Path;

namespace RcloneHelper.Views.Windows;

public partial class MainWindow : Window
{
    private StreamGeometry? _maximizeGeometry;
    private StreamGeometry? _restoreGeometry;
    private StreamGeometry? _sunGeometry;
    private StreamGeometry? _moonGeometry;
    private bool _isDarkMode = true;
    private INotificationService? _notificationService;

    /// <summary>
    /// Toast 列表，支持叠加显示
    /// </summary>
    public ObservableCollection<ToastItem> Toasts { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        _maximizeGeometry = Application.Current?.FindResource("IconMaximize") as StreamGeometry;
        _restoreGeometry = Application.Current?.FindResource("IconRestore") as StreamGeometry;
        _sunGeometry = Application.Current?.FindResource("IconSun") as StreamGeometry;
        _moonGeometry = Application.Current?.FindResource("IconMoon") as StreamGeometry;

        // 绑定 Toast 列表
        ToastList.ItemsSource = Toasts;

        // 加载主题设置
        LoadThemeSetting();
    }

    /// <summary>
    /// 初始化服务（由 App.axaml.cs 调用）
    /// </summary>
    public void InitializeServices(INotificationService notificationService, IDialogService dialogService)
    {
        _notificationService = notificationService;
        _notificationService.NotificationRequested += OnNotificationRequested;
    }

    private void OnNotificationRequested(string message, NotificationType type, int duration)
    {
        Dispatcher.UIThread.Post(() => ShowToast(message, type, duration));
    }

    private void ShowToast(string message, NotificationType type, int duration)
    {
        // 添加新的 Toast
        var toast = new ToastItem(message, type.ToString().ToLower(), OnToastExpired);
        Toasts.Insert(0, toast); // 插入到顶部，实现从下往上堆叠

        // 启动计时器
        toast.StartTimer(duration);
    }

    private void OnToastExpired(ToastItem toast)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Toasts.Remove(toast);
        });
    }

    private void LoadThemeSetting()
    {
        try
        {
            var settingsPath = PathUtil.SettingsPath;
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig);
                if (settings != null)
                {
                    _isDarkMode = settings.IsDarkMode;
                }
            }
        }
        catch { }

        // 应用加载的主题
        ThemeService.ApplyTheme(_isDarkMode);
        UpdateThemeIcon();
    }

    private void SaveThemeSetting()
    {
        try
        {
            var settingsPath = PathUtil.SettingsPath;

            AppConfig settings;
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig) ?? new AppConfig();
            }
            else
            {
                settings = new AppConfig();
            }

            settings.IsDarkMode = _isDarkMode;
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, AppJsonContext.Default.AppConfig));
        }
        catch { }
    }

    private void UpdateThemeIcon()
    {
        if (ThemeIcon != null)
        {
            ThemeIcon.Data = _isDarkMode ? _sunGeometry : _moonGeometry;
        }
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void MinimizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            if (MaximizeIcon?.Child is Path path)
                path.Data = _maximizeGeometry;
        }
        else
        {
            WindowState = WindowState.Maximized;
            if (MaximizeIcon?.Child is Path path)
                path.Data = _restoreGeometry;
        }
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void ThemeToggle_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isDarkMode = !_isDarkMode;
        ThemeService.ApplyTheme(_isDarkMode);
        UpdateThemeIcon();
        SaveThemeSetting();
    }
}