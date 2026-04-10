using System.Text.Json.Serialization;

namespace RcloneHelper.Models;

/// <summary>
/// JSON 序列化上下文，提供 AOT 安全的序列化支持
/// </summary>
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(MountConfig))]
[JsonSerializable(typeof(List<MountConfig>))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
