# AegisMint.Core and AegisMint.Service Overview

## AegisMint.Core (shared library)
Purpose: core domain, security, and storage utilities used by the service, client SDK, and admin UI.
- **Config**: `MintOptions` (data directory, share counts, thresholds, device id, governance quorum, unlock window).
- **Models/Contracts**: device metadata, mnemonic responses, logs response, Shamir shares.
- **Security**:
  - AES-256-GCM envelope for on-disk encryption of the mnemonic.
  - DPAPI-protected master key store (`FileProtectedKeyStore`) scoped to LocalMachine.
  - Argon2 password hasher utility.
  - Shamir Secret Sharing implementation (GF(256)) for generating recovery shares.
- **Vault**: `GenesisVault` generates and stores the 12-word mnemonic, enforces thresholds, persists shares, and surfaces device info.

## AegisMint.Service (Windows service with localhost API)
Purpose: host the internal API that TokenControl/admin tools call to interact with the Mint vault, while keeping secrets on the box.
- **Hosting**: .NET 8 Windows service (localhost binding; HTTPS by default).
- **Endpoints**:
  - `GET /ping` → health
  - `GET /getDeviceInfo` → device metadata (share counts, quorum, unlock window, device id)
  - `GET /getMnemonic` → returns mnemonic only when unlocked
  - `POST /governance/unlock/dev` → dev-only unlock (config gated)
  - `POST /governance/lock` → lock immediately
  - `GET /logs/recent?limit=N` → log tail
- **State**: governance lock/unlock is tracked in-memory (`GovernanceState`). Dev unlock is optional; production should use Aegis Web governance signals.
- **Storage**: mnemonic encrypted at rest using AES-GCM with DPAPI-protected master key; shares persisted for recovery per configured thresholds.
- **Logging**: file-based logger configured via `Service.LogFilePath`; also writes to console; logs are surfaced via `/logs/recent`.

## Installer/Deployment Notes
- PowerShell script `Scripts/Build-Installer.ps1` publishes the service and builds an Inno Setup installer that creates/starts a Windows service (`sc create/start`) and removes it on uninstall.
- Default install directory: Program Files\AegisMint (configurable via Inno script if needed).
- Service config (`appsettings*.json`) controls port, HTTPS, dev unlock, log path, and Mint options.
