# Aegis + Mint Architecture (Updated)

## 1. High-Level View
```
+--------------+       +------------------+        +----------------+
| TokenControl | <---> |  AegisMint Box   | <----> |   Aegis Web    |
|   (on box)   |       |  (Local API)     |        |  (Governance)  |
+--------------+       +------------------+        +----------------+
```
Local box hosts the Mint Windows service (localhost API), TokenControl, and optional AdminApp. Aegis Web governs unlocks and provides recovery UI.

---

## 2. Components

### AegisMint Service (Windows, localhost)
- Background Windows service; auto-start on boot.
- Responsibilities: generate/store Genesis key; encrypt at rest (AES-GCM + DPAPI); generate/persist Shamir shares; serve internal API.
- Endpoints:
  - `GET /ping`
  - `GET /getDeviceInfo`
  - `GET /getMnemonic` (requires unlock)
  - `POST /governance/unlock/dev` (dev only, config gated)
  - `POST /governance/lock`
  - `GET /logs/recent?limit=N`
- Governance state held in-memory; production unlocks should be driven by Aegis Web.
- Config via `appsettings*.json`: data dir, share/thresholds, governance quorum, unlock window, log path, port, HTTPS toggle, dev unlock.

### TokenControl
- User-facing app on the box.
- Flow: call Aegis Web `isUnlocked(deviceId)` → if true call Mint `getMnemonic` → start session; otherwise show waiting/poll.

### AdminApp (WPF)
- Local admin utility to ping service, view device info, dev-unlock/lock, request mnemonic (word-count only), and view recent logs.

### Aegis Web (Governance & Recovery)
- N-of-M approvals, unlock windows, device metadata, logs.
- Provides `isUnlocked(deviceId)` for TokenControl.
- Recovery UI + downloadable offline recovery tool (future).

---

## 3. Governance Unlock Flow
```
Governors (N of M) -> Aegis Web -> sets unlock window (e.g., 15 min)
TokenControl -> Mint: getMnemonic (only succeeds while unlocked)
```
Governance is independent from recovery shares.

---

## 4. Recovery Flow
```
User enters N shares
 -> Shamir reconstruction
 -> Genesis restored
 -> Secure display/export (no logging)
```
Works even if other services are down.

---

## 5. Data in Mint
| Data            | Description                | Access                |
|-----------------|----------------------------|-----------------------|
| Genesis key     | 12-word mnemonic           | Mint (unlocked) / recovery |
| Shamir shares   | Split fragments (M/N)      | Printed/persisted     |
| Device config   | Thresholds, counts, quorum | Mint + Aegis Web      |
| Master key      | DPAPI-protected key bytes  | Mint only             |

---

## 6. Implementation Notes
- .NET 8 Windows service; localhost binding (HTTP by default; HTTPS optional with cert).
- AES-256-GCM for mnemonic at rest; DPAPI LocalMachine for master key.
- Shamir Secret Sharing implemented over GF(256).
- Logging: file-based; exposed via `/logs/recent`.
- Installer: Inno Setup + PowerShell script; creates/starts Windows service; uninstall stops/deletes service and removes install directory.
