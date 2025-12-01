# instructions.md — AI Coding Assistant Guidelines

This document keeps AI coding assistants aligned with the current AegisMint/Aegis Web implementation and security rules.

---

## 1. Purpose
Assist development for:
- **AegisMint**: Windows service (localhost API), WPF AdminApp, .NET client SDK.
- **Aegis Web**: governance + recovery (future Python/React work).

TokenControl exists already; only integrate with its APIs. AdminApp must never display or log the mnemonic.

---

## 2. Security Rules
- Never print/log the Genesis key except inside a dedicated recovery flow.
- Never store plaintext mnemonics outside the vault; keep AES-GCM encryption at rest with DPAPI-protected master key.
- No hardcoded secrets or backdoors.
- Password hashing must use Argon2.

---

## 3. AegisMint Service Conventions (current)
- Host: Windows service, localhost-only. Default HTTP; HTTPS only when a cert is provisioned.
- Endpoints:
  - `GET /ping`
  - `GET /getDeviceInfo`
  - `GET /getMnemonic` (only when governance unlock is active)
  - `POST /governance/unlock/dev` (dev only; gated by config)
  - `POST /governance/lock`
  - `GET /logs/recent?limit=N` (tail service logs)
- Vault: AES-256-GCM + DPAPI master key; Shamir share generation per configured M/N thresholds.
- Config: `appsettings*.json` (data dir, thresholds, governance quorum, unlock window, dev unlock flag, log path, port/https toggle).

---

## 4. Aegis Web Expectations
- Governance: N-of-M approvals, default unlock window 15 minutes.
- CRUD for governors, approvals, unlock windows, logging, recovery UI.
- TokenControl checks `isUnlocked(deviceId)` from Aegis Web before asking Mint for the mnemonic.

---

## 5. Recovery Guidance
- Accept exactly N Shamir shares; reconstruct via Shamir Secret Sharing.
- Securely display/export the mnemonic; do not log it.

---

## 6. Testing
- Unit tests: share reconstruction, vault persistence/encryption.
- Integration tests: unlock/lock flow, mnemonic access only when unlocked.
- Security tests: unauthorized access attempts to `/getMnemonic`.

---

## 7. Do Not Touch
- TokenControl internal UI logic.
- Blockchain interaction logic.
- Any Genesis exposure outside controlled recovery or unlocked Mint API.

---

## 8. Future Extensions
- Multi-device governance, multi-region Aegis servers.
- Share export (QR/NFC) with the same secrecy constraints.
