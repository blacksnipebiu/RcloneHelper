using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace RcloneHelper.Views.Windows;

public partial class ConfirmationDialog : UserControl
{
    public string DialogTitle { get; set; } = "确认";
    public string DialogMessage { get; set; } = "";
    public TaskCompletionSource<bool>? ResultTcs { get; set; }

    public ConfirmationDialog()
    {
        InitializeComponent();

        var confirmButton = this.FindControl<Button>("ConfirmButton");
        var cancelButton = this.FindControl<Button>("CancelButton");

        if (confirmButton != null)
        {
            confirmButton.Click += (s, e) =>
            {
                ResultTcs?.TrySetResult(true);
                CloseDialog();
            };
        }

        if (cancelButton != null)
        {
            cancelButton.Click += (s, e) =>
            {
                ResultTcs?.TrySetResult(false);
                CloseDialog();
            };
        }
    }

    private void CloseDialog()
    {
        if (this.VisualRoot is Window window)
        {
            window.Close();
        }
    }
}
