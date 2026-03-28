# RcloneHelper

一个使用 Avalonia UI 开发的 rclone 辅助工具，支持 WebDAV 等多种存储类型的挂载管理。

## 功能特性

- **添加 WebDAV 挂载**：支持配置 WebDAV 服务器，包括 Nextcloud、OwnCloud、SharePoint 等
- **开机自启动**：可设置 Windows 开机自动启动
- **自动挂载**：启动后自动挂载所有配置的存储
- **多类型支持**：支持 WebDAV、FTP、SFTP、S3 等 rclone 支持的存储类型
- **简洁的界面**：使用 Avalonia 构建的跨平台 UI

## 系统要求

- Windows 10/11
- .NET 10.0 或更高版本
- rclone 已安装并在 PATH 中，或放在程序目录下

## 使用方法

### 1. 安装 rclone

确保 rclone 已安装：
```bash
# 下载地址: https://rclone.org/downloads/
# 或使用 chocolatey
choco install rclone
```

### 2. 编译项目

```bash
cd RcloneHelper
dotnet restore
dotnet build -c Release
dotnet run --no-build -c Release
```

> ⚠️ **注意**：开发时请使用 Release 配置。Debug 编译输出会被正在运行的 Debug 实例锁定，导致编译失败。如需同时运行多个实例，可使用 `--urls` 参数指定不同端口。

### 3. 发布独立应用

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 使用说明

### 添加挂载

1. 点击标题栏的"添加挂载"按钮
2. 填写以下信息：
   - **名称**: 挂载的标识名称
   - **类型**: 选择 webdav (或其他类型)
   - **URL**: WebDAV 服务器地址
   - **用户名/密码**: 认证信息（如需要）
   - **WebDAV 服务商**: 选择对应的服务商类型
   - **本地盘符**: 要挂载到的 Windows 盘符
3. 点击"保存"

### 挂载/卸载

- 点击列表中的 ▶（挂载）或 ⏸（卸载）图标按钮
- 列表上方有"挂载全部"/"卸载全部"批量操作按钮

### 开机启动设置

- 进入"配置"页面，勾选"开机启动"，程序将添加到 Windows 启动项
- 勾选"启动时自动挂载所有存储"，程序启动后会自动挂载所有已配置的存储

## 配置文件

配置文件保存在：
```
%APPDATA%\RcloneHelper\mounts.json
```

## 注意事项

1. 首次使用需要确保 rclone 已正确安装
2. 挂载需要管理员权限（某些情况下）
3. 程序退出时会自动卸载所有挂载

## 许可证

MIT License
