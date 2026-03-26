using CommunityToolkit.Mvvm.ComponentModel;

namespace RcloneHelper.Models;

/// <summary>
/// 依赖安装状态
/// </summary>
public enum DependencyStatus
{
    Unknown,
    Installed,
    NotInstalled
}

/// <summary>
/// 系统依赖项
/// </summary>
public partial class SystemDependency : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private DependencyStatus _status = DependencyStatus.Unknown;

    [ObservableProperty]
    private string _installUrl = "";

    [ObservableProperty]
    private string _icon = "";

    public bool IsInstalled => Status == DependencyStatus.Installed;
    public bool NeedsInstallation => Status == DependencyStatus.NotInstalled;
}
