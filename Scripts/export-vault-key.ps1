param(
    [string]$OutputHexFile = "",
    [switch]$ShowKey
)

if (-not ([System.Management.Automation.PSTypeName]'System.Security.Cryptography.ProtectedData').Type) {
    Add-Type -AssemblyName System.Security
}

$baseDir = Join-Path $env:LOCALAPPDATA "AegisMint\Data"
$keyPath = Join-Path $baseDir "vault.key"
$dbPath = Join-Path $baseDir "vault.db"

if (-not (Test-Path $keyPath)) {
    Write-Error "Key file not found at $keyPath"
    exit 1
}
if (-not (Test-Path $dbPath)) {
    Write-Warning "Database file not found at $dbPath"
}

try {
    $bytes = [System.IO.File]::ReadAllBytes($keyPath)
    $unprotected = [System.Security.Cryptography.ProtectedData]::Unprotect($bytes, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    $hex = ($unprotected | ForEach-Object { $_.ToString("x2") }) -join ''
}
catch {
    Write-Error "Failed to unprotect key. Run as the Windows user who created the vault. $_"
    exit 1
}

if ($ShowKey) {
    Write-Host "Raw hex key (keep secret):"
    Write-Host $hex
}

if ($OutputHexFile) {
    $dir = Split-Path $OutputHexFile
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
    Set-Content -Path $OutputHexFile -Value $hex -NoNewline
    Write-Host "Hex key written to $OutputHexFile"
}

Write-Host "Database path: $dbPath"
Write-Host "To open with SQLCipher sqlite3:"
Write-Host '  sqlite3.exe "'$dbPath'"'
Write-Host '  PRAGMA key = "x'"$hex"'";'
Write-Host '  PRAGMA cipher_compatibility = 4;'
Write-Host '  PRAGMA kdf_iter = 256000;'
Write-Host '  .tables'
