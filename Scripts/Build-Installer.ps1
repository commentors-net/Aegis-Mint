param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$AppVersion = "0.1.0",
    [string]$ServiceName = "AegisMintService",
    [string]$PublishDir = "$PSScriptRoot\publish\service",
    [string]$OutputDir = "$PSScriptRoot\dist",
    [bool]$SelfContained = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$serviceProject = Join-Path $repoRoot "Mint\src\AegisMint.Service\AegisMint.Service.csproj"
$mintProject = Join-Path $repoRoot "Mint\src\AegisMint.Mint\AegisMint.Mint.csproj"

if (-not (Test-Path $serviceProject)) {
    throw "Cannot find service project at $serviceProject"
}

if (-not (Test-Path $mintProject)) {
    throw "Cannot find mint app project at $mintProject"
}

Write-Host "Publishing service..." -ForegroundColor Cyan
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
dotnet publish $serviceProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained:$SelfContained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $PublishDir

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

$iss = @"
#define AppVersion "$AppVersion"
#define ServiceSourceDir "$PublishDir"
#define AdminSourceDir "$mintPublishDir"
#define OutputDir "$OutputDir"
#define ServiceName "$ServiceName"

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
"@

$iss | Set-Content -Path $issPath -Encoding UTF8

Write-Host "Building installer with Inno Setup..." -ForegroundColor Cyan
$iscc = $innoCandidates | Select-Object -First 1
Write-Host "Using ISCC: $iscc" -ForegroundColor Gray
& "$iscc" "/Q" $issPath

Write-Host "Installer build complete. Output: $OutputDir" -ForegroundColor Green
