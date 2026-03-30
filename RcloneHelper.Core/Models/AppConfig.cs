namespace RcloneHelper.Models;

/// <summary>
    /// 应用程序配置
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 启动时自动挂载所有存储
        /// </summary>
        public bool AutoMountOnStart { get; set; } = false;

        /// <summary>
        /// 是否使用深色模式
        /// </summary>
        public bool IsDarkMode { get; set; } = true;

    /// <summary>
    /// 开机启动时是否静默启动（最小化到托盘）
    /// </summary>
    public bool StartSilently { get; set; } = false;

    /// <summary>
    /// 是否启用代理
    /// </summary>
    public bool ProxyEnabled { get; set; } = false;

    /// <summary>
    /// 代理协议 (http, https, socks5)
    /// </summary>
    public string ProxyProtocol { get; set; } = "http";

    /// <summary>
    /// 代理服务器地址 (如 127.0.0.1)
    /// </summary>
    public string ProxyHost { get; set; } = "";

    /// <summary>
    /// 代理服务器端口 (如 7890)
    /// </summary>
    public int ProxyPort { get; set; } = 7890;

    /// <summary>
    /// 获取完整的代理 URL
    /// </summary>
    public string GetProxyUrl()
    {
        if (!ProxyEnabled || string.IsNullOrWhiteSpace(ProxyHost))
            return "";

        return $"{ProxyProtocol}://{ProxyHost}:{ProxyPort}";
    }
}
