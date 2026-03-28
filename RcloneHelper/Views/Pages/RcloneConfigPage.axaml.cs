using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RcloneHelper.Core.Pages;

namespace RcloneHelper.Views.Pages;

public partial class RcloneConfigPage : UserControl
{
    public RcloneConfigPage()
    {
        InitializeComponent();

        // 每次页面显示时自动刷新
        this.AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is RcloneConfigPageViewModel vm)
            {
                vm.RefreshConfigCommand.Execute(null);
            }
        };

        // 选择 rclone 路径按钮
        var button = this.FindControl<Button>("SelectRclonePathButton");
        if (button != null)
        {
            button.Click += OnSelectRclonePathButtonClick;
        }
    }

    private async void OnSelectRclonePathButtonClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = this.DataContext as RcloneConfigPageViewModel;
        if (viewModel == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 rclone 可执行文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("rclone 可执行文件") { Patterns = new[] { "*.exe", "rclone" } },
                new FilePickerFileType("所有文件") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count > 0)
        {
            viewModel.CustomRclonePath = files[0].Path.LocalPath;
        }
    }
}
