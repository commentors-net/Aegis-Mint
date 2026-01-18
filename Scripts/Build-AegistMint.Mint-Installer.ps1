param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$AppVersion = "0.1.0",
    [string]$ServiceName = "AegisMintService", # retained for backward compat, unused now
    [string]$PublishDir = "$PSScriptRoot\publish\service", # unused now
    [string]$OutputDir = "$PSScriptRoot\dist",
    [bool]$SelfContained = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$mintProject = Join-Path $repoRoot "Mint\src\AegisMint.Mint\AegisMint.Mint.csproj"

if (-not (Test-Path $mintProject)) {
    throw "Cannot find mint app project at $mintProject"
}

# Helpers: version management
function Get-ProjectVersion([string]$csprojPath) {
    $xml = [xml](Get-Content $csprojPath)
    $pgs = $xml.Project.PropertyGroup
    if (-not $pgs) { throw "No PropertyGroup found in $csprojPath" }

    $verNode = $pgs | ForEach-Object { $_.SelectSingleNode("Version") } | Where-Object { $_ } | Select-Object -First 1
    if ($verNode -and -not [string]::IsNullOrWhiteSpace($verNode.InnerText)) {
        return $verNode.InnerText.Trim()
    }
    return "0.1.0"
}

function Set-ProjectVersion([string]$csprojPath, [string]$version) {
    $xml = [xml](Get-Content $csprojPath)
    $pg = $xml.Project.PropertyGroup | Select-Object -First 1
    if (-not $pg) {
        $pg = $xml.CreateElement("PropertyGroup")
        $null = $xml.Project.AppendChild($pg)
    }
    $verNode = $pg.SelectSingleNode("Version")
    if (-not $verNode) {
        $verNode = $xml.CreateElement("Version")
        $null = $pg.AppendChild($verNode)
    }
    $verNode.InnerText = $version
    $xml.Save($csprojPath)
}

function Bump-Patch([string]$version) {
    try {
        $v = [version]$version
        $patch = if ($v.Build -lt 0) { 1 } else { $v.Build + 1 }
        return "{0}.{1}.{2}" -f $v.Major, $v.Minor, $patch
    } catch {
        throw "Invalid version format: $version"
    }
}

$currentVersion = Get-ProjectVersion $mintProject
Write-Host "Current AegisMint.Mint version: $currentVersion" -ForegroundColor Yellow
$inputVersion = Read-Host "Enter new version (blank to bump patch)"

if ([string]::IsNullOrWhiteSpace($inputVersion)) {
    $AppVersion = Bump-Patch $currentVersion
    Write-Host "No version entered. Bumping patch to $AppVersion" -ForegroundColor Cyan
} else {
    # validate
    try {
        $null = [version]$inputVersion
        $AppVersion = $inputVersion.Trim()
    } catch {
        throw "Provided version '$inputVersion' is not valid."
    }
    Write-Host "Using provided version: $AppVersion" -ForegroundColor Cyan
}

# Persist the chosen version back into the project for consistency
Set-ProjectVersion $mintProject $AppVersion

Write-Host "Publishing mint app..." -ForegroundColor Cyan
$mintPublishDir = "$PSScriptRoot\publish\mint"
if (Test-Path $mintPublishDir) { Remove-Item $mintPublishDir -Recurse -Force }
dotnet publish $mintProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained:$SelfContained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $mintPublishDir

# Ask if this is a production build
Write-Host ""
Write-Host "Configuration Setup" -ForegroundColor Yellow
Write-Host "==================" -ForegroundColor Yellow
$isProduction = Read-Host "Is this a PRODUCTION build? (yes/no)"

if ($isProduction -match "^(y|yes)$") {
    Write-Host "Using PRODUCTION configuration (https://apkserve.com/govern/)" -ForegroundColor Green
    
    # Look for production config in source directory (since PublishSingleFile might not include it)
    $sourceProjectDir = Split-Path -Parent $mintProject
    $sourceProdConfig = Join-Path $sourceProjectDir "appsettings.json.production"
    $targetConfig = Join-Path $mintPublishDir "appsettings.json"
    
    if (Test-Path $sourceProdConfig) {
        Copy-Item $sourceProdConfig $targetConfig -Force
        Write-Host "Production config applied successfully from source" -ForegroundColor Green
    } else {
        # Fallback: try in publish directory
        $prodConfig = Join-Path $mintPublishDir "appsettings.json.production"
        if (Test-Path $prodConfig) {
            Copy-Item $prodConfig $targetConfig -Force
            Write-Host "Production config applied successfully from publish dir" -ForegroundColor Green
        } else {
            Write-Warning "Production config file not found at $sourceProdConfig or $prodConfig"
            Write-Warning "The application will use development settings (localhost)!"
        }
    }
} else {
    Write-Host "Using DEVELOPMENT configuration (http://localhost:8000)" -ForegroundColor Cyan
}

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }

$innoCandidates = @(
    $env:INNOSETUP_PATH,
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles} "Inno Setup 6\ISCC.exe")
) | Where-Object { $_ -and (Test-Path $_) }
$innoCandidates = @($innoCandidates) # force array even if single result

if (-not $innoCandidates) {
    throw "Inno Setup 6 (ISCC.exe) not found. Set INNOSETUP_PATH or install Inno Setup."
}

$issPath = Join-Path $PSScriptRoot "AegisMint.generated.iss"

$mintPublishDir = "$PSScriptRoot\publish\mint"
$outputBase = "AegisMint-Mint-Setup-$AppVersion"

$filesSection = @"
Source: "{#AdminSourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
"@

$runSection = @"
Filename: "{app}\AegisMint.Mint.exe"; Description: "Launch Aegis Mint"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent
"@

$iss = @"
#define AppVersion "$AppVersion"
#define AdminSourceDir "$mintPublishDir"
#define OutputDir "$OutputDir"
#define ServiceName "$ServiceName"

[Setup]
AppName=AegisMint
AppVersion={#AppVersion}
DefaultDirName={pf}\AegisMint\Mint
DefaultGroupName=AegisMint
DisableProgramGroupPage=yes
OutputBaseFilename=$outputBase
OutputDir={#OutputDir}
UninstallDisplayIcon={app}\AegisMint.Mint.exe
UninstallDisplayName=AegisMint
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes

[Files]
$filesSection

[Icons]
Name: "{group}\Aegis Mint"; Filename: "{app}\AegisMint.Mint.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\Aegis Mint"; Filename: "{app}\AegisMint.Mint.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon for Aegis Mint"

[Run]
$runSection

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
const
  MOVEFILE_DELAY_UNTIL_REBOOT = `$00000004;

type
  TProcessInfo = record
    PID: Cardinal;
    ProcessName: String;
  end;

function MoveFileEx(lpExistingFileName, lpNewFileName: String; dwFlags: DWORD): BOOL;
  external 'MoveFileExW@kernel32.dll stdcall';

var
  GlobalDeletePaths: TStringList;

function FindProcessesByName(ProcessName: String): TStringList;
var
  Processes: TStringList;
  TempFile: String;
  Lines: TStringList;
  I: Integer;
  Line: String;
  PID: String;
  Name: String;
  ResultCode: Integer;
begin
  Processes := TStringList.Create;
  TempFile := ExpandConstant('{tmp}\processes.txt');
  
  try
    // Run tasklist and save to temp file
    if Exec('cmd.exe', '/c tasklist /FO CSV /NH | findstr /I "' + ProcessName + '" > "' + TempFile + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if FileExists(TempFile) then
      begin
        Lines := TStringList.Create;
        try
          Lines.LoadFromFile(TempFile);
          for I := 0 to Lines.Count - 1 do
          begin
            Line := Lines[I];
            if Length(Line) > 0 then
            begin
              // Parse CSV format: "name","PID","Session Name","Session#","Mem Usage"
              // Extract name (first quoted field)
              if Pos('"', Line) = 1 then
              begin
                Delete(Line, 1, 1); // Remove first quote
                Name := Copy(Line, 1, Pos('"', Line) - 1);
                Delete(Line, 1, Pos('"', Line)); // Remove name and quote
                // Extract PID (second quoted field)
                if Pos('"', Line) > 0 then
                begin
                  Delete(Line, 1, Pos('"', Line)); // Skip to PID
                  PID := Copy(Line, 1, Pos('"', Line) - 1);
                  Processes.Add(PID + '|' + Name);
                  Log('Found process: ' + Name + ' (PID: ' + PID + ')');
                end;
              end;
            end;
          end;
        finally
          Lines.Free;
          DeleteFile(TempFile);
        end;
      end;
    end;
  except
    Log('Error querying processes via tasklist');
  end;
  
  Result := Processes;
end;

function KillProcessByPID(PID: Cardinal): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('taskkill', '/F /PID ' + IntToStr(PID), '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if Result and (ResultCode = 0) then
    Log('Successfully killed process PID: ' + IntToStr(PID))
  else
    Log('Failed to kill process PID: ' + IntToStr(PID));
end;

function IsDirectoryEmpty(DirPath: String): Boolean;
var
  FindRec: TFindRec;
  Count: Integer;
begin
  Count := 0;
  if FindFirst(AddBackslash(DirPath) + '*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
          Count := Count + 1;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
  Result := (Count = 0);
end;

function ScheduleDeleteOnReboot(Path: String): Boolean;
var
  FindRec: TFindRec;
  SubPath: String;
begin
  Result := True;
  if DirExists(Path) then
  begin
    if FindFirst(AddBackslash(Path) + '*', FindRec) then
    begin
      try
        repeat
          if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
          begin
            SubPath := AddBackslash(Path) + FindRec.Name;
            if FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0 then
              ScheduleDeleteOnReboot(SubPath)
            else
              MoveFileEx(SubPath, '', MOVEFILE_DELAY_UNTIL_REBOOT);
          end;
        until not FindNext(FindRec);
      finally
        FindClose(FindRec);
      end;
    end;
    MoveFileEx(Path, '', MOVEFILE_DELAY_UNTIL_REBOOT);
    GlobalDeletePaths.Add(Path);
  end
  else if FileExists(Path) then
  begin
    MoveFileEx(Path, '', MOVEFILE_DELAY_UNTIL_REBOOT);
    GlobalDeletePaths.Add(Path);
  end;
end;

function AttemptDelete(Path: String; MaxRetries: Integer): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to MaxRetries do
  begin
    if DirExists(Path) then
    begin
      if DelTree(Path, True, True, True) then
      begin
        Log('Successfully deleted: ' + Path);
        Result := True;
        Break;
      end;
    end
    else if FileExists(Path) then
    begin
      if DeleteFile(Path) then
      begin
        Log('Successfully deleted file: ' + Path);
        Result := True;
        Break;
      end;
    end
    else
    begin
      Result := True;
      Break;
    end;
    
    if I < MaxRetries then
    begin
      Log('Retry ' + IntToStr(I) + ' failed, waiting 500ms...');
      Sleep(500);
    end;
  end;
end;

function InitializeUninstall(): Boolean;
var
  Processes: TStringList;
  ProcessInfo: String;
  PID: Cardinal;
  ProcessName: String;
  I, SepPos: Integer;
  LockedMessage: String;
begin
  Result := True;
  GlobalDeletePaths := TStringList.Create;
  
  Log('Checking for running processes before uninstall');
  
  // Check for running Aegis Mint processes BEFORE Windows shows uninstall confirmation
  LockedMessage := '';
  Processes := FindProcessesByName('AegisMint');
  try
    if Processes.Count > 0 then
    begin
      LockedMessage := 'The following AegisMint processes are currently running:' + #13#10#13#10;
      for I := 0 to Processes.Count - 1 do
      begin
        ProcessInfo := Processes[I];
        SepPos := Pos('|', ProcessInfo);
        if SepPos > 0 then
        begin
          PID := StrToIntDef(Copy(ProcessInfo, 1, SepPos - 1), 0);
          ProcessName := Copy(ProcessInfo, SepPos + 1, Length(ProcessInfo));
          LockedMessage := LockedMessage + '  • ' + ProcessName + ' (PID: ' + IntToStr(PID) + ')' + #13#10;
        end;
      end;
      LockedMessage := LockedMessage + #13#10 + 'These processes must be closed before uninstalling.' + #13#10 + 'Click OK to automatically close them, or Cancel to abort uninstallation.';
      
      if MsgBox(LockedMessage, mbConfirmation, MB_OKCANCEL) = IDOK then
      begin
        Log('User confirmed process termination');
        for I := 0 to Processes.Count - 1 do
        begin
          ProcessInfo := Processes[I];
          SepPos := Pos('|', ProcessInfo);
          if SepPos > 0 then
          begin
            PID := StrToIntDef(Copy(ProcessInfo, 1, SepPos - 1), 0);
            if PID > 0 then
            begin
              KillProcessByPID(PID);
              Sleep(500);
            end;
          end;
        end;
      end
      else
      begin
        Log('User cancelled uninstallation');
        Result := False;
        Exit;
      end;
    end;
  finally
    Processes.Free;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataPath: String;
  DeleteData: Boolean;
  NeedReboot: Boolean;
begin
  if CurUninstallStep = usUninstall then
  begin
    // This runs AFTER user confirms uninstall in Windows dialog
    Log('Starting file deletion process');
    NeedReboot := False;
    
    // Ask about deleting application data
    DeleteData := (MsgBox('Do you want to delete all application data?' + #13#10 + '(This includes vault database and encryption keys)' + #13#10#13#10 + 'WARNING: This action cannot be undone!', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES);
    
    if DeleteData then
    begin
      Log('User confirmed data deletion');
      
      // Delete AppData folder
      DataPath := ExpandConstant('{localappdata}\AegisMint');
      if DirExists(DataPath) then
      begin
        Log('Attempting to delete AppData: ' + DataPath);
        if not AttemptDelete(DataPath, 3) then
        begin
          Log('Failed to delete AppData, scheduling for reboot');
          ScheduleDeleteOnReboot(DataPath);
          NeedReboot := True;
        end;
      end;
    end;
    
    // Note: Installation folder is automatically deleted by [UninstallDelete] section
    Log('Installation folder will be removed by Inno Setup');
    
    // Notify user if reboot is needed
    if NeedReboot then
    begin
      MsgBox('Some files could not be deleted because they are in use.' + #13#10#13#10 + 'They have been scheduled for deletion after system restart.' + #13#10#13#10 + 'Please restart your computer to complete the uninstallation.', mbInformation, MB_OK);
    end
    else
    begin
      Log('Uninstallation completed successfully without requiring reboot');
    end;
  end;
end;

procedure DeinitializeUninstall();
begin
  if Assigned(GlobalDeletePaths) then
    GlobalDeletePaths.Free;
end;
"@

$iss | Set-Content -Path $issPath -Encoding UTF8

Write-Host "Building installer with Inno Setup..." -ForegroundColor Cyan
$iscc = $innoCandidates | Select-Object -First 1
Write-Host "Using ISCC: $iscc" -ForegroundColor Gray
& "$iscc" "/Q" $issPath

Write-Host "Installer build complete. Output: $OutputDir" -ForegroundColor Green
