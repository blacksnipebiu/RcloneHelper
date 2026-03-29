using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RcloneHelper.Converters;

/// <summary>
/// 将 Toast 类型字符串转换为背景颜色
/// </summary>
public class ToastTypeToBrushConverter : IValueConverter
{
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.FromRgb(46, 160, 67));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(248, 81, 73));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.FromRgb(210, 153, 34));
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.FromRgb(56, 139, 253));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string type)
        {
            return type.ToLower() switch
            {
                "success" => SuccessBrush,
                "error" => ErrorBrush,
                "warning" => WarningBrush,
                _ => InfoBrush
            };
        }
        return InfoBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}