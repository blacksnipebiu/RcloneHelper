using System;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RcloneHelper.Views.Windows;

/// <summary>
/// Toast 通知项，支持进入和退出动画
/// </summary>
public partial class ToastItem : ObservableObject
{
    private readonly Action<ToastItem>? _onExpired;

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private string _time = "";

    [ObservableProperty]
    private string _type = "info";

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private double _opacity = 0;

    [ObservableProperty]
    private double _offsetY = 30;

    [ObservableProperty]
    private IBrush _backgroundColor = new SolidColorBrush(Colors.DodgerBlue);

    public ToastItem(string message, string type, Action<ToastItem>? onExpired = null)
    {
        _message = message;
        _type = type;
        _onExpired = onExpired;
        _time = DateTime.Now.ToString("HH:mm:ss");

        // 根据类型设置背景色
        BackgroundColor = type.ToLower() switch
        {
            "success" => new SolidColorBrush(Color.FromRgb(46, 160, 67)),  // 绿色
            "error" => new SolidColorBrush(Color.FromRgb(248, 81, 73)),    // 红色
            "warning" => new SolidColorBrush(Color.FromRgb(210, 153, 34)), // 橙色
            _ => new SolidColorBrush(Color.FromRgb(56, 139, 253))         // 蓝色
        };
    }

    /// <summary>
    /// 开始计时器（包含动画）
    /// </summary>
    public async void StartTimer(int durationMs)
    {
        // 播放入场动画
        await PlayEnterAnimationAsync();

        // 等待指定时间
        await Task.Delay(durationMs);

        // 播放退出动画
        await PlayExitAnimationAsync();

        // 移除
        IsVisible = false;
        _onExpired?.Invoke(this);
    }

    private async Task PlayEnterAnimationAsync()
    {
        var duration = 250;
        var steps = 15;
        var delay = duration / steps;

        for (int i = 0; i <= steps; i++)
        {
            var progress = (double)i / steps;
            var eased = EaseOutCubic(progress);

            Opacity = eased;
            OffsetY = 30 * (1 - eased);

            await Task.Delay(delay);
        }
    }

    private async Task PlayExitAnimationAsync()
    {
        var duration = 200;
        var steps = 10;
        var delay = duration / steps;

        for (int i = 0; i <= steps; i++)
        {
            var progress = (double)i / steps;
            var eased = EaseInCubic(progress);

            Opacity = 1 - eased;
            OffsetY = 20 * eased;  // 往下走

            await Task.Delay(delay);
        }
    }

    private static double EaseOutCubic(double t)
    {
        return 1 - Math.Pow(1 - t, 3);
    }

    private static double EaseInCubic(double t)
    {
        return t * t * t;
    }
}