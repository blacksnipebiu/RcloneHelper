using RcloneHelper.Models;

namespace RcloneHelper.Services.Abstractions;

/// <summary>
/// 应用配置服务接口，统一管理 AppConfig 的读取、保存和修改
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// 当前配置实例（修改后需调用 Save 或 Update 方法保存）
    /// </summary>
    AppConfig Current { get; }

    /// <summary>
    /// 保存当前配置到文件
    /// </summary>
    void Save();

    /// <summary>
    /// 修改配置并自动保存
    /// </summary>
    /// <param name="modifier">配置修改操作</param>
    void Update(Action<AppConfig> modifier);

    /// <summary>
    /// 从文件重新加载配置
    /// </summary>
    void Reload();
}