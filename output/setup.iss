; RcloneHelper Inno Setup Script
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "RcloneHelper"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "nullpoint333"
#define MyAppURL "https://github.com/nullpoint333/RcloneHelper"
#define MyAppExeName "RcloneHelper.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{A7B8C9D0-E1F2-3A4B-5C6D-7E8F9A0B1C2D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; Require admin privileges for WinFsp installation
PrivilegesRequired=admin
OutputDir={#SourcePath}
OutputBaseFilename=RcloneHelperv{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern

SetupIconFile="{#SourcePath}..\RcloneHelper\Assets\app.ico"

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 依赖文件 - 嵌入安装包但不自动安装
Source: "{#SourcePath}winfsp-2.1.25156.msi"; DestDir: "{tmp}"; Flags: dontcopy noencryption
Source: "{#SourcePath}rclone-v1.73.3-windows-amd64.zip"; DestDir: "{tmp}"; Flags: dontcopy noencryption
; 主程序文件
Source: "{#SourcePath}publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
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

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  WinFspPath: String;
  RclonePath: String;
  RcloneDest: String;
  TempDir: String;
begin
  // ssInstall 阶段：在主程序文件复制之前安装 WinFsp
  if CurStep = ssInstall then
  begin
    TempDir := ExpandConstant('{tmp}');
    
    // 提取并安装 WinFsp
    ExtractTemporaryFile('winfsp-2.1.25156.msi');
    WinFspPath := TempDir + '\winfsp-2.1.25156.msi';
    
    if FileExists(WinFspPath) then
    begin
      // 静默安装 WinFsp MSI
      Exec('msiexec.exe', '/i "' + WinFspPath + '" /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
  end;
  
  // ssPostInstall 阶段：主程序文件复制完成后解压 rclone
  if CurStep = ssPostInstall then
  begin
    TempDir := ExpandConstant('{tmp}');
    
    // 提取 rclone zip
    ExtractTemporaryFile('rclone-v1.73.3-windows-amd64.zip');
    RclonePath := TempDir + '\rclone-v1.73.3-windows-amd64.zip';
    RcloneDest := ExpandConstant('{app}');
    
    if FileExists(RclonePath) and DirExists(RcloneDest) then
    begin
      // 使用 PowerShell 解压 zip 文件
      Exec('powershell.exe', 
           '-Command "Expand-Archive -Path ''' + RclonePath + ''' -DestinationPath ''' + RcloneDest + ''' -Force"', 
           '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
  end;
end;