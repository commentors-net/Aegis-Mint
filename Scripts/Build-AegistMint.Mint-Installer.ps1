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
Source: "{#AdminSourceDir}\*"; DestDir: "{app}\Mint"; Flags: recursesubdirs ignoreversion
"@

$runSection = @"
Filename: "{app}\Mint\AegisMint.Mint.exe"; Description: "Launch Aegis Mint"; Flags: postinstall nowait skipifsilent
"@

$iss = @"
#define AppVersion "$AppVersion"
#define AdminSourceDir "$mintPublishDir"
#define OutputDir "$OutputDir"
#define ServiceName "$ServiceName"

[Setup]
AppName=AegisMint
AppVersion={#AppVersion}
DefaultDirName={pf}\AegisMint
DefaultGroupName=AegisMint
DisableProgramGroupPage=yes
OutputBaseFilename=$outputBase
OutputDir={#OutputDir}
UninstallDisplayIcon={app}\Mint\AegisMint.Mint.exe
UninstallDisplayName=AegisMint
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes

[Files]
$filesSection

[Icons]
Name: "{group}\Aegis Mint"; Filename: "{app}\Mint\AegisMint.Mint.exe"; WorkingDir: "{app}\Mint"
Name: "{commondesktop}\Aegis Mint"; Filename: "{app}\Mint\AegisMint.Mint.exe"; WorkingDir: "{app}\Mint"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon for Aegis Mint"

[Run]
$runSection

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
"@

$iss | Set-Content -Path $issPath -Encoding UTF8

Write-Host "Building installer with Inno Setup..." -ForegroundColor Cyan
$iscc = $innoCandidates | Select-Object -First 1
Write-Host "Using ISCC: $iscc" -ForegroundColor Gray
& "$iscc" "/Q" $issPath

Write-Host "Installer build complete. Output: $OutputDir" -ForegroundColor Green
