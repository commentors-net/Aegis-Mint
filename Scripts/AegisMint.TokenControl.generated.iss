#define AppVersion "1.0.3"
#define AdminSourceDir "D:\Jobs\workspace\DiG\Aegis-Mint\Scripts\publish\tokencontrol"
#define OutputDir "D:\Jobs\workspace\DiG\Aegis-Mint\Scripts\dist"
#define ServiceName "AegisMintService"

[Setup]
AppName=AegisMint Token Control
AppVersion={#AppVersion}
DefaultDirName={pf}\AegisMint\TokenControl
DefaultGroupName=AegisMint
DisableProgramGroupPage=yes
OutputBaseFilename=AegisMint-TokenControl-Setup-1.0.3
OutputDir={#OutputDir}
UninstallDisplayIcon={app}\AegisMint.TokenControl.exe
UninstallDisplayName=AegisMint Token Control
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes

[Files]
Source: "{#AdminSourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\Aegis Mint Token Control"; Filename: "{app}\AegisMint.TokenControl.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\Aegis Mint Token Control"; Filename: "{app}\AegisMint.TokenControl.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon for Aegis Mint Token Control"

[Run]
Filename: "{app}\AegisMint.TokenControl.exe"; Description: "Launch Aegis Mint Token Control"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function InitializeUninstall(): Boolean;
var
  DataPath: String;
  ResultCode: Integer;
begin
  Result := True;
  
  // Ask user if they want to delete application data
  if MsgBox('Do you want to delete all application data (including vault database and encryption keys)?', mbConfirmation, MB_YESNO) = IDYES then
  begin
    DataPath := ExpandConstant('{localappdata}\AegisMint');
    if DirExists(DataPath) then
    begin
      if DelTree(DataPath, True, True, True) then
        Log('Successfully deleted application data folder: ' + DataPath)
      else
        MsgBox('Warning: Could not delete application data folder at: ' + DataPath + #13#10 + 'You may need to delete it manually.', mbInformation, MB_OK);
    end;
  end;
end;
