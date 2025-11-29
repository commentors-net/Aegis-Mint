#define AppVersion "0.1.0"
#define SourceDir "D:\Jobs\workspace\DiG\Aegis-Mint\scripts\publish\service"
#define OutputDir "D:\Jobs\workspace\DiG\Aegis-Mint\scripts\dist"
#define ServiceName "AegisMintService"

[Setup]
AppName=AegisMint Service
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
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Run]
Filename: "cmd.exe"; Parameters: "/c sc stop ""{#ServiceName}"" 1>nul 2>nul"; Flags: runhidden waituntilterminated
Filename: "cmd.exe"; Parameters: "/c sc delete ""{#ServiceName}"" 1>nul 2>nul"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "create ""{#ServiceName}"" binPath= ""{app}\AegisMint.Service.exe"" start= auto displayname= ""AegisMint Service"" obj= LocalSystem"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "start ""{#ServiceName}"""; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "cmd.exe"; Parameters: "/c sc stop ""{#ServiceName}"" 1>nul 2>nul"; Flags: runhidden waituntilterminated
Filename: "cmd.exe"; Parameters: "/c sc delete ""{#ServiceName}"" 1>nul 2>nul"; Flags: runhidden waituntilterminated

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
