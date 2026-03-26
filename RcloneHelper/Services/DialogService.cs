using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using RcloneHelper.Services.Abstractions;
using RcloneHelper.Views.Windows;

namespace RcloneHelper.Services;

/// <summary>
/// 对话框服务实现
/// </summary>
public class DialogService : IDialogService
{
    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        var dialogContent = new ConfirmationDialog
        {
            DialogTitle = title,
            DialogMessage = message,
            ResultTcs = tcs
        };

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            CanResize = false,
            Content = dialogContent
        };

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            await dialog.ShowDialog(desktop.MainWindow!);
        }

        return await tcs.Task;
    }
}
