#define AppName "Makredo"
#define AppVersion "1.0.0"
#define AppPublisher "Enes Göker"
#define RepoDir "<REPO_DIR>"
#define SourceDir RepoDir + "\publish\" + AppName + "-win-x64"
#define IconPath RepoDir + "\src\Makredo.WinForms\Resources\" + AppName + ".ico"

[Setup]
AppId={{B2D8F73E-4B6A-4DA4-9C90-1E78A71EAA21}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/EnesGoker/Makredo
AppSupportURL=https://github.com/EnesGoker/Makredo
AppUpdatesURL=https://github.com/EnesGoker/Makredo
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#RepoDir}\installer\output
OutputBaseFilename={#AppName}_Setup_v{#AppVersion}
SetupIconFile={#IconPath}
UninstallDisplayIcon={app}\{#AppName}.exe
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
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppName}.exe"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppName}.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppName}.exe"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppName}.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppName}.exe"; Description: "{#AppName} uygulamasını başlat"; Flags: nowait postinstall skipifsilent
