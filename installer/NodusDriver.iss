#define MyAppName "NODUS Driver"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Franklys"
#define MyAppExeName "NodusDriver.exe"

[Setup]
AppId={{7F45B4D6-539D-47AA-B945-0D56672C6FC1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\NODUS Driver
DefaultGroupName=NODUS Driver
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=NODUS-Driver-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\artifacts\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\NODUS Driver"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\NODUS Driver"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch NODUS Driver"; Flags: nowait postinstall skipifsilent
