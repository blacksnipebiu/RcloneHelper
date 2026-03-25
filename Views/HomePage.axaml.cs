using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RcloneHelper.Models;
using RcloneHelper.ViewModels;

namespace RcloneHelper.Views;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
    }

    private void OnMountPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is MountInfo mount)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.MountItemCommand.Execute(mount);
        }
    }

    private void OnUnmountPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is MountInfo mount)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.UnmountItemCommand.Execute(mount);
        }
    }

    private void OnEditPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is MountInfo mount)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.EditItemCommand.Execute(mount);
        }
    }

    private void OnDeletePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is MountInfo mount)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.DeleteItemCommand.Execute(mount);
        }
    }
}
