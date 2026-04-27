# AegisMint Flowcharts and Stakeholder Diagrams

This document provides visuals for engineering, operations, and stakeholder communication.

---

## 1) Cross-Application Connection Flowchart

```mermaid
flowchart LR
    Owner[Desktop Owner]
    Admin[Super Admin]
    GovUser[Governance User]
    SU[Share User]

    Mint[Mint Desktop]
    TokenControl[Token Control Desktop]
    ShareManager[ShareManager Desktop]
    GPWeb[GP Frontend Web]
    GPAPI[GP Backend API]
    CSWeb[Client Share Frontend]
    CSAPI[Client Share Backend Proxy]
    ETH[(Ethereum Network)]
    DB[(Platform Database)]
    ShareFiles[(Encrypted Share Files)]

    Owner --> Mint
    Owner --> TokenControl
    Owner --> ShareManager
    Admin --> GPWeb
    GovUser --> GPWeb
    SU --> CSWeb

    Mint -->|register desktop| GPAPI
    Mint -->|mint token + upload share metadata| GPAPI
    Mint -->|deploy contract| ETH
    Mint -->|create files| ShareFiles

    TokenControl -->|register desktop + poll unlock| GPAPI
    TokenControl -->|recover, transfer, freeze, retrieve| ETH
    TokenControl -->|share retrieval logs| GPAPI

    ShareManager -->|validate shares + upload rotation| GPAPI
    ShareManager -->|lookup token data| ETH
    ShareManager -->|reads/writes| ShareFiles

    GPWeb --> GPAPI
    CSWeb --> CSAPI --> GPAPI

    GPAPI --> DB
```

---

## 2) End-to-End Lifecycle Sequence (Install to Recovery)

```mermaid
sequenceDiagram
    participant Owner as Desktop Owner
    participant Mint as Mint Desktop
    participant GP as GP (Web/API)
    participant Admin as Super Admin
    participant Gov as Governance User(s)
    participant CS as Client Share Portal
    participant SU as Share User
    participant TC as Token Control
    participant SM as ShareManager
    participant Chain as Ethereum

    Owner->>Mint: Install and launch (first run)
    Mint->>GP: Register desktop (Mint type)
    Mint-->>Owner: Show pending message, then close

    Admin->>GP: Approve desktop in Manage Desktops
    Admin->>GP: Approve unlock in Mint Approval

    Owner->>Mint: Launch again
    Mint->>Chain: Deploy ERC20 token
    Mint->>GP: Send deployment + share metadata

    Admin->>GP: Create Share Users and assign shares
    SU->>CS: Login + MFA
    CS-->>SU: Show assigned shares
    SU->>CS: Download share (one-time default)
    CS->>GP: Log success/failure history

    Owner->>TC: Install and launch (first run)
    TC->>GP: Register desktop (TokenControl type)
    TC-->>Owner: Show pending message, then close

    Admin->>GP: Approve TokenControl desktop
    Admin->>GP: Assign governance users to desktop
    Gov->>GP: Approve desktop session (N of M)
    GP-->>TC: Unlock status true (time window)

    Owner->>TC: Launch operations
    TC->>Chain: Unpause, transfer, freeze/unfreeze, retrieve tokens

    Owner->>SM: Recover mnemonic from shares
    SM->>GP: Upload rotated share configuration
    GP-->>CS: Old shares inactive, new shares active
```

---

## 3) Trust and Control Layers (Security Story)

```mermaid
flowchart TB
    A[Layer 1: Device Trust]
    B[Layer 2: Access Trust]
    C[Layer 3: Key Recovery Trust]
    D[Layer 4: Audit Trust]

    A1[Desktop registration per install]
    A2[Admin desktop approval]

    B1[Mint unlock approval]
    B2[TokenControl governance quorum]
    B3[Time-bound unlock sessions]

    C1[Secured secret share distribution]
    C2[Share assignment to independent users]
    C3[One-time download policy]
    C4[Share rotation and old-share invalidation]

    D1[Approval logs]
    D2[Share download history]
    D3[Desktop share operation logs]

    A --> A1
    A --> A2

    B --> B1
    B --> B2
    B --> B3

    C --> C1
    C --> C2
    C --> C3
    C --> C4

    D --> D1
    D --> D2
    D --> D3

    A --> B --> C --> D
```

---

## 4) Stakeholder Value Diagram 

```mermaid
flowchart LR
    Risk[Customer Risks]
    Cap[Platform Capabilities]
    Outcome[Business Outcomes]

    R1[Unauthorized desktop use]
    R2[Single point of key loss]
    R3[Weak custody traceability]
    R4[Operational lockout during incidents]

    C1[Desktop approval and unlock governance]
    C2[Distributed encrypted share custody]
    C3[Share user MFA and one-time downloads]
    C4[Share rotation and recovery tooling]
    C5[Full audit trail across apps]

    O1[Higher trust for token issuers]
    O2[Reduced key compromise impact]
    O3[Clear compliance evidence]
    O4[Faster incident recovery]
    O5[Enterprise-ready governance story]

    Risk --> R1
    Risk --> R2
    Risk --> R3
    Risk --> R4

    R1 --> C1
    R2 --> C2
    R3 --> C3
    R4 --> C4
    R3 --> C5

    C1 --> Outcome
    C2 --> Outcome
    C3 --> Outcome
    C4 --> Outcome
    C5 --> Outcome

    Outcome --> O1
    Outcome --> O2
    Outcome --> O3
    Outcome --> O4
    Outcome --> O5
```

---

## Suggested Usage

- Use Diagram 1 for technical architecture walkthroughs.
- Use Diagram 2 for QA/UAT kickoff and go-live readiness.
- Use Diagram 3 for security and governance discussions.
- Use Diagram 4 for executive and customer-facing presentations.
