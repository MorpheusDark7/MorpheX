#define MyAppName "MorpheX Live"
#define MyAppVersion "0.1.1"
#define MyAppPublisher "MorpheusDark"
#define MyAppExeName "MorpheX.exe"

[Setup]
AppId={{E57D2C04-0D0E-4C2E-8CE6-6DE8413BD6A8}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes

PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline

OutputDir=dist
OutputBaseFilename=MorpheX-Live-Setup-v{#MyAppVersion}
SetupIconFile=src\MorpheX\Assets\Branding\morphex-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

WizardStyle=modern
WizardSizePercent=110

Compression=lzma2/ultra64
SolidCompression=yes
InternalCompressLevel=ultra

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startwithwindows"; Description: "Launch MorpheX automatically when Windows starts"; GroupDescription: "Windows Integration:"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MorpheX"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: startwithwindows

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall
