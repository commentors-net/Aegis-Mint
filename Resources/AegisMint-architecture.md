
# Aegis + Mint System Architecture Document

## 1. High-Level Architecture Overview
The system consists of 3 major components that work together securely:

```
+--------------+       +------------------+        +----------------+
| TokenControl | <---> |  AegisMint (Box) | <----> |   Aegis Web    |
|   (on box)   |       |   (Local API)    |        |  (Governance)  |
+--------------+       +------------------+        +----------------+
```

---

## 2. Component Responsibilities

### 2.1 AegisMint Service (Local Box)
- Runs in the background.
- Generates and stores the Genesis key.
- Generates and prints/export shares.
- Provides secure internal API endpoints to TokenControl.

#### AegisMint Internal APIs
| Endpoint | Purpose |
|---------|----------|
| `/getMnemonic` | Returns decrypted Genesis key (requires governance unlock). |
| `/getDeviceInfo` | Returns device ID, configuration, thresholds. |
| `/ping` | Health check. |

---

### 2.2 TokenControl
- User-facing app.
- Requests governance status from Aegis Web.
- Requests mnemonic from Mint if unlocked.
- Operates the token for blockchain interactions.
- Logs events.

#### TokenControl Flow
1. Start
2. Call Aegis Web → `isUnlocked(deviceId)`
3. If false → show waiting screen
4. If true → call Mint → get mnemonic
5. Load main UI
6. Operate

---

### 2.3 Aegis Web (Governance & Recovery)
Provides:
- Multi-governor approval workflow
- Unlock logic
- Device management
- Event logging
- Share-based recovery UI
- Download of standalone recovery tool

---

## 3. Governance Unlock Flow

```
Governors (N of M)
        |
        v
+----------------+
|  Aegis Web     |
|  Collect N approvals
+----------------+
        |
        v
+-----------------------+
| Device Unlock Window  |
|  (e.g., 15 minutes)   |
+-----------------------+
        |
        v
TokenControl -> Mint -> Genesis
```

Governance is separate from shares.

---

## 4. Recovery Flow (Web or Standalone)

```
User enters shares (N required)
        |
        v
Shamir Reconstruction
        |
        v
Genesis Key Restored
        |
        +--> Shown / downloaded securely
```

Recovery works even if all services are down.

---

## 5. Data Stored in Mint

| Data | Description | Access |
|------|-------------|--------|
| Genesis Key | 12-word mnemonic | Only Mint, or recovery tool |
| Shares | Shamir split fragments | Printed/provided |
| Device Config | Thresholds, share counts | Mint + Aegis Web |
| Encryption Keys | Used internally to protect Genesis | Mint only |

---

## 6. Data Stored in Aegis Web

| Data | Description |
|------|-------------|
| Unlock status | Whether device is currently unlocked |
| Governance rules | Governor list, N-of-M |
| Logs | Governance approvals, TokenControl events |
| Device metadata | ID, share count, thresholds |

---

## 7. Sequence Diagrams

### 7.1 Token Startup Sequence

```
TokenControl -> Aegis Web: isUnlocked(deviceId)?
Aegis Web -> TokenControl: Yes
TokenControl -> Mint: getMnemonic
Mint -> TokenControl: Genesis returned
TokenControl: starts session
```

### 7.2 Governance Approval Sequence

```
Governor -> Aegis Web: Approve(deviceId)
Aegis Web: Store approval
Aegis Web: Check if N approvals reached
If yes → set unlock(deviceId, 15min)
```

---

## 8. Technology Recommendations
- Backend: Python, Go, or Node.js
- Web UI: React or Vue
- Database: PostgreSQL
- Local Key Storage: OS keyring + encrypted vault
- Communication: gRPC or HTTPS

---

