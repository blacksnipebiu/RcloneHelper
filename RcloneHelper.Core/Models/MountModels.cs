using CommunityToolkit.Mvvm.ComponentModel;
using RcloneHelper.Helpers;
using System;
using System.Diagnostics;
using System.Threading;

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

    // URL 组件字段
    public bool UseHttps { get; set; } = true;
    public string Host { get; set; } = "";
    public int Port { get; set; } = 443;
    public string Path { get; set; } = "";

    // S3 专用字段
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
    public string Region { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string S3Provider { get; set; } = "AWS";
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
    [NotifyPropertyChangedFor(nameof(IsFtpType))]
    [NotifyPropertyChangedFor(nameof(IsSftpType))]
    [NotifyPropertyChangedFor(nameof(IsS3Type))]
    [NotifyPropertyChangedFor(nameof(UseUrlInput))]
    private string _type = "webdav";

    /// <summary>
    /// 类型变更时自动更新默认端口
    /// </summary>
    partial void OnTypeChanged(string value)
    {
        // 根据类型设置默认端口
        Port = value switch
        {
            "webdav" => UseHttps ? 443 : 80,
            "ftp" => 21,
            "sftp" => 22,
            "smb" => 445,
            _ => 443
        };
    }

    [ObservableProperty]
    private string _url = "";

    [ObservableProperty]
    private string _user = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _vendor = "other";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanMount))]
    [NotifyPropertyChangedFor(nameof(CanUnmount))]
    private bool _isMounted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanMount))]
    [NotifyPropertyChangedFor(nameof(CanUnmount))]
    private bool _isMounting;

    [ObservableProperty]
    private string _status = "未挂载";

    /// <summary>
    /// 用于取消挂载操作的 CancellationTokenSource
    /// </summary>
    public CancellationTokenSource? MountCancellationTokenSource { get; set; }

    /// <summary>
    /// 计算属性：是否可以挂载（未挂载且未在挂载中）
    /// </summary>
    public bool CanMount => !IsMounted && !IsMounting;

    /// <summary>
    /// 计算属性：是否可以卸载（已挂载且未在挂载中）
    /// </summary>
    public bool CanUnmount => IsMounted && !IsMounting;

    [ObservableProperty]
    private bool _autoMountOnStart = true;

    [ObservableProperty]
    private bool _useNetworkMode = false;

    // URL 组件字段
    [ObservableProperty]
    private bool _useHttps = true;

    /// <summary>
    /// 协议类型（用于 UI 绑定）
    /// </summary>
    public string Protocol
    {
        get => UseHttps ? "HTTPS" : "HTTP";
        set
        {
            if (value == "HTTPS" && !UseHttps)
            {
                UseHttps = true;
                // WebDAV 切换到 HTTPS 时更新端口
                if (Type == "webdav" && Port == 80)
                {
                    Port = 443;
                }
                OnPropertyChanged(nameof(Protocol));
                OnPropertyChanged(nameof(ComputedUrl));
            }
            else if (value == "HTTP" && UseHttps)
            {
                UseHttps = false;
                // WebDAV 切换到 HTTP 时更新端口
                if (Type == "webdav" && Port == 443)
                {
                    Port = 80;
                }
                OnPropertyChanged(nameof(Protocol));
                OnPropertyChanged(nameof(ComputedUrl));
            }
        }
    }

    [ObservableProperty]
    private string _host = "";

    [ObservableProperty]
    private int _port = 443;

    [ObservableProperty]
    private string _path = "";

    // S3 专用字段
    [ObservableProperty]
    private string _accessKeyId = "";

    [ObservableProperty]
    private string _secretAccessKey = "";

    [ObservableProperty]
    private string _region = "";

    [ObservableProperty]
    private string _endpoint = "";

    [ObservableProperty]
    private string _s3Provider = "AWS";

    /// <summary>
    /// 计算属性：是否为 WebDAV 类型
    /// </summary>
    public bool IsWebDavType => Type == "webdav";

    /// <summary>
    /// 计算属性：是否为 FTP 类型
    /// </summary>
    public bool IsFtpType => Type == "ftp";

    /// <summary>
    /// 计算属性：是否为 SFTP 类型
    /// </summary>
    public bool IsSftpType => Type == "sftp";

    /// <summary>
    /// 计算属性：是否为 S3 类型
    /// </summary>
    public bool IsS3Type => Type == "s3";

    /// <summary>
    /// 计算属性：是否为网络存储类型（WebDAV/FTP/SFTP）
    /// </summary>
    public bool IsNetworkStorageType => IsWebDavType || IsFtpType || IsSftpType;

    /// <summary>
    /// 计算属性：是否使用 URL 输入（非 WebDAV/FTP/SFTP 类型）
    /// </summary>
    public bool UseUrlInput => !IsNetworkStorageType;

    /// <summary>
    /// 获取默认端口
    /// </summary>
    public int DefaultPort => Type switch
    {
        "webdav" => UseHttps ? 443 : 80,
        "ftp" => 21,
        "sftp" => 22,
        "smb" => 445,
        _ => 443
    };

    /// <summary>
    /// 计算完整 URL
    /// </summary>
    public string ComputedUrl
    {
        get
        {
            if (IsWebDavType && !string.IsNullOrWhiteSpace(Host))
            {
                var protocol = UseHttps ? "https" : "http";
                var portPart = (UseHttps && Port == 443) || (!UseHttps && Port == 80) ? "" : $":{Port}";
                var pathPart = string.IsNullOrWhiteSpace(Path) ? "" : (Path.StartsWith("/") ? Path : $"/{Path}");
                return $"{protocol}://{Host}{portPart}{pathPart}";
            }

            if ((IsFtpType || IsSftpType) && !string.IsNullOrWhiteSpace(Host))
            {
                var protocol = IsFtpType ? "ftp" : "sftp";
                var portPart = Port == DefaultPort ? "" : $":{Port}";
                var pathPart = string.IsNullOrWhiteSpace(Path) ? "" : (Path.StartsWith("/") ? Path : $"/{Path}");
                return $"{protocol}://{Host}{portPart}{pathPart}";
            }

            return Url;
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
            // 解密密码（兼容未加密的旧数据）
            Password = SecureStorageHelper.Decrypt(config.Password),
            Vendor = config.Vendor,
            AutoMountOnStart = config.AutoMountOnStart,
            UseNetworkMode = config.UseNetworkMode,
            UseHttps = config.UseHttps,
            Host = config.Host,
            Port = config.Port,
            Path = config.Path,
            // S3 字段
            AccessKeyId = config.AccessKeyId,
            SecretAccessKey = SecureStorageHelper.Decrypt(config.SecretAccessKey),
            Region = config.Region,
            Endpoint = config.Endpoint,
            S3Provider = config.S3Provider,
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
            Url = IsWebDavType || IsFtpType || IsSftpType ? ComputedUrl : Url,
            User = User,
            // 加密密码
            Password = SecureStorageHelper.Encrypt(Password),
            Vendor = Vendor,
            AutoMountOnStart = AutoMountOnStart,
            UseNetworkMode = UseNetworkMode,
            UseHttps = UseHttps,
            Host = Host,
            Port = Port,
            Path = Path,
            // S3 字段
            AccessKeyId = AccessKeyId,
            SecretAccessKey = SecureStorageHelper.Encrypt(SecretAccessKey),
            Region = Region,
            Endpoint = Endpoint,
            S3Provider = S3Provider
        };
    }
}
