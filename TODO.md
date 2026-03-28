# RcloneHelper TODO

## 项目概述

**RcloneHelper** 是一个基于 Avalonia UI 开发的跨平台 rclone 辅助工具，用于管理 WebDAV、FTP、SFTP、S3 等多种存储类型的挂载操作。

---

## ✅ 已完成功能

### 主页（HomePage）
- [x] 挂载列表显示（全部/已挂载/未挂载分组）
- [x] 单个挂载：挂载、卸载、编辑、删除按钮
- [x] 批量操作：挂载全部、卸载全部
- [x] 新增挂载表单（支持 WebDAV URL 组件拆分：Host/Port/Path/UseHttps）
- [x] 挂载状态实时刷新

### 配置页（SettingsPage）
- [x] 开机自启动开关（Windows 注册表）
- [x] 启动时自动挂载所有存储
- [x] rclone 可执行文件路径配置
- [x] WinFsp 依赖检测（显示状态和安装链接）

### rclone 配置页（RcloneConfigPage）
- [x] 显示 rclone 版本信息
- [x] 显示/打开 rclone 配置文件路径
- [x] 显示/打开 rclone 缓存目录
- [x] 读取并解析 rclone.conf，显示 remote 列表
- [x] 读取应用日志
- [x] 删除 remote
- [x] 卸载选中的 remote

### 核心服务
- [x] MountService：挂载 CRUD、rclone config create/delete、进程管理
- [x] ISystemService：平台抽象（Windows/Linux/MacOS 实现）
- [x] FileLoggerService：文件日志记录
- [x] NotificationService：Toast 通知
- [x] DialogService：确认对话框

---

## 📋 待办事项

### 🔴 P0 - 稳定性/安全（必须修复）

- [x] **修复空 catch 块** ✅
  - `ConfigService.cs`: 添加日志记录
  - `FileLoggerService.cs`: 添加控制台 fallback 输出
  - 改为：记录日志 + 适当向上传播或返回明确错误状态

- [x] **挂载进程异常退出通知** ✅
  - `MountService.cs` Exited 事件添加通知
  - 通过 `INotificationService.ShowWarning()` 提醒用户

- [x] **密码安全存储** ✅
  - 创建 `SecureStorageHelper.cs` 使用 Windows DPAPI 加密
  - `MountInfo.ToConfig()` 自动加密密码
  - `MountInfo.FromConfig()` 自动解密密码
  - 兼容未加密的旧数据

### 🟡 P1 - 功能完善（核心价值）

- [x] **完成深色模式 UI 开关** ✅
  - `AppConfig.IsDarkMode` 已连接到 ThemeService
  - 标题栏有深色模式切换开关，支持动态切换

- [x] **非 WebDAV 类型配置表单完善** ✅
  - 新增 FTP/SFTP 类型专用配置：主机地址、端口、用户名、密码
  - 新增 S3 类型配置：服务商、Access Key、Secret Key、区域、Endpoint
  - 编辑表单根据 Type 动态显示不同配置项
  - 添加类型判断属性：IsFtpType, IsSftpType, IsS3Type

- [x] **添加单元测试项目** ✅
  - 创建 `RcloneHelper.Tests` 项目（xUnit + Moq）
  - 测试覆盖：PathUtil, SecureStorageHelper, MountInfo
  - 29 个测试全部通过

- [x] **配置导入/导出功能** ✅
  - SettingsPage 添加"导出配置"/"导入配置"按钮
  - 导出为带时间戳的 JSON 文件
  - 导入时跳过已存在的挂载

### 🟢 P2 - 体验提升（用户感知）

- [ ] **Windows 原生 Toast 通知**
  - 当前使用应用内 Toast，托盘状态下用户看不到
  - 使用 `Microsoft.Toolkit.Uwp.Notifications` 或 WinRT API
  - 支持操作按钮（如"重新挂载"）

- [ ] **rclone 版本自动检查更新**
  - 当前仅有跳转下载页面的按钮
  - 调用 `rclone version` 获取当前版本
  - 调用 GitHub API 或 rclone.org 获取最新版本
  - 显示更新提示

- [ ] **挂载历史记录**
  - 新增 `MountHistory` 表：时间、操作类型、成功/失败、耗时
  - 新增历史查看页面或面板

### 🔵 P3 - 扩展功能（可选）

- [ ] **多语言支持（i18n）**
  - 当前硬编码中文字符串
  - 可使用 .resx 资源文件支持中英文切换

- [ ] **自动更新机制**
  - 应用自身版本检查和更新

- [ ] **Linux/macOS 实际测试与修复**
  - 跨平台实现已完成，但未在真实环境测试

- [ ] **日志等级控制**
  - Debug/Info/Warning/Error 分级
  - 用户可配置日志级别

---

## 🐛 待修复问题

| 问题 | 说明 | 严重程度 |
|------|------|----------|
| ~~空 catch 块~~ | ~~多处 catch { } 吞掉错误~~ | ✅ 已修复 |
| ~~密码明文存储~~ | ~~mounts.json 中密码未加密~~ | ✅ 已修复 |
| ~~挂载异常退出无通知~~ | ~~进程退出仅更新状态，用户不知情~~ | ✅ 已修复 |
| 输入验证不足 | URL/配置项未验证有效性 | 🟡 中 |
| 编辑时名称冲突检查 | 编辑挂载时如果改名为已存在的名称，仅在保存时检查 | 🟢 低 |

---

## 📝 配置文件位置

```
%APPDATA%\RcloneHelper\
├── mounts.json      # 挂载配置
├── settings.json    # 应用设置
├── app.log         # 应用日志
└── rclone.log      # rclone 操作日志
```

---

## 构建命令

```bash
# 还原依赖
dotnet restore

# 构建（开发时使用 Release 避免文件锁定）
dotnet build -c Release

# 运行
dotnet run -c Release --no-build

# 发布独立应用
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
