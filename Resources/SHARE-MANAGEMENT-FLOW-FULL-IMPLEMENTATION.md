# Aegis Mint - Share Management System

Version: 1.2
Created: 2026-01-24
Last Updated: 2026-01-26

## Purpose
Build a secure web-based system for managing and distributing Encryption recovery shares with admin oversight, MFA, and complete audit trails.

## Current State
- Shares are stored individually in `share_files`.
- Token users are global by email and can be assigned to multiple tokens via `token_user_assignments`.
- Admins assign shares to token users; downloads are one-time by default and audited.

## Architecture Overview
- Desktop (AegisMint.Mint) generates shares and uploads them to the backend.
- Web Admin (Web) manages tokens, token users, and share assignments.
- Share Portal (ClientWeb) lets token users authenticate with MFA and download their assigned shares.
- ClientWeb backend is a proxy that forwards requests to the main backend.

### Architecture Diagram
```mermaid
graph TB
    subgraph Desktop["Desktop App (AegisMint.Mint)"]
        D1[Mint Token]
        D2[Generate Shares]
        D3[Save Locally C:\\Shares\\TOKEN]
        D4[Upload to Backend]
    end
    
    subgraph Backend["Backend API (FastAPI)"]
        B1[Token Deployment API]
        B2[Share Upload API]
        B3[Admin Assignment API]
        B4[User Download API]
        B5[Token User Auth API]
        B6[Share Operation Log API]
    end
    
    subgraph AdminUI["Admin Portal (Web)"]
        A1[Token List]
        A2[User Management]
        A3[Share Assignment]
    end
    
    subgraph UserUI["Share Portal (ClientWeb)"]
        U1[Login/MFA]
        U2[My Shares]
        U3[Download History]
    end
    
    subgraph Database["MySQL Database"]
        DB1[(token_deployments)]
        DB2[(share_files)]
        DB3[(share_assignments)]
        DB4[(token_users)]
        DB5[(token_user_assignments)]
        DB6[(share_download_log)]
        DB7[(share_operation_logs)]
    end
    
    D1 --> D2 --> D3 --> D4
    D4 --> B1 --> DB1
    D4 --> B2 --> DB2
    D4 --> B6 --> DB7
    
    A1 --> B3
    A2 --> B3
    A3 --> B3
    B3 --> DB3
    B3 --> DB4
    B3 --> DB5
    
    U1 --> B5
    B5 --> DB4
    B5 --> DB5
    
    U2 --> B4
    U3 --> B4
    B4 --> DB3
    B4 --> DB2
    B4 --> DB6
```

## End-to-End Flows (Summary)

### 1) Share Upload
1. Desktop mints token and generates N share files.
2. Desktop posts token deployment metadata.
3. Desktop uploads share files in bulk to `/api/share-files/bulk`.
4. Backend stores each share and updates `token_deployments.shares_uploaded`.

```mermaid
sequenceDiagram
    participant Desktop as Desktop App
    participant API as Backend API
    participant DB as MySQL
    
    Desktop->>Desktop: Mint token and generate shares
    Desktop->>API: POST /api/token-deployments
    API->>DB: INSERT token_deployments
    DB-->>API: deployment_id
    
    Desktop->>API: POST /api/share-files/bulk
    loop for each share
        API->>DB: INSERT share_files
    end
    API->>DB: UPDATE token_deployments.shares_uploaded = true
    API-->>Desktop: Upload success
```

### 2) Admin Assignment
1. Admin selects a token and views its shares.
2. Admin creates or selects a token user (global email).
3. Admin assigns a share to the user.
4. Assignment is tracked in `share_assignments` and visible to the user.

```mermaid
sequenceDiagram
    participant Admin as Admin UI
    participant API as Backend API
    participant DB as MySQL
    
    Admin->>API: GET /api/share-files/token/{id}
    API->>DB: SELECT share_files with assignment status
    API-->>Admin: share list
    
    Admin->>API: GET /api/token-share-users/token/{id}
    API->>DB: SELECT token_users + token_user_assignments
    API-->>Admin: token users list
    
    Admin->>API: POST /api/admin/share-assignments {share_file_id, user_id}
    API->>DB: INSERT share_assignments
    API-->>Admin: assignment created
```

### 3) User Download
1. Token user logs in with email + password + MFA.
2. User views assigned shares.
3. User downloads a share (one-time by default).
4. Backend disables the download and writes to `share_download_log`.

```mermaid
sequenceDiagram
    participant User as Share Portal
    participant API as Backend API
    participant DB as MySQL
    
    User->>API: GET /api/my-shares
    API->>DB: SELECT share_assignments for token_user
    API-->>User: list of assigned shares
    
    User->>API: GET /api/my-shares/download/{assignment_id}
    API->>DB: CHECK download_allowed
    alt download allowed
        API->>DB: UPDATE share_assignments download_count and disable
        API->>DB: INSERT share_download_log
        API-->>User: share file JSON
    else download blocked
        API-->>User: error (already downloaded)
    end
```

### 4) Admin Re-enable
1. Admin re-enables a share download when requested.
2. User can download again and the action is logged.

```mermaid
sequenceDiagram
    participant Admin as Admin UI
    participant API as Backend API
    participant DB as MySQL
    
    Admin->>API: GET /api/admin/share-assignments?user_id=...
    API->>DB: SELECT share_assignments
    API-->>Admin: assignments list
    
    Admin->>API: PATCH /api/admin/share-assignments/{id} {download_allowed: true}
    API->>DB: UPDATE share_assignments download_allowed=true
    API-->>Admin: update success
```

## Implementation Status (Phases)

### Phase 1: Database and Core API (Complete)
- [x] `share_files`, `share_assignments`, `share_download_log`, `share_operation_logs`
- [x] `token_users` + `token_user_assignments` (migration 019)
- [x] Share upload, assignment, and download endpoints

### Phase 2: Desktop Integration (Complete)
- [x] Upload individual share files with retry logic
- [x] Token deployment metadata upload

### Phase 3: Admin Portal UI (Complete)
- [x] Token list with share upload status
- [x] Token user management (create/edit/delete assignment)
- [x] Share assignment and re-enable actions

### Phase 4: Token User Portal (Complete)
- [x] Login + MFA setup and verification
- [x] Multi-token selection
- [x] Assigned shares dashboard
- [x] Download flow with auto-disable
- [x] Download history view

### Phase 5: Advanced Features (Backlog)
- [ ] Share expiration dates
- [ ] Bulk assignment operations
- [ ] Share transfer between users (with approval)
- [ ] Export audit reports
- [ ] Notifications (email/Slack)
- [ ] Emergency revocation (disable all downloads quickly)

## Audit Notes
- Admin assignment actions are recorded in `share_assignments` (assigned_by, assigned_at_utc).
- User download attempts are recorded in `share_download_log` with IP and user agent.
- Desktop creation/retrieval events are recorded in `share_operation_logs`.

## Next Work
- Produce a user-facing test plan (see `TEST-PLAN-USER-FRIENDLY.md` as the template).
