#define AppName "Polly"
#define AppVersion "1.0.0"
#define AppPublisher "Revani"
#define RepoDir "<REPO_DIR>"
#define SourceDir RepoDir + "\publish\Polly-win-x64"
#define IconPath RepoDir + "\MacroMaster.WinForms\Resources\Polly.ico"

[Setup]
AppId={{B2D8F73E-4B6A-4DA4-9C90-1E78A71EAA21}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/EnesGoker/MacroMaster
AppSupportURL=https://github.com/EnesGoker/MacroMaster
AppUpdatesURL=https://github.com/EnesGoker/MacroMaster
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#RepoDir}\installer\output
OutputBaseFilename=Polly_Setup_v{#AppVersion}
SetupIconFile={#IconPath}
UninstallDisplayIcon={app}\Polly.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Ek kısayollar:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Polly"; Filename: "{app}\Polly.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Polly.exe"
Name: "{autodesktop}\Polly"; Filename: "{app}\Polly.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Polly.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Polly.exe"; Description: "Polly uygulamasını başlat"; Flags: nowait postinstall skipifsilent