using System.Linq;

namespace RcloneHelper.Models;

/// <summary>
    /// 应用程序配置
    /// </summary>
    public class AppConfig
    {
        private const int MinPort = 1;
        private const int MaxPort = 65535;
        private static readonly string[] ValidProtocols = { "http", "https", "socks5" };

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
    private string _proxyProtocol = "http";
    public string ProxyProtocol
    {
        get => _proxyProtocol;
        set
        {
            var normalizedValue = value?.ToLowerInvariant() ?? "";
            _proxyProtocol = ValidProtocols.Contains(normalizedValue) ? normalizedValue : "http";
        }
    }

    /// <summary>
    /// 代理服务器地址 (如 127.0.0.1)
    /// </summary>
    public string ProxyHost { get; set; } = "";

    /// <summary>
    /// 代理服务器端口 (如 7890)
    /// </summary>
    private int _proxyPort = 7890;
    public int ProxyPort
    {
        get => _proxyPort;
        set => _proxyPort = value >= MinPort && value <= MaxPort ? value : 7890;
    }

    /// <summary>
    /// 获取完整的代理 URL
    /// </summary>
    public string GetProxyUrl()
    {
        if (!ProxyEnabled)
            return "";

        if (string.IsNullOrWhiteSpace(ProxyHost))
            return "";

        if (ProxyPort < MinPort || ProxyPort > MaxPort)
            return "";

        return $"{ProxyProtocol}://{ProxyHost.Trim()}:{ProxyPort}";
    }
}
