using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace RcloneHelper.Services;

public static class ThemeService
{
    public static void ApplyTheme(bool isDark)
    {
        if (Application.Current == null) return;

        // 设置 FluentTheme 主题
        Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;

        // 更新自定义资源
        var resources = Application.Current.Resources;
        
        if (isDark)
        {
            // 深色主题
            UpdateResource(resources, "BackgroundBrush", "#1e1e1e");
            UpdateResource(resources, "SurfaceBrush", "#2d2d2d");
            UpdateResource(resources, "SurfaceHighBrush", "#252525");
            UpdateResource(resources, "SurfaceLowBrush", "#3d3d3d");
            UpdateResource(resources, "ForegroundBrush", "#ffffff");
            UpdateResource(resources, "ForegroundSecondaryBrush", "#cccccc");
            UpdateResource(resources, "ForegroundMutedBrush", "#999999");
            UpdateResource(resources, "ForegroundDisabledBrush", "#666666");
            UpdateResource(resources, "BorderBrush", "#3d3d3d");
            UpdateResource(resources, "CardBrush", "#2d2d2d");
            UpdateResource(resources, "TitleBarBackgroundBrush", "#252525");
            UpdateResource(resources, "TitleBarForegroundBrush", "#cccccc");
            UpdateResource(resources, "NavBarBackgroundBrush", "#2d2d2d");
            UpdateResource(resources, "NavBarItemHoverBrush", "#3d3d3d");
            UpdateResource(resources, "NavBarItemSelectedBrush", "#4CAF50");
            UpdateResource(resources, "ContentBackgroundBrush", "#1e1e1e");
            UpdateResource(resources, "ListItemHoverBrush", "#3d3d3d");
            UpdateResource(resources, "ListItemSelectedBrush", "#4CAF50");
            UpdateResource(resources, "ListItemSelectedForegroundBrush", "#ffffff");
        }
        else
        {
            // 浅色主题
            UpdateResource(resources, "BackgroundBrush", "#f5f5f5");
            UpdateResource(resources, "SurfaceBrush", "#ffffff");
            UpdateResource(resources, "SurfaceHighBrush", "#fafafa");
            UpdateResource(resources, "SurfaceLowBrush", "#eeeeee");
            UpdateResource(resources, "ForegroundBrush", "#1a1a1a");
            UpdateResource(resources, "ForegroundSecondaryBrush", "#333333");
            UpdateResource(resources, "ForegroundMutedBrush", "#666666");
            UpdateResource(resources, "ForegroundDisabledBrush", "#999999");
            UpdateResource(resources, "BorderBrush", "#e0e0e0");
            UpdateResource(resources, "CardBrush", "#ffffff");
            UpdateResource(resources, "TitleBarBackgroundBrush", "#ffffff");
            UpdateResource(resources, "TitleBarForegroundBrush", "#333333");
            UpdateResource(resources, "NavBarBackgroundBrush", "#fafafa");
            UpdateResource(resources, "NavBarItemHoverBrush", "#e8f5e9");
            UpdateResource(resources, "NavBarItemSelectedBrush", "#4CAF50");
            UpdateResource(resources, "ContentBackgroundBrush", "#f5f5f5");
            UpdateResource(resources, "ListItemHoverBrush", "#f0f0f0");
            UpdateResource(resources, "ListItemSelectedBrush", "#e8f5e9");
            UpdateResource(resources, "ListItemSelectedForegroundBrush", "#1a1a1a");
        }
    }

    private static void UpdateResource(IResourceDictionary resources, string key, string colorHex)
    {
        resources[key] = new SolidColorBrush(Color.Parse(colorHex));
    }
}