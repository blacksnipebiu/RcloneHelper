using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using RcloneHelper.Core.Windows;
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
    private readonly INotificationService _notificationService;
    private readonly IConfigService _configService;
    private TrayIcon? _trayIcon;

    public bool IsAutoStartLaunch { get; }
    public ObservableCollection<ToastItem> Toasts { get; } = new();

    public MainWindow(
        MainWindowViewModel viewModel,
        INotificationService notificationService,
        IDialogService dialogService,
        IConfigService configService)
    {
        InitializeComponent();

        IsAutoStartLaunch = Environment.GetCommandLineArgs().Contains("--autostart");
        DataContext = viewModel;

        _notificationService = notificationService;
        _configService = configService;
        _notificationService.NotificationRequested += (msg, type, duration) =>
            Dispatcher.UIThread.Post(() => ShowToast(msg, type, duration));

        _maximizeGeometry = Application.Current?.FindResource("IconMaximize") as StreamGeometry;
        _restoreGeometry = Application.Current?.FindResource("IconRestore") as StreamGeometry;
        _sunGeometry = Application.Current?.FindResource("IconSun") as StreamGeometry;
        _moonGeometry = Application.Current?.FindResource("IconMoon") as StreamGeometry;

        ToastList.ItemsSource = Toasts;

        // 初始化对话框按钮
        DialogConfirmButton.Click += (_, _) => HideDialog(true);
        DialogCancelButton.Click += (_, _) => HideDialog(false);

        var config = _configService.Current;
        _isDarkMode = config.IsDarkMode;
        ThemeService.ApplyTheme(_isDarkMode);
        UpdateThemeIcon();

        InitializeTrayIcon();

        if (IsAutoStartLaunch && config.StartSilently)
        {
            ShowInTaskbar = false;
            WindowState = WindowState.Minimized;
        }
    }

    /// <summary>
    /// 显示确认对话框
    /// </summary>
    public Task<bool> ShowConfirmationAsync(string message)
    {
        var tcs = new TaskCompletionSource<bool>();
        Dispatcher.UIThread.Post(() =>
        {
            DialogMessage.Text = message;
            DialogOverlay.IsVisible = true;
            _dialogTcs = tcs;
        });
        return tcs.Task;
    }

    private TaskCompletionSource<bool>? _dialogTcs;

    private void HideDialog(bool result)
    {
        _dialogTcs?.TrySetResult(result);
        _dialogTcs = null;
        DialogOverlay.IsVisible = false;
    }

    public void RestoreNormalMode()
    {
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
    }

    private void InitializeTrayIcon()
    {
        var iconStream = AssetLoader.Open(new Uri("avares://RcloneHelper/Assets/app.ico"));
        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(iconStream),
            ToolTipText = "Rclone Helper",
            IsVisible = true
        };

        var menu = new NativeMenu();
        var showItem = new NativeMenuItem("显示窗口");
        showItem.Click += (_, _) => ShowWindow();
        menu.Add(showItem);

        var exitItem = new NativeMenuItem("退出");
        exitItem.Click += (_, _) => { _trayIcon?.Dispose(); System.Environment.Exit(0); };
        menu.Add(exitItem);

        _trayIcon.Menu = menu;
        _trayIcon.Clicked += (_, _) => ShowWindow();
    }

    private void ShowWindow()
    {
        RestoreNormalMode();
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ShowToast(string message, NotificationType type, int duration)
    {
        var toast = new ToastItem(message, type.ToString().ToLower(), t => Dispatcher.UIThread.Post(() => Toasts.Remove(t)));
        Toasts.Insert(0, toast);
        toast.StartTimer(duration);
    }

    private void UpdateThemeIcon()
    {
        ThemeIcon.Data = _isDarkMode ? _sunGeometry : _moonGeometry;
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e) => BeginMoveDrag(e);
    private void MinimizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            if (MaximizeIcon.Child is Path path) path.Data = _maximizeGeometry;
        }
        else
        {
            WindowState = WindowState.Maximized;
            if (MaximizeIcon.Child is Path path) path.Data = _restoreGeometry;
        }
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void ThemeToggle_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isDarkMode = !_isDarkMode;
        ThemeService.ApplyTheme(_isDarkMode);
        UpdateThemeIcon();
        _configService.Update(c => c.IsDarkMode = _isDarkMode);
    }
}