
# AegisMint Token Control – User‑Friendly Test Plan
(Plain English – Non‑Technical)

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

---

# TEST SCENARIO 1 – Mint Application (One‑Time Setup)

## Step 1.1 – Start Mint

1. Open the **Mint** application

You should see:
- A window opens
- Only the **Generate Treasury** button is enabled

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

## Step 1.4 – Finish Mint Setup

- Mint can now be closed or uninstalled
- Shares must be stored securely
- Continue to Token Control

---

# TEST SCENARIO 2 – Desktop Registration & Access Approval
(Plain‑English Version)

## IMPORTANT – READ FIRST

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

## Step 3.1 – Start Recovery

1. Click **Recover**
2. Provide the minimum required shares

Result:
- Wallet and contract info appear
- Balances load

---

# TEST SCENARIO 4 – Admin Portal Basics

## Step 4.1 – Manage Users

Admin can:
- Create users
- Edit users
- Enable or disable users

---

## Step 4.2 – Manage Desktops

Admin can:
- Approve computers
- Edit approval rules
- Disable computers

---

## Step 4.3 – Assign Desktops

Admin can:
- Assign which users approve which desktops

---

# TEST SCENARIO 5 – Governance Portal

Governance users can:
- See assigned desktops
- Approve access
- See countdown timers

---

# TEST SCENARIO 6 – Multiple Desktops

System supports:
- Multiple computers
- Independent approvals
- Independent timers

---

# IMPORTANT THINGS TO REMEMBER

- Lock screens are normal
- Approvals expire by design
- Restarting does not bypass security
- If unsure, check approval status in the portal

---

# END OF TEST PLAN
