#define AppName "Makredo"
#define AppVersion "1.0.0"
#define AppPublisher "Enes Göker"
#define RepoDir "<REPO_DIR>"
#define SourceDir RepoDir + "\publish\Makredo-win-x64"
#define IconPath RepoDir + "\src\Makredo.WinForms\Resources\Makredo.ico"

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
OutputBaseFilename=Makredo_Setup_v{#AppVersion}
SetupIconFile={#IconPath}
UninstallDisplayIcon={app}\Makredo.exe
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
Name: "{group}\Makredo"; Filename: "{app}\Makredo.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Makredo.exe"
Name: "{autodesktop}\Makredo"; Filename: "{app}\Makredo.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Makredo.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Makredo.exe"; Description: "Makredo uygulamasını başlat"; Flags: nowait postinstall skipifsilent
