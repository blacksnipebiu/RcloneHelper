using Avalonia.Controls;
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
    }
}
