
# AegisMint Token Control – User‑Friendly Test Plan
(Non‑Technical)

---

## PURPOSE OF THIS DOCUMENT

This document explains **how to test the AegisMint system end‑to‑end** in clear, simple language.
You do **not** need to understand cryptography, blockchain, or security internals to follow this guide.

If something looks “stuck” or “locked”, it is **usually expected behavior**, not a failure.

---

## SYSTEM OVERVIEW (IN SIMPLE TERMS)

The system has **three parts**:

1. **Mint Application**
   - Used once to create the token and security shares
2. **Token Control Application**
   - Used on desktops to manage the token
   - This app locks and unlocks based on approvals
3. **Governance Website**
   - Used by admins and approvers to grant access

---

## WHO DOES WHAT

- **Admin**
  - Approves computers
  - Assigns who can approve access

- **Governance Users**
  - Approve access requests

- **Token Control User**
  - Uses the desktop app
  - Waits for approvals

- **Test Users**
  - Admin: admin@example.com / ChangeMe123!
  - Governance: gov@example.com / GovPass123!
  - Governance: new2@example.com / finebyme@400users

---

# TEST SCENARIO 1 – Mint Application (One‑Time Setup)

## What This Step Is For (Important)

This scenario creates **two critical things**:
1. The token itself
2. **Recovery shares**, which are needed later in [Test Scenario 3](#test-scenario-3--recovering-the-token)

These shares are **not used every day**.
They are only needed for **recovery or disaster situations**.

---

## Step 1.1 – Desktop Registration & Approval (New Requirement)

**The Mint application requires governance approval before it can be used**, similar to Token Control.

### Step 1.1.1 – Launch Mint for First Time

1. Open the **Mint** application

You will see:
- A lock icon
- "Checking authorization…"

### Possible outcomes

**A. New computer**
- Message says it is registered and pending approval
- App closes automatically

**B. Already registered**
- Message says approvals are required
- App stays open

**C. Already approved**
- App unlocks immediately (skip to Step 1.2)

---

### Step 1.1.2 – Admin Verifies Mint Registration

1. Admin logs into the governance website
2. Opens **Manage Desktops**

You should see:
- The Mint application listed (App Type: **Mint**)
- Status = Pending

---

### Step 1.1.3 – Admin Approves Mint Application

1. Admin clicks **Approve** for the Mint entry

Result:
- Status becomes **Active**

---

### Step 1.1.4 – Admin Assigns Approvers for Mint

1. Admin opens **Assign Desktops**
2. Selects the Mint application
3. Assigns two governance users
4. Saves

---

### Step 1.1.5 – Request Access for Mint

1. Relaunch Mint

You will see:
- Locked screen
- Approval count (0 of 2)

---

### Step 1.1.6 – First Approval

1. Governance user #1 logs in
2. Clicks **Approve** for the Mint session

Result:
- Status shows 1 of 2
- App remains locked

---

### Step 1.1.7 – Second Approval

1. Governance user #2 logs in
2. Clicks **Approve** for the Mint session

Result:
- Status shows 2 of 2
- Countdown timer starts

---

### Step 1.1.8 – Automatic Unlock

1. Wait up to 30 seconds

Result:
- Mint unlocks
- Timer visible in title bar
- **Generate Treasury** button is enabled

---

### Step 1.1.9 – Session Expiry

When timer ends:
- Mint locks again
- Approvals reset
- Must repeat approval process to continue

This is expected behavior.

---

## Step 1.2 – Generate Treasury Address

1. Click **Generate Treasury**

You should see:
- A wallet address appears

This address must have test ETH before continuing.

---

## Step 1.3 – Mint Token and Create Shares

1. Enter:
   - Token name
   - Total supply
   - Decimals
   - Number of shares
   - Shares required to recover
2. Click **Mint Token**
3. Save the generated share files

You should see:
- A contract address appears
- Minting completes successfully

---

## Step 1.4 – What Are Shares?

Shares are **pieces of a master key**.

- One share alone does **nothing**
- No single person can control the token
- A minimum number of shares must be combined to recover access

Think of shares like **parts of a safe combination**.

---

## Step 1.5 – How to Store and Distribute Shares

- Give **one share per trusted person**
- Store shares in different locations
- Never email or centrally store all shares

---

## Step 1.6 – When Shares Are Needed Again

Shares are only required when:
- Recovering the token ([Test Scenario 3](#test-scenario-3--recovering-the-token))
- Rebuilding access after a disaster

---

## Step 1.7 – Finish Mint Setup

- After completing the minting process, Mint can be closed
- Shares must remain safely stored
- Continue to Token Control

---

### [Scenario 1 → See Appendix A](#appendix-a--shares--recovery-explained-one-page)

---

# TEST SCENARIO 2 – Desktop Registration & Access Approval

## IMPORTANT

Token Control may be locked for **normal reasons**:
1. New computer
2. Approvals missing
3. Session expired
4. New version installed

A lock screen does **not** mean failure.

---

## Step 2.1 – Launch Token Control

1. Open **Token Control**

You will see:
- A lock icon
- “Checking authorization…”

### Possible outcomes

**A. New computer**
- Message says it is registered and pending approval
- App closes automatically

**B. Already registered**
- Message says approvals are required
- App stays open

**C. Already approved**
- App unlocks immediately

---

## Step 2.2 – Admin Verifies Registration

1. Admin logs into the governance website
2. Opens **Manage Desktops**

You should see:
- The computer listed
- Status = Pending

---

## Step 2.3 – Admin Approves Computer

1. Admin clicks **Approve**

Result:
- Status becomes **Active**

---

## Step 2.4 – Admin Assigns Approvers

1. Admin opens **Assign Desktops**
2. Selects the computer
3. Assigns two governance users
4. Saves

---

## Step 2.5 – Request Access

1. Relaunch Token Control

You will see:
- Locked screen
- Approval count (0 of 2)

---

## Step 2.6 – First Approval

1. Governance user #1 logs in
2. Clicks **Approve**

Result:
- Status shows 1 of 2
- App remains locked

---

## Step 2.7 – Second Approval

1. Governance user #2 logs in
2. Clicks **Approve**

Result:
- Status shows 2 of 2
- Countdown timer starts

---

## Step 2.8 – Automatic Unlock

1. Wait up to 30 seconds

Result:
- App unlocks
- Timer visible in title bar

---

## Step 2.9 – Session Expiry

When timer ends:
- App locks again
- Approvals reset

This is expected.

---

# TEST SCENARIO 3 – Recovering the Token
(Connection to Shares Created in Scenario 1)

## What This Step Is For (Important)

This scenario uses the **shares created in Test Scenario 1**.
It confirms that multiple trusted people can jointly recover access.

## Important – Recovery Prerequisites

Before you can recover a token, you must complete these steps:

### Step A – Install Token Control on New Desktop

1. Install the Token Control application on your new or replacement computer
2. Launch it and it will register with web server

---

### Step B – Complete Desktop Registration & Approval

You must complete the full approval process from [Test Scenario 2](#test-scenario-2--desktop-registration--access-approval):

1. **Launch Token Control** (Step 2.1)
   - Application will register with the server
   - Application will lock automatically

2. **Admin Verifies Registration** (Step 2.2)
   - Admin sees the new desktop in the governance website

3. **Admin Approves Computer** (Step 2.3)
   - Admin changes status from Pending to Active

4. **Admin Assigns Approvers** (Step 2.4)
   - Admin assigns at least 2 governance users

5. **Request Access** (Step 2.5)
   - Relaunch Token Control
   - See locked screen with approval count

6. **First Approval** (Step 2.6)
   - First governance user approves

7. **Second Approval** (Step 2.7)
   - Second governance user approves
   - Countdown timer starts

8. **Automatic Unlock** (Step 2.8)
   - Wait up to 30 seconds
   - Application unlocks

---

### Step C – Proceed to Recovery

- **Only after** Token Control is unlocked, proceed to Step 3.1 below
- Have your recovery shares ready (created in [Test Scenario 1](#test-scenario-1--mint-application-one-time-setup))
---

## Step 3.1 – Start Recovery

1. Open **Token Control**
2. Ensure the application is unlocked
3. Click **Recover**
4. Provide the minimum required shares

You should see:
- Wallet address appears
- Contract address loads
- Balances display

---

### [Scenario 1 → See Appendix A](#appendix-a--shares--recovery-explained-one-page)

---

# TEST SCENARIO 4 – Admin Portal Basics

Admin can:
- Manage users
- Manage desktops
- Assign approvals

---

# TEST SCENARIO 5 – Governance Portal

Governance users can:
- Approve access
- Monitor timers

---

# TEST SCENARIO 6 – Multiple Desktops

The system supports:
- Multiple computers
- Independent approvals
- Independent timers

---

# IMPORTANT THINGS TO REMEMBER

- Lock screens are normal
- Approvals expire by design
- Shares are only used for recovery
- Restarting does not bypass security

---

# END OF TEST PLAN



# Appendix A – Shares & Recovery Explained (One Page)

## What Are Shares?

Shares are **pieces of a master key** created when the token is minted.

- A single share is **not useful on its own**
- No single person can control the token
- A minimum number of shares must be combined to recover access

Think of shares like **parts of a safe combination**:
- One person cannot open the safe
- A group must agree and work together

---

## Why Shares Exist

Shares exist to prevent:
- Single-person control
- Accidental loss of access
- Insider abuse

They ensure that **important actions require agreement**.

---

## How Shares Are Created

- Shares are created **once**, during the Mint process
- They are saved as files
- They are never sent to the server

---

## How Shares Should Be Distributed

Recommended best practices:
- One share per trusted person
- Different physical or secure digital locations
- No emailing or shared folders

Example holders:
- CTO
- Security Officer
- Compliance Officer
- External custodian (optional)

---

## When Shares Are Needed

Shares are **not needed for daily use**.

They are only used when:
- Recovering the token
- Rebuilding access after a failure
- Verifying ownership using Token Control recovery

---

## What Happens During Recovery

1. Token Control requests recovery
2. Trusted people provide their shares
3. Once the minimum number is reached:
   - The master key is reconstructed
   - Wallet and contract data are restored

---

## What If a Share Is Lost?

- As long as the minimum required shares exist, recovery still works
- If too many shares are lost, recovery is not possible

---

## About Shares

-- Useless without AegisMint applications
-- Protected by AES-256 encryption
-- Can only be decrypted by AegisMint applications

---

## Key Takeaway

Shares protect the system.
They make sure **no single mistake or person can break it**.



# Appendix B – AegisMint End-to-End Flow Diagram

```mermaid
flowchart TD
    A[Mint Application] --> B[Generate Token]
    B --> C[Create Shares]
    C --> D[Distribute Shares to Trusted People]

    D --> E[Token Control Installed]
    E --> F[Desktop Registration]
    F --> G[Admin Approval]
    G --> H[Governance Approvals]
    H --> I[Token Control Unlocked]

    I --> J[Normal Usage]
    J --> K[Session Expiry]
    K --> H

    C --> L[Recovery Needed]
    L --> M[Collect Required Shares]
    M --> N[Recover Master Key]
    N --> I
```
