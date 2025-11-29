
# Aegis & Mint System Requirements Document

## 1. Overview
This document defines the functional, non-functional, governance, and security requirements for the Aegis Governance System, Mint Service, and their interaction with TokenControl. TokenControl already exists and only requires small modifications, while Aegis and Mint must be designed from scratch.

The system consists of:
- **Mint (AegisMint Service)** – runs on the hardware box, generates and secures the Genesis key and Shamir shares.
- **Aegis Web System** – governance control, approval workflow, logs, recovery UI.
- **TokenControl Application** – the user-facing token operation app. It relies on Mint and governance status from Aegis.

---

## 2. Definitions

### 2.1 Genesis Key
A 12‑word mnemonic phrase, unique per device, created once by Mint. It is the single master secret required for all cryptographic operations.

### 2.2 Shares
Shamir Secret Sharing fragments of the Genesis key, used strictly for recovery.  
If N of M shares are required:  
- Mint produces **M user shares** and **N additional safety shares** retained by the system.

### 2.3 Aegis Governance
A web-based decision layer where multiple authorized governors approve unlocking the TokenControl for use. Governance is NOT related to recovery shares.

### 2.4 Device (Box)
The hardware unit where Mint service and TokenControl run, and the Genesis key is buried.

---

## 3. Functional Requirements

### 3.1 Mint Service Requirements
1. Shall generate a **single Genesis key** (12 words) during initial installation.
2. Shall generate Shamir shares based on administrator parameters:
   - M = number of key holders
   - N = number required for recovery
3. Shall create **M + N** total shares.
4. Shall securely bury the Genesis key on the box.
5. Shall never expose the Genesis key except during a recovery flow.
6. Shall provide a secure API for TokenControl:
   - `/getMnemonic` → returns decrypted Genesis key only when governance unlock is active.
7. Shall send metadata to Aegis Web upon installation:
   - Device ID / serial number
   - Number of shares
   - Threshold value
   - Governance configuration
8. Must run continuously in background unless explicitly stopped by admin.
9. Must prevent unauthorized tampering.

---

### 3.2 TokenControl Requirements
1. TokenControl shall NOT ask the user for a mnemonic unless in emergency Admin mode.
2. At startup, TokenControl shall query Aegis Web:
   - `isUnlocked(deviceId)` → Boolean.
3. If unlocked:
   - TokenControl requests mnemonic from Mint service.
   - TokenControl loads main UI automatically.
4. If locked:
   - Show “Waiting for Governance Approval”.
   - Poll Aegis every X seconds.
5. TokenControl shall log all usage events to Aegis:
   - Startup
   - Unlock
   - Shutdown
   - Operations (optional)

---

### 3.3 Aegis Governance Requirements
1. Aegis Web shall maintain governance rules for each device:
   - Number of governors (M)
   - Approval threshold (N)
   - Unlock duration
2. Governors shall be able to log in and approve a request, providing:
   - Reason
   - Timestamp
3. When N approvals are collected:
   - Device is unlocked for 15 minutes (configurable).
4. Events must be logged:
   - Governor approvals
   - Unlock events
   - Token usage events
5. Allows remote lock even after approval was given.
6. Provides a dashboard showing:
   - Device status
   - Logs
   - Unlock windows
   - Active governors

---

### 3.4 Recovery UI Requirements (Web & Standalone)
1. Allows user to enter Shamir shares.
2. Performs reconstruction of Genesis key.
3. Shows or downloads the Genesis key.
4. Available in two forms:
   - Web-based recovery UI
   - Standalone offline executable (download from Aegis Web)
5. Requires admin authentication before use.
6. Must validate:
   - Correct number of shares
   - Correct share format

---

## 4. Non-Functional Requirements

### 4.1 Security
- Genesis key must never appear in plain text except in recovery.
- Critical operations encrypted at rest and in transit.
- Strong hashing & encryption: AES‑256, Argon2, or equivalent.
- Audit logs must be immutable.

### 4.2 Reliability
- All components must survive device restarts.
- Aegis Web service must accept offline caching and later sync.

### 4.3 Performance
- Unlock check must return within 500 ms.
- Mint API response to TokenControl must be < 300 ms.

### 4.4 Usability
- Governors need a simple approval interface.
- Recovery UI must be easy to follow without technical expertise.

---

## 5. Constraints
- Mint can only run on the hardware device.
- TokenControl must not rely on shares.
- Governance is independent from recovery.

---

## 6. Open Questions
- How are device identities created? (hardware ID? certificate?)
- Backup of Aegis Web data—what is the redundancy model?

---

