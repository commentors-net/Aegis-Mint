# AegisMint AdminApp Manual

## Purpose
Lightweight WPF control panel for local Mint operations (dev and admin support). It should never display the mnemonic; it only confirms retrieval status.

## Prerequisites
- AegisMint.Service running on the same machine (default https://localhost:5050).
- `appsettings.json` alongside the executable (copied by the project) to override the service URL if needed.

## Actions
- **Ping**: Verifies service availability.
- **Device Info**: Reads device id, share counts, governance quorum, unlock window.
- **Unlock (dev)**: Temporary unlock via `/governance/unlock/dev` (only works when `Service.AllowDevBypassUnlock=true` in the service config). Intended for development only.
- **Lock**: Forces lock immediately.
- **Get Mnemonic**: Requests the mnemonic when unlocked; UI only reports word count to avoid exposure.
- **Refresh Logs**: Shows recent service log lines (read-only tail).

## Usage Notes
- Run the AdminApp as an administrator if the service requires elevated endpoints or if the service is installed under Program Files.
- For production, disable `AllowDevBypassUnlock` in the service config; unlock should be driven by governance signals from Aegis Web.
- Logs are read from the service via `/logs/recent`; ensure the service log path is reachable by the service account.
