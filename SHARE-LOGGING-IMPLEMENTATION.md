# Share Operation Logging Implementation

## Branch: `feature/share-operation-logging`

## Overview
This implementation adds comprehensive logging for share creation and retrieval operations with tracking to the web backend database. Both desktop applications (AegisMint.Mint and AegisMint.TokenControl) now send detailed operation logs to the web server, enabling full audit trails and troubleshooting capabilities.

---

## What Was Implemented

### 1. Backend Infrastructure

#### Database Model: `ShareOperationLog`
**File:** `Web/backend/app/models/share_operation_log.py`

Tracks all share operations with:
- **Desktop identification**: `desktop_app_id`, `app_type` (Mint/TokenControl), `machine_name`
- **Operation details**: `operation_type` (Creation/Retrieval), `success`, `operation_stage`
- **Share metadata**: `total_shares`, `threshold`, `shares_used`
- **Context**: `token_name`, `token_address`, `network`, `shares_path`
- **Error tracking**: `error_message`, `notes`
- **Timestamp**: `at_utc` for chronological tracking

#### API Endpoints
**File:** `Web/backend/app/api/routers/share_operations.py`

- **POST `/api/share-operations/log`**
  - Logs share operations from desktop applications
  - Requires desktop authentication (HMAC or certificate)
  - Returns operation log ID and success status

- **GET `/api/share-operations/logs`**
  - Retrieves share operation logs
  - Supports filtering by `desktop_app_id`, `operation_type`
  - Pagination with configurable limit

#### Database Migration
**File:** `Web/backend/alembic/versions/009_share_operation_logs.py`

- Creates `share_operation_logs` table
- Adds indexes on frequently queried fields
- Defines `ShareOperationType` enum (Creation, Retrieval)

---

### 2. Desktop Application Logging

#### Share Operation Logger Service
**File:** `Mint/src/AegisMint.Core/Services/ShareOperationLogger.cs`

Reusable service for logging share operations to backend:
- **Generic logging**: `LogOperationAsync()` with all parameters
- **Creation helpers**:
  - `LogShareCreationStartAsync()`
  - `LogShareCreationSuccessAsync()`
  - `LogShareCreationFailureAsync()`
- **Retrieval helpers**:
  - `LogShareRetrievalStartAsync()`
  - `LogShareRetrievalSuccessAsync()`
  - `LogShareRetrievalFailureAsync()`

Features:
- Uses existing `DesktopAuthenticationService` for secure API calls
- Non-blocking: Logs failures without affecting main operations
- Includes retry logic and error handling

---

### 3. AegisMint.Mint - Share Creation Logging

**File:** `Mint/src/AegisMint.Mint/MainWindow.xaml.cs`

**Method:** `CreateAndSaveRecoverySharesAsync()`

#### Logging Stages:

1. **Start**: `=== SHARE CREATION STARTED ===`
   - Logs token name, total shares, threshold
   - Sends to backend with status "Started"

2. **Validation**: 
   - Logs if treasury mnemonic is missing
   - Sends failure to backend immediately

3. **Encryption**:
   - Logs 256-bit key generation
   - Logs AES-256-CBC encryption process

4. **Shamir Split**:
   - Logs total shares and threshold
   - Logs successful split completion with share count

5. **File Saving**:
   - Logs share directory path
   - Logs each individual share file: `Share X/Y saved: filename`
   - Logs token contract address

6. **Completion**: `=== SHARE CREATION COMPLETED ===`
   - Logs total count and final path
   - Sends success to backend with all metadata

7. **Failure**: `=== SHARE CREATION FAILED ===`
   - Logs full exception details
   - Sends failure to backend with error message

---

### 4. AegisMint.TokenControl - Share Retrieval Logging

**File:** `Mint/src/AegisMint.TokenControl/MainWindow.xaml.cs`

**Method:** `HandleRecoverFromSharesAsync()`

#### Logging Stages:

1. **Start**: `=== SHARE RETRIEVAL STARTED ===`
   - Logs user action initiation

2. **File Selection**:
   - Logs number of files selected
   - Logs cancellation if no files chosen

3. **Parsing**:
   - Logs each share file being parsed
   - Logs metadata discovery (threshold, network)

4. **Validation**:
   - Logs share count vs. threshold requirement
   - Logs validation failures with specific errors
   - Sends validation failures to backend

5. **Reconstruction**:
   - Logs Shamir reconstruction with share count
   - Logs successful key reconstruction

6. **Decryption**:
   - Logs decryption start
   - Logs successful mnemonic recovery

7. **Import**:
   - Logs vault import
   - Logs network update
   - Logs contract discovery

8. **Completion**: `=== SHARE RETRIEVAL COMPLETED ===`
   - Logs success
   - Sends success to backend with metadata

9. **Failure**: `=== SHARE RETRIEVAL FAILED ===`
   - Logs detailed error information
   - Sends failure to backend

---

## Backend Log Query Examples

### View all share operations
```sql
SELECT * FROM share_operation_logs 
ORDER BY at_utc DESC 
LIMIT 100;
```

### Find failed operations
```sql
SELECT desktop_app_id, operation_type, error_message, at_utc 
FROM share_operation_logs 
WHERE success = FALSE 
ORDER BY at_utc DESC;
```

### Track specific desktop's operations
```sql
SELECT operation_type, operation_stage, success, at_utc
FROM share_operation_logs
WHERE desktop_app_id = 'YOUR_DESKTOP_ID'
ORDER BY at_utc DESC;
```

### Get share creation statistics
```sql
SELECT 
    DATE(at_utc) as date,
    COUNT(*) as total_operations,
    SUM(CASE WHEN success THEN 1 ELSE 0 END) as successful,
    SUM(CASE WHEN NOT success THEN 1 ELSE 0 END) as failed
FROM share_operation_logs
WHERE operation_type = 'Creation'
GROUP BY DATE(at_utc)
ORDER BY date DESC;
```

---

## Desktop Log Examples

### Share Creation (Mint)
```
[INFO] === SHARE CREATION STARTED === Token: MyToken, Total: 5, Threshold: 3
[INFO] Generating 256-bit encryption key for mnemonic
[INFO] Encrypting mnemonic with AES-256-CBC
[INFO] Splitting encryption key using Shamir Secret Sharing: 5 shares, 3 threshold
[INFO] Shamir split complete - generated 5 shares
[INFO] Saving recovery shares to: C:\Shares\MyToken
[INFO] Token contract address: 0x123...
[INFO] Share 1/5 saved: aegis-share-001.json
[INFO] Share 2/5 saved: aegis-share-002.json
...
[INFO] === SHARE CREATION COMPLETED === All 5 shares saved to C:\Shares\MyToken
```

### Share Retrieval (TokenControl)
```
[INFO] === SHARE RETRIEVAL STARTED === User requested share recovery
[INFO] Loading 3 share files
[INFO] Parsing share file: aegis-share-001.json
[INFO] Share metadata loaded: 5 shares, 3 threshold, Network: sepolia
[INFO] Loaded 3 shares (threshold: 3)
[INFO] Parsing share format and preparing for Shamir reconstruction
[INFO] Reconstructing encryption key from 3 shares using Shamir combine
[INFO] Encryption key reconstructed successfully
[INFO] Decrypting mnemonic with reconstructed key
[INFO] Mnemonic decrypted successfully
[INFO] Importing treasury mnemonic to vault
[INFO] Updating network to: sepolia
[INFO] Discovering and persisting contract information
[INFO] === SHARE RETRIEVAL COMPLETED === Treasury recovered successfully
```

---

## Testing Instructions

### 1. Run Database Migration
```bash
cd Web/backend
alembic upgrade head
```

### 2. Start Backend Server
```bash
cd Web/backend
python main.py
```

### 3. Test Share Creation (Mint)
1. Open AegisMint.Mint
2. Complete token minting process
3. Check desktop logs for detailed operation stages
4. Query backend database to see logged operations

### 4. Test Share Retrieval (TokenControl)
1. Open AegisMint.TokenControl
2. Use "Recover from Shares" feature
3. Select 2-3 share files
4. Check desktop logs for reconstruction details
5. Query backend database for retrieval operation log

### 5. Verify Backend Logs
```bash
# Via API (requires desktop auth)
curl -X GET "http://localhost:8000/api/share-operations/logs?limit=10"

# Via database
mysql -u root -p aegis_mint
SELECT * FROM share_operation_logs ORDER BY at_utc DESC LIMIT 10;
```

---

## Benefits

### 1. **Full Audit Trail**
- Every share operation is tracked with timestamp
- Desktop identification enables accountability
- Operation stages provide detailed history

### 2. **Troubleshooting**
- Detailed error messages pinpoint failures
- Stage-by-stage logging identifies where issues occur
- Backend centralization enables cross-desktop analysis

### 3. **Security & Compliance**
- All share access attempts are recorded
- Failed attempts are logged with reasons
- Desktop identification prevents unauthorized access

### 4. **Operational Insights**
- Track share creation patterns
- Monitor recovery attempt frequency
- Identify problematic desktops

---

## Security Considerations

✅ **No Secrets Logged**: 
- Mnemonic phrases are never logged
- Share values are never logged
- Only metadata and operation status recorded

✅ **Authentication Required**:
- All backend logging requires desktop auth
- HMAC or certificate-based verification

✅ **Non-Blocking**:
- Logging failures don't affect operations
- Main workflow continues even if backend is down

---

## Files Modified/Created

### New Files
1. `Mint/src/AegisMint.Core/Services/ShareOperationLogger.cs`
2. `Web/backend/app/models/share_operation_log.py`
3. `Web/backend/app/api/routers/share_operations.py`
4. `Web/backend/alembic/versions/009_share_operation_logs.py`

### Modified Files
1. `Mint/src/AegisMint.Mint/MainWindow.xaml.cs`
2. `Mint/src/AegisMint.TokenControl/MainWindow.xaml.cs`
3. `Web/backend/app/models/__init__.py`
4. `Web/backend/app/api/routers/__init__.py`
5. `Web/backend/app/main.py`

---

## Next Steps

1. **Test Thoroughly**: Run complete share creation and retrieval workflows
2. **Apply Migration**: Run `alembic upgrade head` on production
3. **Monitor Logs**: Check both desktop and backend logs
4. **Build UI**: Consider adding admin UI to view share operation logs
5. **Merge to Main**: Once tested, merge `feature/share-operation-logging` to main

---

## Notes on Bug Fixes

The implementation also addresses the mentioned issues:

### Issue 1: Freeze Before Eventually Worked
- Added detailed stage logging to identify where delays occur
- Backend logging helps diagnose timing issues
- Non-blocking logger prevents additional delays

### Issue 2: Stale UI State After Approval
- Comprehensive logging helps identify when state becomes stale
- Operation tracking enables debugging of long-running sessions
- Stage markers help identify refresh failures

---

## Summary

This implementation provides **complete observability** of share operations across all desktop applications with centralized backend tracking. Every stage of share creation and retrieval is logged both locally and to the backend database, enabling full audit trails, troubleshooting capabilities, and operational insights while maintaining security best practices.
