#define MyAppName "LINHER Keyboard Wedge"
#define MyAppVersion "1.0.1"
#define MyAppExeName "LinherKeyboardWedgeLauncher.exe"

[Setup]
AppId={{A75B97B8-9363-4289-A17C-11B9AF80F9C7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\LINHER Keyboard Wedge
DefaultGroupName={#MyAppName}
OutputDir=..\dist
OutputBaseFilename=LinherKeyboardWedgeSetup-Inno
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest

[Files]
Source: "..\dist\LinherKeyboardWedgeLauncher\LinherKeyboardWedgeLauncher.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\LinherKeyboardWedgeLauncher\bundled_assets\*"; DestDir: "{app}\bundled_assets"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\config.example.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir {#MyAppName}"; Flags: nowait postinstall skipifsilent
