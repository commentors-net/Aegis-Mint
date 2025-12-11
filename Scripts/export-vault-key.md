# Exporting the AegisMint Vault SQLCipher Key

`Scripts/export-vault-key.ps1` extracts the raw SQLCipher hex key for the local vault database so you can open it with SQLCipher-enabled tools.

## Prerequisites
- Run as the same Windows user who created/uses AegisMint (DPAPI protection).
- PowerShell (pwsh or Windows PowerShell).
- SQLCipher-capable client (sqlite3 with SQLCipher or DB Browser for SQLite SQLCipher build).

## Usage
Show the key on screen:
```powershell
pwsh Scripts/export-vault-key.ps1 -ShowKey
```

Save the key to a file:
```powershell
pwsh Scripts/export-vault-key.ps1 -OutputHexFile C:\temp\vault-key.hex
```

Both options can be combined. The script also echoes the database path.

## Opening with SQLCipher sqlite3
1) Run the script and copy the hex key (no spaces, no quotes).
2) In a shell:
```powershell
cd $env:LOCALAPPDATA\AegisMint\Data
sqlite3.exe vault.db
PRAGMA key = "x'<hex_here>'";
PRAGMA cipher_compatibility = 4;
PRAGMA kdf_iter = 256000;
.tables
```

## Opening with DB Browser for SQLite (SQLCipher build)
1) File → Open Database → pick `vault.db` (path printed by the script).
2) In the SQLCipher dialog:
   - Change the dropdown from `Passphrase` to `Raw key`.
   - Paste the hex key (no spaces).
   - Select **SQLCipher 4 defaults**.
   - Leave page size, KDF iterations (256000), HMAC/KDF algorithm at defaults (shown as grayed or prefilled).
3) Click OK. Tables should load.

## Security
- The hex key is the full encryption key; anyone with it can decrypt the vault. Handle and delete any exported file once done.
- The key is DPAPI-protected per Windows user in `LocalAppData\AegisMint\Data\vault.key`.
