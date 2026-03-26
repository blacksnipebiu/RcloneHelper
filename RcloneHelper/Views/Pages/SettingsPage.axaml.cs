using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RcloneHelper.Core.Pages;

namespace RcloneHelper.Views.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        
        var button = this.FindControl<Button>("SelectRclonePathButton");
        if (button != null)
        {
            button.Click += OnSelectRclonePathButtonClick;
        }
    }
    
    private async void OnSelectRclonePathButtonClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = this.DataContext as SettingsPageViewModel;
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
            viewModel.RclonePath = files[0].Path.LocalPath;
        }
    }
}
