; SportsPredictor installer (Inno Setup 6+: https://jrsoftware.org/isinfo.php)
;
; Prerequisites on the BUILD machine before compiling this script:
;   1. Run installer/build-release.ps1 — produces installer/staging/{Api,Desktop,Frontend,Ml,database}
;
; Prerequisites the installer expects on the TARGET machine (documented, not bundled):
;   - .NET 10 Desktop Runtime (for the API and the WPF launcher)
;   - Node.js 22+ (to run the Next.js standalone server.js)
;   - Python 3.12 (to build the Ml/.venv — done automatically by setup-ml-venv.ps1 below)
;   - WebView2 Runtime (ships with Windows 11 by default)
;
; This installer does NOT bundle .NET/Node/Python runtimes themselves — see
; README.md for why (keeping this pass's scope realistic) and how to extend it.

#define MyAppName "SportsPredictor"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SportsPredictor"
#define MyAppExeName "SportsPredictor.Desktop.exe"

[Setup]
AppId={{B6C6C9C1-3B2B-4F6E-9B1B-9C6F6B7E2A11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=SportsPredictor-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "staging\Api\*"; DestDir: "{app}\Api"; Flags: recursesubdirs ignoreversion
Source: "staging\Desktop\*"; DestDir: "{app}\Desktop"; Flags: recursesubdirs ignoreversion
Source: "staging\Frontend\*"; DestDir: "{app}\Frontend"; Flags: recursesubdirs ignoreversion
Source: "staging\Ml\*"; DestDir: "{app}\Ml"; Flags: recursesubdirs ignoreversion
Source: "setup-ml-venv.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\.env.example"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{app}\database"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\Desktop\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Desktop\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; \
    Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\setup-ml-venv.ps1"" -MlDirectory ""{app}\Ml"""; \
    StatusMsg: "Setting up the Python ML service (this can take a few minutes)…"; \
    Flags: waituntilterminated
Filename: "{app}\Desktop\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Ml\.venv"
Type: filesandordirs; Name: "{app}\database"
