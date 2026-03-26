using System.Collections.Generic;
using System.Text.Json.Serialization;
using RcloneHelper.Models;

namespace RcloneHelper.Helpers;

/// <summary>
/// JSON 序列化上下文，用于裁剪友好的 JSON 序列化
/// </summary>
[JsonSerializable(typeof(List<MountConfig>))]
[JsonSerializable(typeof(MountConfig))]
[JsonSerializable(typeof(AppConfig))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class AppJsonContext : JsonSerializerContext
{
}