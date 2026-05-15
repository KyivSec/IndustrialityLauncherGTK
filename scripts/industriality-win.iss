; Industriality Launcher — Windows installer wizard
; Compile with:
;   ISCC.exe scripts\industriality-win.iss ^
;     /DSourceDir=<abs path to publish/launcher/<rid>> ^
;     /DOutputDir=<abs path to publish/installers/<rid>> ^
;     /DArch=<x64|arm64> ^
;     /DAppVersion=<version string>
;
; The script is normally driven by scripts\build-installer.ps1; the parameters
; above are required when invoking ISCC.exe directly.

#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif

#ifndef Arch
  #define Arch "x64"
#endif

#ifndef SourceDir
  #error "Define SourceDir via /DSourceDir=<path-to-publish-tree>"
#endif

#ifndef OutputDir
  #error "Define OutputDir via /DOutputDir=<path-to-installer-output>"
#endif

[Setup]
AppId={{F1A8C2D0-3E5F-4A1B-9C7D-1D5751A11A11}
AppName=Industriality Launcher
AppVersion={#AppVersion}
AppVerName=Industriality Launcher {#AppVersion}
AppPublisher=KyivSec
DefaultDirName={userappdata}\IndustrialityLauncher
DefaultGroupName=Industriality
DisableProgramGroupPage=auto
DisableDirPage=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed={#Arch}
ArchitecturesInstallIn64BitMode={#Arch}
OutputDir={#OutputDir}
OutputBaseFilename=IndustrialityLauncherSetup-{#Arch}
SetupIconFile=..\src\Industriality.UI.Gtk\Assets\icon.ico
Compression=lzma2/normal
SolidCompression=no
WizardStyle=modern
UninstallDisplayName=Industriality Launcher
UninstallDisplayIcon={app}\IndustrialityLauncher.exe
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\Industriality Launcher"; Filename: "{app}\IndustrialityLauncher.exe"
Name: "{group}\Uninstall Industriality Launcher"; Filename: "{uninstallexe}"
Name: "{userdesktop}\Industriality Launcher"; Filename: "{app}\IndustrialityLauncher.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\IndustrialityLauncher.exe"; Description: "{cm:LaunchProgram,Industriality Launcher}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\minecraft"
Type: filesandordirs; Name: "{app}\runtimes"
Type: files; Name: "{app}\launcher-settings.json"
Type: files; Name: "{app}\Industriality.NeoForge.zip"
Type: files; Name: "{app}\modpack-version.txt"
