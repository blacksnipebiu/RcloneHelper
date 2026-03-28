using Avalonia;
using System;
using RcloneHelper.Services;

namespace RcloneHelper;

class Program
{
    // 应用唯一标识符 GUID
    private const string AppGuid = "A1B2C3D4-E5F6-7890-ABCD-EF1234567890";

    // 单实例服务
    private static SingleInstanceService? _singleInstanceService;

    [STAThread]
    public static void Main(string[] args)
    {
        // 创建单实例服务
        _singleInstanceService = new SingleInstanceService();

        // 尝试启动，检查是否为第一个实例
        if (!_singleInstanceService.TryStart())
        {
            // 不是第一个实例，发送参数给已有实例并退出
            if (args.Length > 0)
            {
                _singleInstanceService.SendArgumentsToFirstInstance(args);
            }
            else
            {
                // 没有参数，发送空参数以激活窗口
                _singleInstanceService.SendArgumentsToFirstInstance([]);
            }

            _singleInstanceService.Dispose();
            return;
        }

        // 是第一个实例，正常运行应用
        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _singleInstanceService.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// 获取单实例服务（供 App.axaml.cs 使用）
    /// </summary>
    public static SingleInstanceService? GetSingleInstanceService() => _singleInstanceService;
}