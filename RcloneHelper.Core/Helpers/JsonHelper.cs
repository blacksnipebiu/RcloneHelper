using System;
using System.IO;
using System.Text.Json;
using RcloneHelper.Services.Abstractions;

public static class JsonHelper
{
    private static ILoggerService? _logger;

    /// <summary>
    /// 初始化 JsonHelper 的日志服务
    /// </summary>
    public static void Initialize(ILoggerService logger)
    {
        _logger = logger;
    }

    private static readonly JsonSerializerOptions _defaultOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions _compactOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize<T>(T value, bool indented = true)
    {
        var options = indented ? _defaultOptions : _compactOptions;
        return JsonSerializer.Serialize(value, options);
    }

    public static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, _defaultOptions);
    }

    public static bool TryDeserialize<T>(string json, out T? result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            result = JsonSerializer.Deserialize<T>(json, _defaultOptions);
            return result != null;
        }
        catch (Exception ex)
        {
            _logger?.Warning($"JSON 反序列化失败: {ex.Message}");
            return false;
        }
    }

    public static void WriteToFile<T>(string filePath, T value, bool indented = true)
    {
        var json = Serialize(value, indented);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(filePath, json);
    }

    public static T? ReadFromFile<T>(string filePath)
    {
        if (!File.Exists(filePath))
            return default;

        var json = File.ReadAllText(filePath);
        return Deserialize<T>(json);
    }

    public static bool TryReadFromFile<T>(string filePath, out T? result)
    {
        result = default;

        if (!File.Exists(filePath))
            return false;

        try
        {
            var json = File.ReadAllText(filePath);
            return TryDeserialize(json, out result);
        }
        catch (Exception ex)
        {
            _logger?.Warning($"读取 JSON 文件失败 ({filePath}): {ex.Message}");
            return false;
        }
    }
}