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
$tokenControlProject = Join-Path $repoRoot "Mint\src\AegisMint.TokenControl\AegisMint.TokenControl.csproj"

if (-not (Test-Path $tokenControlProject)) {
    throw "Cannot find token control app project at $tokenControlProject"
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

$currentVersion = Get-ProjectVersion $tokenControlProject
Write-Host "Current AegisMint.TokenControl version: $currentVersion" -ForegroundColor Yellow
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
Set-ProjectVersion $tokenControlProject $AppVersion

Write-Host "Publishing token control app..." -ForegroundColor Cyan
$tokenControlPublishDir = "$PSScriptRoot\publish\tokencontrol"
if (Test-Path $tokenControlPublishDir) { Remove-Item $tokenControlPublishDir -Recurse -Force }
dotnet publish $tokenControlProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained:$SelfContained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $tokenControlPublishDir

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

$issPath = Join-Path $PSScriptRoot "AegisMint.TokenControl.generated.iss"

$tokenControlPublishDir = "$PSScriptRoot\publish\tokencontrol"
$outputBase = "AegisMint-TokenControl-Setup-$AppVersion"

$filesSection = @"
Source: "{#AdminSourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
"@

$runSection = @"
Filename: "{app}\AegisMint.TokenControl.exe"; Description: "Launch Aegis Mint Token Control"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent
"@

$iss = @"
#define AppVersion "$AppVersion"
#define AdminSourceDir "$tokenControlPublishDir"
#define OutputDir "$OutputDir"
#define ServiceName "$ServiceName"

[Setup]
AppName=AegisMint Token Control
AppVersion={#AppVersion}
DefaultDirName={pf}\AegisMint\TokenControl
DefaultGroupName=AegisMint
DisableProgramGroupPage=yes
OutputBaseFilename=$outputBase
OutputDir={#OutputDir}
UninstallDisplayIcon={app}\AegisMint.TokenControl.exe
UninstallDisplayName=AegisMint Token Control
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes

[Files]
$filesSection

[Icons]
Name: "{group}\Aegis Mint Token Control"; Filename: "{app}\AegisMint.TokenControl.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\Aegis Mint Token Control"; Filename: "{app}\AegisMint.TokenControl.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon for Aegis Mint Token Control"

[Run]
$runSection

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
"@

$iss | Set-Content -Path $issPath -Encoding UTF8

Write-Host "Building installer with Inno Setup..." -ForegroundColor Cyan
$iscc = $innoCandidates | Select-Object -First 1
Write-Host "Using ISCC: $iscc" -ForegroundColor Gray
& "$iscc" "/Q" $issPath

Write-Host "Installer build complete. Output: $OutputDir" -ForegroundColor Green
