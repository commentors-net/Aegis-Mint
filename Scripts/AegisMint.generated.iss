#define AppVersion "0.1.0"
#define ServiceSourceDir "D:\Jobs\workspace\DiG\Aegis-Mint\Scripts\publish\service"
#define AdminSourceDir "D:\Jobs\workspace\DiG\Aegis-Mint\Scripts\publish\admin"
#define OutputDir "D:\Jobs\workspace\DiG\Aegis-Mint\Scripts\dist"
#define ServiceName "AegisMintService"

[Setup]
AppName=AegisMint
AppVersion={#AppVersion}
DefaultDirName={pf}\AegisMint
DefaultGroupName=AegisMint
DisableProgramGroupPage=yes
OutputBaseFilename=AegisMint-Setup
OutputDir={#OutputDir}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes

[Files]
Source: "{#ServiceSourceDir}\*"; DestDir: "{app}\Service"; Flags: recursesubdirs ignoreversion
Source: "{#AdminSourceDir}\*"; DestDir: "{app}\Admin"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\AegisMint Admin"; Filename: "{app}\Admin\AegisMint.AdminApp.exe"; WorkingDir: "{app}\Admin"
Name: "{commondesktop}\AegisMint Admin"; Filename: "{app}\Admin\AegisMint.AdminApp.exe"; WorkingDir: "{app}\Admin"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon for AegisMint Admin"

[Run]
Filename: "cmd.exe"; Parameters: "/c sc stop ""{#ServiceName}"" 1>nul 2>nul"; Flags: runhidden waituntilterminated
Filename: "cmd.exe"; Parameters: "/c sc delete ""{#ServiceName}"" 1>nul 2>nul"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "create ""{#ServiceName}"" binPath= ""{app}\Service\AegisMint.Service.exe"" start= auto displayname= ""AegisMint Service"" obj= LocalSystem"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "start ""{#ServiceName}"""; Flags: runhidden waituntilterminated; StatusMsg: "Starting AegisMint Service..."
Filename: "{app}\Admin\AegisMint.AdminApp.exe"; Description: "Launch AegisMint Admin"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "cmd.exe"; Parameters: "/c sc stop ""{#ServiceName}"" 1>nul 2>nul"; Flags: runhidden waituntilterminated
Filename: "cmd.exe"; Parameters: "/c sc delete ""{#ServiceName}"" 1>nul 2>nul"; Flags: runhidden waituntilterminated

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
