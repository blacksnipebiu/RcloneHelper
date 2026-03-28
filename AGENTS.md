# RcloneHelper - AGENTS.md

An Avalonia UI application for managing rclone mounts (WebDAV, FTP, SFTP, S3).

## Build Commands

```bash
# Restore dependencies
dotnet restore

# Build (use Release to avoid file lock issues during development)
dotnet build -c Release

# Build Debug (may fail if Debug instance is running)
dotnet build -c Debug

# Run application
dotnet run -c Release --no-build

# Run tests
dotnet test                        # Run all tests
dotnet test --no-build             # Run without rebuilding
dotnet test -c Release             # Run in Release mode
dotnet test --filter "FullyQualifiedName~PathUtilTests"  # Run single test class
dotnet test --filter "FullyQualifiedName~PathUtilTests.AppDataDir"  # Run single test

# Publish standalone application
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Project Structure

```
RcloneHelper/
├── RcloneHelper/                    # Main UI project (Avalonia)
│   ├── Views/Pages/                # Page controls (HomePage, SettingsPage, etc.)
│   ├── Views/Windows/              # MainWindow
│   ├── Styles/                    # Avalonia styles
│   ├── Themes/                     # Theme definitions (Theme.axaml)
│   ├── Services/                   # UI-specific services (DialogService)
│   └── App.axaml.cs               # DI container setup
│
├── RcloneHelper.Core/              # Core logic (platform-agnostic)
│   ├── Models/                     # MountInfo, MountConfig, AppConfig
│   ├── Pages/                      # ViewModels (HomePageViewModel, etc.)
│   ├── Services/
│   │   ├── Abstractions/           # Interfaces (ISystemService, INotificationService)
│   │   ├── Implementations/        # Platform-specific (Windows/Linux/MacOS)
│   │   └── MountService.cs        # Core mount management
│   ├── Helpers/                    # PathUtil, ServiceCollectionExtensions
│   └── Windows/                    # MainWindowViewModel
│
└── RcloneHelper.Tests/             # xUnit tests with Moq
```

## Architecture

- **Framework**: .NET 10.0+ with Avalonia UI 11.x
- **MVVM**: CommunityToolkit.Mvvm (Source Generators)
- **DI**: Microsoft.Extensions.DependencyInjection
- **Testing**: xUnit + Moq
- **Serialization**: System.Text.Json

## Code Style Guidelines

### Imports (ordered)

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RcloneHelper.Models;
using RcloneHelper.Services.Abstractions;
```

### MVVM (CommunityToolkit.Mvvm)

```csharp
public partial class MyViewModel : ObservableObject
{
    // Fields: _camelCase (REQUIRED for ObservableProperty)
    [ObservableProperty]
    private string _name = "";

    // Chain notifications
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullName))]
    private string _firstName = "";

    public string FullName => $"{FirstName} {Name}";

    // Commands generate MyActionCommand
    [RelayCommand]
    private void DoSomething() { }

    [RelayCommand]
    private async Task DoSomethingAsync() { }
}
```

### Models

```csharp
// Config models: plain classes (no ObservableObject)
public class MountConfig
{
    public string Name { get; set; } = "";
}

// Runtime models: ObservableObject for UI binding
public partial class MountInfo : ObservableObject
{
    [ObservableProperty] private string _name = "";
    public static MountInfo FromConfig(MountConfig config) { }
    public MountConfig ToConfig() { }
}
```

### Services & DI

```csharp
// Interfaces: I<ServiceName>, organized with #region sections
public interface ISystemService
{
    #region 开机自启
    bool IsAutoStartEnabled { get; }
    bool SetAutoStart(bool enabled);
    #endregion
}

// Register Singletons in ServiceCollectionExtensions.cs
services.AddSingleton<IMyService, MyService>();
services.AddSingleton<MyViewModel>();

// Constructor injection
public MyViewModel(MountService mountService, INotificationService notificationService)
{
    _mountService = mountService;
    _notificationService = notificationService;
}
```

### JSON Serialization

```csharp
// Standard JsonSerializer with options
var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
var obj = JsonSerializer.Deserialize<MyType>(json);
```

### Error Handling

```csharp
// Throw descriptive exceptions with Chinese messages
throw new ArgumentException("挂载名称不能为空");

// Wrap with context preserving inner exception
catch (Exception ex)
{
    throw new InvalidOperationException($"加载配置失败: {ex.Message}", ex);
}

// Log errors before throwing
_logger.Error($"挂载失败: {name}, 错误: {ex.Message}");
throw new InvalidOperationException($"挂载错误: {ex.Message}", ex);
```

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Namespaces | `RcloneHelper.<Area>.<SubArea>` | `RcloneHelper.Core.Pages` |
| Interfaces | `I<ServiceName>` | `ISystemService` |
| ViewModels | `<Name>ViewModel` | `HomePageViewModel` |
| Pages | `<Name>Page.axaml` | `HomePage.axaml` |
| Private fields | `_camelCase` | `_mountService` |
| Services | `<Name>Service` | `MountService` |

### Avalonia XAML

```xml
<!-- Compiled bindings with x:DataType -->
<UserControl x:Class="RcloneHelper.Views.Pages.HomePage"
              xmlns:vm="using:RcloneHelper.Core.Pages"
              x:DataType="vm:HomePageViewModel">

<!-- Dynamic resources for theming -->
<Border Background="{DynamicResource SurfaceBrush}"/>
<TextBlock Foreground="{DynamicResource ForegroundBrush}"/>

<!-- Visibility: ! prefix for negation -->
<TextBox IsVisible="{Binding !EditingMount.IsWebDavType}"/>

<!-- Commands use generated names -->
<Button Command="{Binding AddMountCommand}"/>

<!-- Relative bindings in DataTemplate -->
<Button Command="{Binding $parent[UserControl].DataContext.EditItemCommand}"
        CommandParameter="{Binding}"/>
```

### Cross-Platform Services

Platform implementations in `Services/Implementations/`:
- `WindowsSystemService.cs`, `LinuxSystemService.cs`, `MacOSSystemService.cs`

Selected via `RuntimeInformation.IsOSPlatform()` in `ServiceCollectionExtensions.cs`.

## File Formatting

- **Line Endings**: CRLF (Windows style)
- **Encoding**: UTF-8 with BOM
- **Indentation**: 4 spaces (no tabs)

## Important Notes

- Use **Release builds** during development to avoid file lock issues
- All ViewModels and Services are **Singleton** (desktop app state persistence)
- Config files: `%APPDATA%\RcloneHelper\` (mounts.json, settings.json)
- rclone must be in PATH or application directory
- Views (code-behind) are minimal - logic is in ViewModels
