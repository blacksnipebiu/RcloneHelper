using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Diagnostics;

namespace RcloneHelper.Models;

public class MountConfig
{
    public string Name { get; set; } = "";
    public string RemotePath { get; set; } = "";
    public string LocalDrive { get; set; } = "Z:";
    public string Type { get; set; } = "webdav";
    public string Url { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string Vendor { get; set; } = "other";
    public bool AutoMountOnStart { get; set; } = true;
    public bool UseNetworkMode { get; set; } = false;
    
    // 新增：URL 组件字段（用于 WebDAV 等类型）
    public bool UseHttps { get; set; } = true;
    public string Host { get; set; } = "";
    public int Port { get; set; } = 443;
    public string Path { get; set; } = "";
}

public partial class MountInfo : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _remotePath = "";

    [ObservableProperty]
    private string _localDrive = "Z:";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWebDavType))]
    private string _type = "webdav";

    [ObservableProperty]
    private string _url = "";

    [ObservableProperty]
    private string _user = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _vendor = "other";

    [ObservableProperty]
    private bool _isMounted;

    [ObservableProperty]
    private string _status = "未挂载";

    [ObservableProperty]
    private bool _autoMountOnStart = true;
    
    [ObservableProperty]
    private bool _useNetworkMode = false;
    
    // 新增：URL 组件字段（用于 WebDAV 等类型）
    [ObservableProperty]
    private bool _useHttps = true;
    
    [ObservableProperty]
    private string _host = "";
    
    [ObservableProperty]
    private int _port = 443;
    
    [ObservableProperty]
    private string _path = "";
    
    /// <summary>
    /// 计算属性：是否为 WebDAV 类型（用于 UI 显示控制）
    /// </summary>
    public bool IsWebDavType => Type == "webdav";
    
    /// <summary>
    /// 计算完整 URL
    /// </summary>
    public string ComputedUrl
    {
        get
        {
            if (!IsWebDavType || string.IsNullOrWhiteSpace(Host))
                return Url;
            
            var protocol = UseHttps ? "https" : "http";
            var portPart = (UseHttps && Port == 443) || (!UseHttps && Port == 80) ? "" : $":{Port}";
            var pathPart = string.IsNullOrWhiteSpace(Path) ? "" : (Path.StartsWith("/") ? Path : $"/{Path}");
            return $"{protocol}://{Host}{portPart}{pathPart}";
        }
    }

    public Process? MountProcess { get; set; }

    public static MountInfo FromConfig(MountConfig config)
    {
        return new MountInfo
        {
            Name = config.Name,
            RemotePath = config.RemotePath,
            LocalDrive = config.LocalDrive,
            Type = config.Type,
            Url = config.Url,
            User = config.User,
            Password = config.Password,
            Vendor = config.Vendor,
            AutoMountOnStart = config.AutoMountOnStart,
            UseNetworkMode = config.UseNetworkMode,
            UseHttps = config.UseHttps,
            Host = config.Host,
            Port = config.Port,
            Path = config.Path,
            IsMounted = false,
            Status = "未挂载"
        };
    }

    public MountConfig ToConfig()
    {
        return new MountConfig
        {
            Name = Name,
            RemotePath = RemotePath,
            LocalDrive = LocalDrive,
            Type = Type,
            Url = IsWebDavType ? ComputedUrl : Url,
            User = User,
            Password = Password,
            Vendor = Vendor,
            AutoMountOnStart = AutoMountOnStart,
            UseNetworkMode = UseNetworkMode,
            UseHttps = UseHttps,
            Host = Host,
            Port = Port,
            Path = Path
        };
    }
}
