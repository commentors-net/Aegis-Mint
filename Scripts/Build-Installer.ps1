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

if (-not (Test-Path $serviceProject)) {
    throw "Cannot find service project at $serviceProject"
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

$iss = @"
#define AppVersion "$AppVersion"
#define SourceDir "$PublishDir"
#define OutputDir "$OutputDir"
#define ServiceName "$ServiceName"

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
"@

$iss | Set-Content -Path $issPath -Encoding UTF8

Write-Host "Building installer with Inno Setup..." -ForegroundColor Cyan
$iscc = $innoCandidates | Select-Object -First 1
Write-Host "Using ISCC: $iscc" -ForegroundColor Gray
& "$iscc" "/Q" $issPath

Write-Host "Installer build complete. Output: $OutputDir" -ForegroundColor Green
