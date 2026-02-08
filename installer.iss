[Setup]
AppId={{C7A9E24F-7B3A-4B6A-B6A7-3B2D7D6F5E4C}
AppName=AppStarter
AppVersion={#AppVersion}
AppPublisher=AppStarter Team
DefaultDirName={autopf}\AppStarter
DefaultGroupName=AppStarter
AllowNoIcons=yes
OutputDir=..\dist\{#AppVersion}
OutputBaseFilename=AppStarter_{#AppArch}_Setup
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed={#ArchitecturesAllowed}
ArchitecturesInstallIn64BitMode={#ArchitecturesInstallIn64BitMode}
UninstallDisplayIcon={app}\AppStarter.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\{#AppVersion}\{#AppArch}\AppStarter.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\AppStarter"; Filename: "{app}\AppStarter.exe"
Name: "{autodesktop}\AppStarter"; Filename: "{app}\AppStarter.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AppStarter.exe"; Description: "{cm:LaunchProgram,AppStarter}"; Flags: nowait postinstall skipifsilent
