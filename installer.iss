; ============================================================
; CryptoWidget 安装包脚本 (Inno Setup 6/7)
; 用法: iscc installer.iss [/DMyAppVersion=0.1.0]
; 版本号优先取命令行 /DMyAppVersion，否则用下方默认值。
; 打包流程见 scripts/build_installer.ps1 或 Makefile 的 dist 目标。
; ============================================================

#ifndef MyAppVersion
#define MyAppVersion "0.0.1"
#endif

#define MyAppName "CryptoWidget"
#define MyAppPublisher "CryptoWidget"
#define MyAppURL "https://github.com/Ledgerbiggg/CryptoWidget"
#define MyAppExeName "CryptoWidget.Shell.exe"

[Setup]
; 安装包/卸载程序的唯一标识（请勿随意更改，否则会视为不同软件）
AppId={{75F0B871-2F73-4EC8-8846-1FB06CCFF4F0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
; 安装到 Program Files，作为"正规软件"集成到系统
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; 图标使用项目内的比特币图标
SetupIconFile=CryptoWidget.Shell\Assets\btc.ico
OutputDir=package
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; 写入 Program Files 需要管理员权限
PrivilegesRequired=admin
; 64 位安装
ArchitecturesInstallIn64BitMode=x64os
; 卸载时清理整个安装目录
UninstallFilesDir={app}\Uninstall

[Languages]
; 英文界面（Inno Setup 默认仅含 Default.isl；中文语言包后续补）
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; 将 dotnet publish 产物整体打包（_publish 为 Makefile dist 临时目录）
Source: "_publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "额外快捷方式:"

[Run]
; 安装完成后可选启动
Filename: "{app}\{#MyAppExeName}"; Description: "立即启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
