using System;
using System.IO;
using System.Text.Json;
using RcloneHelper.Helpers;
using RcloneHelper.Models;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Services;

/// <summary>
/// 应用配置服务实现，统一管理 AppConfig 的读取、保存和修改
/// </summary>
public class ConfigService : IConfigService
{
    private readonly ILoggerService _logger;
    private AppConfig _current;

    public ConfigService(ILoggerService logger)
    {
        _logger = logger;
        _current = LoadFromFile();
    }

    /// <summary>
    /// 当前配置实例
    /// </summary>
    public AppConfig Current => _current;

    /// <summary>
    /// 保存当前配置到文件
    /// </summary>
    public void Save()
    {
        SaveToFile(_current);
    }

    /// <summary>
    /// 修改配置并自动保存
    /// </summary>
    /// <param name="modifier">配置修改操作</param>
    public void Update(Action<AppConfig> modifier)
    {
        modifier(_current);
        Save();
    }

    /// <summary>
    /// 从文件重新加载配置
    /// </summary>
    public void Reload()
    {
        _current = LoadFromFile();
    }

    private AppConfig LoadFromFile()
    {
        try
        {
            var settingsPath = PathUtil.SettingsPath;
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"加载配置文件失败，使用默认配置: {ex.Message}");
        }

        return new AppConfig();
    }

    private void SaveToFile(AppConfig config)
    {
        try
        {
            var settingsPath = PathUtil.SettingsPath;
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            PathUtil.AtomicWriteAllText(settingsPath, json);
        }
        catch (Exception ex)
        {
            _logger.Error($"保存配置文件失败: {ex.Message}");
        }
    }
}