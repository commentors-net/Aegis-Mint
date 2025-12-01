# Aegis & Mint System Requirements (Updated)

## 1. Overview
Defines functional, non-functional, governance, and security requirements for AegisMint (Windows service), Aegis Web (governance/recovery), and TokenControl integration. TokenControl exists; AegisMint provides the mnemonic when unlocked; Aegis Web governs unlocks.

---

## 2. Definitions
- **Genesis Key**: 12-word mnemonic generated once by Mint; master secret.
- **Shares**: Shamir Secret Sharing fragments of the Genesis key for recovery (M holders, N required).
- **Governance**: Aegis Web N-of-M approvals, independent of recovery shares.
- **Device (Box)**: Hardware running Mint service + TokenControl.

---

## 3. Functional Requirements

### 3.1 Mint Service
1) Generate a single 12-word Genesis key on first run; never expose unless unlocked.
2) Generate Shamir shares per config (ShareCount M, RecoveryThreshold N); persist securely.
3) Encrypt mnemonic at rest (AES-256-GCM with DPAPI-protected master key).
4) Expose localhost API:
   - `GET /ping`
   - `GET /getDeviceInfo`
   - `GET /getMnemonic` (only when unlocked)
   - `POST /governance/unlock/dev` (dev only, config gated)
   - `POST /governance/lock`
   - `GET /logs/recent?limit=N` (log tail)
5) Configurable: data directory, share counts/threshold, governance quorum, unlock window, log path, port, HTTPS toggle, dev-unlock flag.
6) Runs continuously as a Windows service; starts automatically on boot.

### 3.2 TokenControl
1) Never prompt for mnemonic (except emergency recovery).
2) On startup, call Aegis Web: `isUnlocked(deviceId)`.
3) If unlocked, call Mint `/getMnemonic`; else show “Waiting for Governance Approval” and poll.
4) Log usage events to Aegis Web (startup/unlock/shutdown/ops optional).

### 3.3 Aegis Web Governance
1) Maintain governors (M), approval threshold (N), unlock duration (default 15 minutes).
2) Collect approvals with reason/timestamp; set device unlock window on success.
3) Log: approvals, unlock events, TokenControl usage, remote lock events.
4) Allow remote lock any time.
5) Dashboard: device status, logs, unlock windows, governors.

### 3.4 Recovery UI (Web & Standalone)
1) Accept N shares; validate format/count.
2) Reconstruct Genesis key; secure display/export only.
3) Require admin authentication.
4) Provide web UI and downloadable offline executable.

---

## 4. Non-Functional Requirements
- **Security**: Mnemonic never logged; AES-GCM at rest; Argon2 for secrets; localhost-only API; HTTPS when cert is configured; no plaintext storage.
- **Reliability**: Survives restarts; service auto-start; data directory preserved.
- **Performance**: Unlock check < 500 ms; Mint API < 300 ms typical.
- **Usability**: Simple governance UI; clear recovery steps.

---

## 5. Constraints
- Mint runs only on the device.
- TokenControl must not rely on shares for normal operation.
- Governance separate from recovery.

---

## 6. Open Questions
- Device identity source (HW ID/cert).
- Aegis Web redundancy/backups.
- HTTPS certificate provisioning/rotation for Mint if/when enabled.
