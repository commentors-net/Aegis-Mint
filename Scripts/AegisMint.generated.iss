#define AppVersion "1.0.5"
#define AdminSourceDir "D:\Jobs\workspace\DiG\Aegis-Mint\Scripts\publish\mint"
#define OutputDir "D:\Jobs\workspace\DiG\Aegis-Mint\Scripts\dist"
#define ServiceName "AegisMintService"

[Setup]
AppName=AegisMint
AppVersion={#AppVersion}
DefaultDirName={pf}\AegisMint
DefaultGroupName=AegisMint
DisableProgramGroupPage=yes
OutputBaseFilename=AegisMint-Mint-Setup-1.0.5
OutputDir={#OutputDir}
UninstallDisplayIcon={app}\Mint\AegisMint.Mint.exe
UninstallDisplayName=AegisMint
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes

[Files]
Source: "{#AdminSourceDir}\*"; DestDir: "{app}\Mint"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\Aegis Mint"; Filename: "{app}\Mint\AegisMint.Mint.exe"; WorkingDir: "{app}\Mint"
Name: "{commondesktop}\Aegis Mint"; Filename: "{app}\Mint\AegisMint.Mint.exe"; WorkingDir: "{app}\Mint"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon for Aegis Mint"

[Run]
Filename: "{app}\Mint\AegisMint.Mint.exe"; Description: "Launch Aegis Mint"; WorkingDir: "{app}\Mint"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "HKCU\Software\AegisMint"
Type: filesandordirs; Name: "HKLM\Software\AegisMint"
