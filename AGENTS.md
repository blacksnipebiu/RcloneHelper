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
dotnet run -c Release

# Publish standalone application
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Project Structure

```
RcloneHelper/
├── RcloneHelper/                    # Main UI project (Avalonia)
│   ├── Views/
│   │   ├── Pages/                   # Page controls (HomePage, SettingsPage, etc.)
│   │   └── Windows/                 # MainWindow
│   ├── Styles/                      # Avalonia styles
│   └── App.axaml.cs                 # DI container setup
│
├── RcloneHelper.Core/               # Core logic (platform-agnostic)
│   ├── Models/                      # Data models (MountInfo, MountConfig)
│   ├── Pages/                       # ViewModels (HomePageViewModel, etc.)
│   ├── Services/
│   │   ├── Abstractions/            # Interfaces (ISystemService, INotificationService)
│   │   ├── Implementations/         # Platform-specific implementations
│   │   └── MountService.cs          # Core mount management
│   ├── Helpers/                     # Utilities (PathUtil, AppJsonContext)
│   └── Windows/                     # MainWindowViewModel
│
└── README.md
```

## Architecture

- **Framework**: .NET 8.0 with Avalonia UI 11.x
- **MVVM**: CommunityToolkit.Mvvm (Source Generators)
- **DI**: Microsoft.Extensions.DependencyInjection
- **Pattern**: ViewModels in Core, Views in main project

## Code Style Guidelines

### MVVM Pattern

```csharp
// ViewModels extend ObservableObject, use partial properties
public partial class MyViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "";
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullName))]  // Chain notifications
    private string _firstName = "";
    
    [RelayCommand]
    private void DoSomething() { }
}
```

### Models

```csharp
// Config models: plain classes for JSON serialization
public class MountConfig
{
    public string Name { get; set; } = "";
    public bool AutoMountOnStart { get; set; } = true;
}

// Runtime models: ObservableObject for UI binding
public partial class MountInfo : ObservableObject
{
    [ObservableProperty] private string _name = "";
    public static MountInfo FromConfig(MountConfig config) { }
    public MountConfig ToConfig() { }
}
```

### Services

```csharp
// Register in ServiceCollectionExtensions.cs
services.AddSingleton<IMyService, MyService>();
services.AddSingleton<MyViewModel>();

// Inject via constructor
public class MyViewModel(IMyService myService)
{
    // ...
}
```

### JSON Serialization

Use `AppJsonContext` for trim-friendly serialization:

```csharp
// Register types in AppJsonContext.cs
[JsonSerializable(typeof(MyType))]

// Use with generated context
var json = JsonSerializer.Serialize(data, AppJsonContext.Default.MyType);
var data = JsonSerializer.Deserialize(json, AppJsonContext.Default.MyType);
```

### Error Handling

```csharp
// Throw descriptive exceptions
throw new ArgumentException("Name cannot be empty");
throw new InvalidOperationException($"Mount not found: {name}");

// Wrap with context
catch (Exception ex)
{
    throw new InvalidOperationException($"Operation failed: {ex.Message}", ex);
}
```

### Naming Conventions

- **Namespaces**: `RcloneHelper.<Area>.<SubArea>` (e.g., `RcloneHelper.Services.Abstractions`)
- **Interfaces**: `I<ServiceName>` (e.g., `ISystemService`)
- **ViewModels**: `<Name>ViewModel` (e.g., `HomePageViewModel`)
- **Pages**: `<Name>Page.axaml` with `<Name>Page.axaml.cs`
- **Private fields**: `_camelCase` (required for ObservableProperty)

### Avalonia XAML

```xml
<!-- Use compiled bindings with x:DataType -->
<UserControl x:Class="RcloneHelper.Views.Pages.HomePage"
              x:DataType="vm:HomePageViewModel">
    
<!-- Dynamic resource references -->
<Border Background="{DynamicResource SurfaceBrush}">

<!-- Visibility bindings with ! prefix for negation -->
<TextBox IsVisible="{Binding !EditingMount.IsWebDavType}" />
```

### Cross-Platform Services

Platform-specific implementations in `Services/Implementations/`:
- `WindowsSystemService.cs`
- `LinuxSystemService.cs`
- `MacOSSystemService.cs`

Selected via `RuntimeInformation.IsOSPlatform()` in `ServiceCollectionExtensions.cs`.

## Important Notes

- Use Release builds during development to avoid file lock issues
- All ViewModels are Singleton (desktop app state persistence)
- Config files stored in `%APPDATA%\RcloneHelper\`
- rclone must be installed and in PATH, or in application directory

## File Formatting

- **Line Endings**: Use CRLF (Windows style) for all source files
- **Encoding**: UTF-8 with BOM for better compatibility