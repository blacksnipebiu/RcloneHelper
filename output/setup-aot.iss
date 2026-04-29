; RcloneHelper AOT Inno Setup Script
; AOT 编译版本，启动更快，体积更小
; 适用于已安装 WinFsp 和 rclone 的用户

#define MyAppName "RcloneHelper"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "nullpoint333"
#define MyAppURL "https://github.com/nullpoint333/RcloneHelper"
#define MyAppExeName "RcloneHelper.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{C1D2E3F4-A5B6-4C7D-8E9F-0A1B2C3D4E5F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; AOT 版本不需要管理员权限（不安装 WinFsp）
PrivilegesRequired=lowest
OutputDir={#SourcePath}
OutputBaseFilename=RcloneHelperv{#MyAppVersion}-AOT
Compression=lzma
SolidCompression=yes
WizardStyle=modern

SetupIconFile="{#SourcePath}..\RcloneHelper\Assets\app.ico"

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 主程序文件
Source: "{#SourcePath}publish-aot\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
