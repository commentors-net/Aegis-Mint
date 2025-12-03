#define AppVersion "0.1.0"
#define ServiceSourceDir "D:\Jobs\workspace\DiG\Aegis-Mint\Scripts\publish\service"
#define AdminSourceDir "D:\Jobs\workspace\DiG\Aegis-Mint\Scripts\publish\mint"
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
Source: "{#AdminSourceDir}\*"; DestDir: "{app}\Mint"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\Aegis Mint"; Filename: "{app}\Mint\AegisMint.Mint.exe"; WorkingDir: "{app}\Mint"
Name: "{commondesktop}\Aegis Mint"; Filename: "{app}\Mint\AegisMint.Mint.exe"; WorkingDir: "{app}\Mint"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon for Aegis Mint"

[Run]
Filename: "cmd.exe"; Parameters: "/c sc stop ""{#ServiceName}"" 1>nul 2>nul"; Flags: runhidden waituntilterminated
Filename: "cmd.exe"; Parameters: "/c sc delete ""{#ServiceName}"" 1>nul 2>nul"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "create ""{#ServiceName}"" binPath= ""{app}\Service\AegisMint.Service.exe"" start= auto displayname= ""AegisMint Service"" obj= LocalSystem"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "start ""{#ServiceName}"""; Flags: runhidden waituntilterminated; StatusMsg: "Starting AegisMint Service..."
Filename: "{app}\Mint\AegisMint.Mint.exe"; Description: "Launch Aegis Mint"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "cmd.exe"; Parameters: "/c sc stop ""{#ServiceName}"" 1>nul 2>nul"; Flags: runhidden waituntilterminated
Filename: "cmd.exe"; Parameters: "/c sc delete ""{#ServiceName}"" 1>nul 2>nul"; Flags: runhidden waituntilterminated

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
