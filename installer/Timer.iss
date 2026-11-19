#define MyAppName "Timer"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Windows Timer"
#define MyAppExeName "Timer.exe"
#define MyAppId "{{7C3E9A14-2B6F-4D58-9E01-6A4B8C2D5F70}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Timer
DefaultGroupName=Timer
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=Timer-Setup
SetupIconFile=..\src\WindowsTimer\Resources\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
AppMutex=Local\WindowsTimer.SingleInstance

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Timer"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; AppUserModelID: "WindowsTimer.Timer"
Name: "{autodesktop}\Timer"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppExeName}"; AppUserModelID: "WindowsTimer.Timer"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Timer"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
