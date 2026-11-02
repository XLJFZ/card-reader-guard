; Inno Setup 6 script for Card Reader Guard.
; Build with: ISCC.exe installer\CardReaderGuard.iss
; The portable build in dist\ must exist first (run build.ps1).

#define AppName "Card Reader Guard"
#define AppVersion "0.2.2"
#define AppPublisher "XLJFZ"
#define AppExeName "CardReaderGuard.exe"
#define SourceDir "..\dist"

[Setup]
AppId={{F462E210-96D2-4E82-B67D-AF1EA3A58817}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/XLJFZ/card-reader-guard
AppSupportURL=https://github.com/XLJFZ/card-reader-guard/issues
AppUpdatesURL=https://github.com/XLJFZ/card-reader-guard/releases/latest
DefaultDirName={localappdata}\Programs\CardReaderGuard
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=CardReaderGuard-Setup-{#AppVersion}-x64
SetupIconFile=..\Assets\App.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "startup"; Description: "Start monitoring when Windows starts"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\CardReaderGuard.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\CardReaderGuard.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\CardReaderGuard.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Card Reader Guard"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\CardReaderGuard.ico"
Name: "{group}\Stop monitoring"; Filename: "{app}\{#AppExeName}"; Parameters: "--stop"; IconFilename: "{app}\CardReaderGuard.ico"
Name: "{group}\Uninstall Card Reader Guard"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Card Reader Guard"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\CardReaderGuard.ico"; Tasks: desktopicon
Name: "{userstartup}\Card Reader Guard"; Filename: "{app}\{#AppExeName}"; Parameters: "--background"; IconFilename: "{app}\CardReaderGuard.ico"; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Open Card Reader Guard"; Flags: nowait postinstall skipifsilent
