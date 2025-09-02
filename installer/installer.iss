; -----------------------------------------------
; DragOverlay Inno Setup Script (Build 1.11)
; -----------------------------------------------

#define MyAppName     "DragOverlay"
#define MyCompany     "GJames / Stew-Mac"
#define MyAppExeName  "CastleOverlayV2.exe"   ; your EXE filename
#define MyAppVersion  "1.11"                  ; installer/app display version

; Source (publish) and output folders you specified:
#define MyPublishDir  "C:\Users\Stewart McMillan\DragOverlay\Publish"
#define MyOutputDir   "C:\Users\Stewart McMillan\DragOverlay\Publish\InstallerOutput"

; Stable AppId GUID (do not change once shipped)
#define MyAppId "{{7C2F6B4A-8C0E-4C5C-9E7B-2D6B3B2D2F31}}"

[Setup]
; Core metadata
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyCompany}

; Install locations / output
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyAppName}-Setup-v{#MyAppVersion}
ArchitecturesInstallIn64BitMode=x64

; UX / packaging
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableReadyMemo=no
SetupLogging=yes
PrivilegesRequired=lowest

; Appearance in Windows “Apps & Features”
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

; Optional: include a license file if you have one
; LicenseFile=license.txt

; Optional code signing (commented out)
; SignTool=msauthenticode
; SignToolParameters=/a /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Package everything from the Publish folder (your screenshot location)
; Exclude PDBs to keep installer clean
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: recursesubdirs ignoreversion

; If you keep a default config outside Publish and want to ensure first-run config:
; Source: "C:\path\to\config\config.json"; DestDir: "{app}\config"; Flags: onlyifdoesntexist

[Icons]
; Start Menu shortcut
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"

; Desktop shortcut MUST be called DragOverlay
Name: "{commondesktop}\DragOverlay"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; WorkingDir: "{app}"

[Run]
; Launch after install
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
