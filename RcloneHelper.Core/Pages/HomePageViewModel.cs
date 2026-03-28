using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RcloneHelper.Models;
using RcloneHelper.Services;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Core.Pages;

public partial class HomePageViewModel : ObservableObject
{
    private readonly MountService _mountService;
    private readonly INotificationService _notificationService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<MountInfo> _allMounts = new();

    [ObservableProperty]
    private ObservableCollection<MountInfo> _mountedMounts = new();

    [ObservableProperty]
    private ObservableCollection<MountInfo> _unmountedMounts = new();

    [ObservableProperty]
    private MountInfo? _selectedMount;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private MountInfo _editingMount = new();

    // 记录正在编辑的原始挂载对象（用于区分新增和编辑）
    private MountInfo? _editingOriginalMount;

    public HomePageViewModel(MountService mountService, INotificationService notificationService, IDialogService dialogService)
    {
        _mountService = mountService;
        _notificationService = notificationService;
        _dialogService = dialogService;

        // 订阅挂载列表变化事件
        _mountService.MountsChanged += OnMountsChanged;

        LoadMountsFromService();
    }

    private void OnMountsChanged()
    {
        // 当 MountService 的挂载列表变化时（如导入配置），刷新 UI
        LoadMountsFromService();
    }

    /// <summary>
    /// 自动挂载所有配置了"启动时自动挂载"的存储
    /// </summary>
    public async Task AutoMountConfiguredAsync()
    {
        var toMount = AllMounts.Where(m => m.AutoMountOnStart && !m.IsMounted).ToList();
        foreach (var mount in toMount)
        {
            await MountAsync(mount);
        }
    }

    /// <summary>
    /// 刷新挂载状态，同步正在运行的 rclone 进程状态
    /// </summary>
    public void RefreshMountStatus()
    {
        _mountService.RefreshMountStatus();
        UpdateMountCollections();
    }

    private void LoadMountsFromService()
    {
        AllMounts.Clear();
        MountedMounts.Clear();
        UnmountedMounts.Clear();

        foreach (var mount in _mountService.Mounts)
        {
            AllMounts.Add(mount);
            if (mount.IsMounted)
                MountedMounts.Add(mount);
            else
                UnmountedMounts.Add(mount);
        }
    }

    private void UpdateMountCollections()
    {
        MountedMounts.Clear();
        UnmountedMounts.Clear();

        foreach (var mount in AllMounts)
        {
            if (mount.IsMounted)
                MountedMounts.Add(mount);
            else
                UnmountedMounts.Add(mount);
        }
    }

    private void MoveToMounted(MountInfo mount)
    {
        if (UnmountedMounts.Contains(mount))
            UnmountedMounts.Remove(mount);
        if (!MountedMounts.Contains(mount))
            MountedMounts.Add(mount);
        mount.IsMounted = true;
        mount.Status = $"已挂载到 {mount.LocalDrive}";
    }

    private void MoveToUnmounted(MountInfo mount)
    {
        if (MountedMounts.Contains(mount))
            MountedMounts.Remove(mount);
        if (!UnmountedMounts.Contains(mount))
            UnmountedMounts.Add(mount);
        mount.IsMounted = false;
        mount.Status = "未挂载";
    }

    [RelayCommand]
    private void AddMount()
    {
        _editingOriginalMount = null; // 新增模式
        EditingMount = new MountInfo
        {
            LocalDrive = _mountService.GetAvailableMountPoint(),
            UseHttps = true,
            Port = 443 // WebDAV 默认端口
        };
        IsEditing = true;
    }

    [RelayCommand]
    private async Task EditMount()
    {
        if (SelectedMount == null) return;

        // 如果已挂载，需要确认
        if (SelectedMount.IsMounted)
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                "编辑挂载",
                "编辑挂载需要先卸载挂载，是否继续？");
            if (!confirmed) return;

            // 卸载挂载
            await UnmountAsync(SelectedMount);
        }

        EditMountInternal(SelectedMount);
    }

    private void EditMountInternal(MountInfo mount)
    {
        _editingOriginalMount = mount; // 编辑模式
        EditingMount = new MountInfo
        {
            Name = mount.Name,
            RemotePath = mount.RemotePath,
            LocalDrive = mount.LocalDrive,
            Type = mount.Type,
            Url = mount.Url,
            User = mount.User,
            Password = mount.Password,
            Vendor = mount.Vendor,
            AutoMountOnStart = mount.AutoMountOnStart,
            UseNetworkMode = mount.UseNetworkMode,
            UseHttps = mount.UseHttps,
            Host = mount.Host,
            Port = mount.Port,
            Path = mount.Path,
            // S3 字段
            AccessKeyId = mount.AccessKeyId,
            SecretAccessKey = mount.SecretAccessKey,
            Region = mount.Region,
            Endpoint = mount.Endpoint,
            S3Provider = mount.S3Provider
        };
        IsEditing = true;
    }

    [RelayCommand]
    private void DeleteMount()
    {
        if (SelectedMount == null) return;
        DeleteMountInternal(SelectedMount);
    }

    private void DeleteMountInternal(MountInfo mount)
    {
        try
        {
            _mountService.DeleteMount(mount.Name);
            AllMounts.Remove(mount);
            MountedMounts.Remove(mount);
            UnmountedMounts.Remove(mount);
            SelectedMount = null;
            _notificationService.ShowSuccess("挂载已删除");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveMount()
    {
        if (string.IsNullOrWhiteSpace(EditingMount.Name))
        {
            _notificationService.ShowWarning("请输入名称");
            return;
        }

        // 根据类型验证必填字段
        if (EditingMount.IsWebDavType)
        {
            if (string.IsNullOrWhiteSpace(EditingMount.Host))
            {
                _notificationService.ShowWarning("请输入主机地址");
                return;
            }
        }
        else if (EditingMount.IsFtpType || EditingMount.IsSftpType)
        {
            if (string.IsNullOrWhiteSpace(EditingMount.Host))
            {
                _notificationService.ShowWarning("请输入主机地址");
                return;
            }
        }
        else if (EditingMount.IsS3Type)
        {
            if (string.IsNullOrWhiteSpace(EditingMount.AccessKeyId))
            {
                _notificationService.ShowWarning("请输入 Access Key ID");
                return;
            }
            if (string.IsNullOrWhiteSpace(EditingMount.SecretAccessKey))
            {
                _notificationService.ShowWarning("请输入 Secret Access Key");
                return;
            }
            if (string.IsNullOrWhiteSpace(EditingMount.RemotePath))
            {
                _notificationService.ShowWarning("请输入 Bucket 名称");
                return;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(EditingMount.Url))
            {
                _notificationService.ShowWarning("请输入 URL");
                return;
            }
        }

        try
        {
            if (_editingOriginalMount != null)
            {
                // 编辑模式：更新原有挂载
                // 保存原始名称（用于更新 rclone 配置）
                var originalName = _editingOriginalMount.Name;
                var wasMounted = _editingOriginalMount.IsMounted;

                // 如果名称被修改，需要检查新名称是否已被其他挂载使用
                if (EditingMount.Name != originalName)
                {
                    var nameConflict = AllMounts.Any(m => m.Name == EditingMount.Name && m != _editingOriginalMount);
                    if (nameConflict)
                    {
                        _notificationService.ShowWarning($"已存在名为 {EditingMount.Name} 的挂载");
                        return;
                    }
                }

                // 如果当前已挂载，先卸载
                if (wasMounted)
                {
                    _mountService.Unmount(originalName);
                }

                _editingOriginalMount.Name = EditingMount.Name;
                _editingOriginalMount.RemotePath = EditingMount.RemotePath;
                _editingOriginalMount.LocalDrive = EditingMount.LocalDrive;
                _editingOriginalMount.Type = EditingMount.Type;
                _editingOriginalMount.Url = EditingMount.ComputedUrl;
                _editingOriginalMount.User = EditingMount.User;
                _editingOriginalMount.Password = EditingMount.Password;
                _editingOriginalMount.Vendor = EditingMount.Vendor;
                _editingOriginalMount.AutoMountOnStart = EditingMount.AutoMountOnStart;
                _editingOriginalMount.UseNetworkMode = EditingMount.UseNetworkMode;
                _editingOriginalMount.UseHttps = EditingMount.UseHttps;
                _editingOriginalMount.Host = EditingMount.Host;
                _editingOriginalMount.Port = EditingMount.Port;
                _editingOriginalMount.Path = EditingMount.Path;
                // S3 字段
                _editingOriginalMount.AccessKeyId = EditingMount.AccessKeyId;
                _editingOriginalMount.SecretAccessKey = EditingMount.SecretAccessKey;
                _editingOriginalMount.Region = EditingMount.Region;
                _editingOriginalMount.Endpoint = EditingMount.Endpoint;
                _editingOriginalMount.S3Provider = EditingMount.S3Provider;

                await _mountService.UpdateMountAsync(_editingOriginalMount, originalName);

                // 如果之前已挂载，重新挂载
                if (wasMounted)
                {
                    try
                    {
                        await _mountService.MountAsync(_editingOriginalMount.Name, originalName);
                        _notificationService.ShowSuccess("挂载已保存并重新挂载");
                    }
                    catch (Exception ex)
                    {
                        _notificationService.ShowError($"保存成功，但重新挂载失败: {ex.Message}");
                    }
                }
                else
                {
                    _notificationService.ShowSuccess("挂载已保存");
                }

                // 更新集合（名称可能变化导致需要重新分类）
                UpdateMountCollections();
            }
            else
            {
                // 新增模式
                if (AllMounts.Any(m => m.Name == EditingMount.Name))
                {
                    _notificationService.ShowWarning($"已存在名为 {EditingMount.Name} 的挂载");
                    return;
                }

                var newMount = new MountInfo
                {
                    Name = EditingMount.Name,
                    RemotePath = EditingMount.RemotePath,
                    LocalDrive = EditingMount.LocalDrive,
                    Type = EditingMount.Type,
                    Url = EditingMount.ComputedUrl,
                    User = EditingMount.User,
                    Password = EditingMount.Password,
                    Vendor = EditingMount.Vendor,
                    AutoMountOnStart = EditingMount.AutoMountOnStart,
                    UseNetworkMode = EditingMount.UseNetworkMode,
                    UseHttps = EditingMount.UseHttps,
                    Host = EditingMount.Host,
                    Port = EditingMount.Port,
                    Path = EditingMount.Path,
                    // S3 字段
                    AccessKeyId = EditingMount.AccessKeyId,
                    SecretAccessKey = EditingMount.SecretAccessKey,
                    Region = EditingMount.Region,
                    Endpoint = EditingMount.Endpoint,
                    S3Provider = EditingMount.S3Provider,
                    IsMounted = false,
                    Status = "未挂载"
                };
                _mountService.AddMount(newMount);
                AllMounts.Add(newMount);
                UnmountedMounts.Add(newMount);
                _notificationService.ShowSuccess("挂载已保存");
            }

            IsEditing = false;
            _editingOriginalMount = null;
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        _editingOriginalMount = null;
    }

    [RelayCommand]
    private async Task MountSelected()
    {
        if (SelectedMount == null || SelectedMount.IsMounted) return;
        await MountAsync(SelectedMount);
    }

    [RelayCommand]
    private async Task UnmountSelected()
    {
        if (SelectedMount == null || !SelectedMount.IsMounted) return;
        await UnmountAsync(SelectedMount);
    }

    [RelayCommand]
    private async Task MountAllAsync()
    {
        var toMount = UnmountedMounts.ToList();
        foreach (var mount in toMount)
        {
            await MountAsync(mount);
        }
    }

    [RelayCommand]
    private async Task UnmountAllAsync()
    {
        var toUnmount = MountedMounts.ToList();
        foreach (var mount in toUnmount)
        {
            await UnmountAsync(mount);
        }
    }

    [RelayCommand]
    private async Task MountItem(MountInfo? mount)
    {
        if (mount == null || mount.IsMounted) return;
        await MountAsync(mount);
    }

    [RelayCommand]
    private async Task UnmountItem(MountInfo? mount)
    {
        if (mount == null || !mount.IsMounted) return;
        await UnmountAsync(mount);
    }

    [RelayCommand]
    private async Task EditItem(MountInfo? mount)
    {
        if (mount == null) return;

        // 如果已挂载，需要确认
        if (mount.IsMounted)
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                "编辑挂载",
                "编辑挂载需要先卸载挂载，是否继续？");
            if (!confirmed) return;

            // 卸载挂载
            await UnmountAsync(mount);
        }

        SelectedMount = mount;
        EditMountInternal(mount);
    }

    [RelayCommand]
    private void DeleteItem(MountInfo? mount)
    {
        if (mount == null) return;
        DeleteMountInternal(mount);
    }

    private async Task MountAsync(MountInfo mount)
    {
        try
        {
            await _mountService.MountAsync(mount.Name);
            MoveToMounted(mount);
            _notificationService.ShowSuccess($"{mount.Name} 挂载成功");
        }
        catch (Exception ex)
        {
            mount.Status = "挂载失败";
            _notificationService.ShowError(ex.Message);
        }
    }

    private async Task UnmountAsync(MountInfo mount)
    {
        try
        {
            await _mountService.UnmountAsync(mount.Name);
            MoveToUnmounted(mount);
            _notificationService.ShowSuccess($"{mount.Name} 已卸载");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }
}