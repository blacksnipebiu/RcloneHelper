using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace RcloneHelper.Models;

// 用于保存配置的纯数据类（不包含运行时状态）
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

    public Process? MountProcess { get; set; }

    // 从配置创建
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
            IsMounted = false,
            Status = "未挂载"
        };
    }

    // 转换为配置
    public MountConfig ToConfig()
    {
        return new MountConfig
        {
            Name = Name,
            RemotePath = RemotePath,
            LocalDrive = LocalDrive,
            Type = Type,
            Url = Url,
            User = User,
            Password = Password,
            Vendor = Vendor,
            AutoMountOnStart = AutoMountOnStart
        };
    }
}

public class AppSettings
{
    public bool AutoMountOnStart { get; set; }
}
