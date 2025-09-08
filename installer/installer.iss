; -----------------------------------------------
; DragOverlay Inno Setup Script (Build 1.11)
; -----------------------------------------------

#define MyAppName     "DragOverlay"
#define MyCompany     "GJames / Stew-Mac"
#define MyAppExeName  "CastleOverlayV2.exe"
#define MyAppVersion  "1.11"

#define MyPublishDir  "C:\Users\Stewart McMillan\DragOverlay\Publish"
#define MyOutputDir   "C:\Users\Stewart McMillan\DragOverlay\Publish\InstallerOutput"

#define MyAppId "{{7C2F6B4A-8C0E-4C5C-9E7B-2D6B3B2D2F31}}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyCompany}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyAppName}-Setup-v{#MyAppVersion}
ArchitecturesInstallIn64BitMode=x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableReadyMemo=no
SetupLogging=yes
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
; LicenseFile=license.txt
; SignTool=msauthenticode
; SignToolParameters=/a /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
; Desktop shortcut for the current user (avoids admin rights)
Name: "{userdesktop}\DragOverlay"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
