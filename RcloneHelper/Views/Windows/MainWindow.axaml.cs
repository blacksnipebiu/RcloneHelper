using System;
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
    #region 字段

    private StreamGeometry? _sunGeometry;
    private StreamGeometry? _moonGeometry;
    private TrayIcon? _trayIcon;
    private TaskCompletionSource<bool>? _dialogTcs;

    #endregion

    #region 属性

    public bool IsAutoStartLaunch { get; }

    #endregion

    #region 构造函数

    // 无参构造函数供 Avalonia 设计器使用
    public MainWindow()
    {
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        IDialogService dialogService)
    {
        InitializeComponent();

        IsAutoStartLaunch = Environment.GetCommandLineArgs().Contains("--autostart");
        DataContext = viewModel;

        InitializeIcons();
        InitializeDialog();
        InitializeTheme(viewModel);
        InitializeTrayIcon();

        // 阻止窗口关闭，改为最小化到托盘
        Closing += (_, e) =>
        {
            e.Cancel = true;
            HideToTray();
        };
    }

    #endregion

    #region 公共方法

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

    /// <summary>
    /// 将窗口带到前台（用于托盘点击和新实例激活）
    /// </summary>
    public void BringToFront()
    {
        ShowInTaskbar = true;
        Show();

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Focus();
    }

    #endregion

    #region 初始化方法

    private void InitializeIcons()
    {
        _sunGeometry = Application.Current?.FindResource("IconSun") as StreamGeometry;
        _moonGeometry = Application.Current?.FindResource("IconMoon") as StreamGeometry;
    }

    private void InitializeDialog()
    {
        DialogConfirmButton.Click += (_, _) => HideDialog(true);
        DialogCancelButton.Click += (_, _) => HideDialog(false);
    }

    private void InitializeTheme(MainWindowViewModel viewModel)
    {
        UpdateThemeIcon(viewModel.IsDarkMode);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsDarkMode))
            {
                ThemeService.ApplyTheme(viewModel.IsDarkMode);
                UpdateThemeIcon(viewModel.IsDarkMode);
            }
        };
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
        showItem.Click += (_, _) => BringToFront();
        menu.Add(showItem);

        var exitItem = new NativeMenuItem("退出");
        exitItem.Click += (_, _) =>
        {
            _trayIcon?.Dispose();
            Environment.Exit(0);
        };
        menu.Add(exitItem);

        _trayIcon.Menu = menu;
        _trayIcon.Clicked += (_, _) => BringToFront();
    }

    #endregion

    #region 私有方法

    private void HideDialog(bool result)
    {
        _dialogTcs?.TrySetResult(result);
        _dialogTcs = null;
        DialogOverlay.IsVisible = false;
    }

    private void HideToTray()
    {
        WindowState = WindowState.Minimized;
        ShowInTaskbar = false;
        Hide();
    }

    private void UpdateThemeIcon(bool isDarkMode)
    {
        ThemeIcon.Data = isDarkMode ? _sunGeometry : _moonGeometry;
    }

    #endregion

    #region 事件处理

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e) => BeginMoveDrag(e);

    private void MinimizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            if (MaximizeIcon.Child is Path path) path.Data = Application.Current?.FindResource("IconMaximize") as StreamGeometry;
        }
        else
        {
            WindowState = WindowState.Maximized;
            if (MaximizeIcon.Child is Path path) path.Data = Application.Current?.FindResource("IconRestore") as StreamGeometry;
        }
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => HideToTray();

    #endregion
}