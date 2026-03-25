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
- .NET 8.0 或更高版本
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
dotnet build
dotnet run
```

### 3. 发布独立应用

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 使用说明

### 添加挂载

1. 点击"添加挂载"按钮
2. 填写以下信息：
   - **名称**: 挂载的标识名称
   - **类型**: 选择 webdav (或其他类型)
   - **URL**: WebDAV 服务器地址
   - **用户名/密码**: 认证信息（如需要）
   - **WebDAV 服务商**: 选择对应的服务商类型
   - **本地盘符**: 要挂载到的 Windows 盘符
3. 点击"保存"

### 挂载/卸载

- 选中列表中的挂载项，点击"挂载/卸载"按钮
- 或使用"挂载全部"/"卸载全部"批量操作

### 开机启动设置

- 勾选"开机启动"复选框，程序将添加到 Windows 启动项
- 勾选"启动时自动挂载"，程序启动后会自动挂载所有配置

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
