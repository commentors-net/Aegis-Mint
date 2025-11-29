
# instructions.md — AI Coding Assistant Guidelines (OpenAI Codex / GitHub Copilot)

This document ensures AI coding assistants understand the architecture and coding rules for AegisMint and Aegis Web.

---

# 1. Purpose
Provide coding assistance for:
- **AegisMint (local service)**
- **Aegis Web (governance + recovery)**

TokenControl already exists and should not be modified beyond API integrations.

---

# 2. High-Level Implementation Rules

### 2.1 Never allow AI to generate code that:
- Prints or logs the Genesis key unless in recovery.
- Stores plaintext mnemonics.
- Hardcodes sensitive values.
- Adds hidden backdoors.

### 2.2 When generating code, always ensure:
- Encryption uses AES‑256-GCM.
- Password hashing uses Argon2.
- TLS/HTTPS is required for API communication.

---

# 3. Coding Conventions

### AegisMint Service
- Must expose internal API only on localhost.
- API methods:
  - `getMnemonic()`
  - `getDeviceInfo()`
  - `ping()`
- Encrypt Genesis key at rest.
- Allow configuration of share counts and thresholds.

### Aegis Web
- Must support:
  - CRUD for governors
  - Approvals (N-of-M)
  - Unlock windows
  - Logging
  - Recovery UI

---

# 4. Directory Structure

Suggested:

```
/Aegis-Mint
   /Mint
      /src
      /config
   /Web
      /api
      /ui
      /config
      /recovery
```

---

# 5. API Contracts

### Aegis Web → TokenControl
`GET /api/device/{id}/isUnlocked`

### TokenControl → Mint Service
`GET http://localhost:port/getMnemonic`

### Aegis Web → Recovery Flow
`POST /api/recovery/reconstruct`

---

# 6. Governance Logic Rules for AI Help

- Governance unlock requires **N approvals**.
- Unlock window must default to **15 minutes**.
- Governors must be authenticated.
- All actions logged.

---

# 7. Recovery Instructions for AI Help

- Accept exactly N shares.
- Use Shamir Secret Sharing library.
- Reconstruct Genesis key.
- Securely display/export the mnemonic.

---

# 8. Testing Guidelines

AI should help generate:
- Unit tests for share reconstruction.
- Integration tests for unlock workflow.
- Security tests for unauthorized key access.

---

# 9. What AI Should NOT Touch
- TokenControl internal UI logic.
- Blockchain interaction logic.
- Any code involving Genesis exposure outside recovery.

---

# 10. Future Extensions
AI may help later with:
- Multi-device governance
- Multi-region Aegis servers
- Share QR codes or NFC export

---

