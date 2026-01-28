
# AegisMint – Formal Test Case Matrix with Swimlanes

---

## Document Purpose

This document provides a **formal test case matrix** derived from the approved storytelling flow.  
It is structured using **swimlanes** to clearly separate responsibilities and actions for:

- **GWA Admin**
- **Mint Desktop User**
- **Token User (SMP)**

This document is intended for:
- QA execution
- UAT sign-off
- Audit and compliance review

---

## Swimlane Overview

| Swimlane | Description |
|--------|------------|
| GWA Admin | Governance actions, approvals, user & share management |
| Mint User | Desktop-based Mint operations and token creation |
| Token User | SMP access, 2FA onboarding, and share download |

---

## Test Case Matrix

### Swimlane: Mint Desktop User

| TC ID | Title | Preconditions | Steps | Expected Result | Priority |
|------|------|--------------|-------|----------------|----------|
| MINT-01 | Install Mint Application | Installer available | Install Mint | Installation completes successfully | High |
| MINT-02 | Register Desktop with GWA | Mint installed | Launch Mint | Desktop registered, app closes in ~5 sec | High |
| MINT-03 | Blocked Use Before Approval | Desktop pending | Launch Mint | Mint blocks usage with approval message | High |
| MINT-04 | Launch Mint After Unlock | Desktop approved & unlocked | Launch Mint | Mint opens and is usable | Critical |
| MINT-05 | Execute Token Creation | Mint unlocked | Perform TEST SCENARIO 1 | Token created successfully | Critical |
| MINT-06 | Sync Token to GWA | Token created | Observe GWA | Token & shares appear in GWA | Critical |
| MINT-07 | Log Mint Actions | Mint operations executed | Check GWA logs | All Mint actions logged | High |

---

### Swimlane: GWA Admin

| TC ID | Title | Preconditions | Steps | Expected Result | Priority |
|------|------|--------------|-------|----------------|----------|
| GWA-01 | Login to GWA | Admin credentials | Login | Login successful | High |
| GWA-02 | View Pending Desktop | Desktop registered | Navigate to Manage Desktops | Desktop visible with Pending status | High |
| GWA-03 | Approve Desktop | Desktop pending | Click Approve | Status changes to Active | Critical |
| GWA-04 | Reject Desktop | Desktop pending | Click Reject | Desktop blocked | Medium |
| GWA-05 | Unlock Mint | Desktop active | Navigate to Mint Approval → Approve Unlock | Mint unlocked | Critical |
| GWA-06 | View Token List | Token created | Navigate to Share Management | Token visible with correct details | Critical |
| GWA-07 | Add Token User | Token exists | Add User (Name, Email, Phone, Password) | User added successfully | High |
| GWA-08 | Reuse Email Across Tokens | User exists | Assign same email to another token | Allowed | High |
| GWA-09 | View Unassigned Shares | Token exists | Manage Shares | Unassigned shares listed | High |
| GWA-10 | Assign Share | Token users exist | Assign share to user | Share assigned | Critical |
| GWA-11 | Assign Multiple Shares | Shares available | Assign multiple shares to same user | Allowed | High |
| GWA-12 | Unassign Share | Share assigned | Click Unassign | Share becomes unassigned | Medium |
| GWA-13 | Admin Notes Visibility | Notes added | View SMP | Notes not visible to token user | High |

---

### Swimlane: Token User (SMP)

| TC ID | Title | Preconditions | Steps | Expected Result | Priority |
|------|------|--------------|-------|----------------|----------|
| SMP-01 | Login First Time | Credentials provided | Login to SMP | QR code displayed | Critical |
| SMP-02 | Enroll Authenticator | QR shown | Scan QR + enter OTP | Login successful | Critical |
| SMP-03 | Invalid OTP Handling | QR enrolled | Enter wrong OTP | Error shown, retry allowed | High |
| SMP-04 | View Assigned Shares | Shares assigned | Login to SMP | Share cards visible | Critical |
| SMP-05 | One-Time Download Warning | Share available | Click Download | Warning displayed | High |
| SMP-06 | Download Share | Warning accepted | Complete download | File downloaded | Critical |
| SMP-07 | Disable Re-Download | Share downloaded | Attempt re-download | Download blocked | Critical |
| SMP-08 | Access Control | Multiple users | Login as another user | Only own shares visible | Critical |

---

## Cross-Swimlane Validation Scenarios

| Scenario | Validation |
|--------|-----------|
| Desktop Approved but Not Unlocked | Mint remains blocked |
| Share Unassigned After Assignment | SMP access removed |
| Same User on Multiple Tokens | User sees shares from all tokens |
| Auditability | All actions logged in GWA |

---

## Entry & Exit Criteria

### Entry Criteria
- Mint installer available
- GWA & SMP accessible
- Admin credentials available
- Test token users defined

### Exit Criteria
- All Critical test cases passed
- No open High severity defects
- Token distribution completed successfully
- One-time download enforcement verified

---

**Traceability**
This matrix directly maps to the approved storytelling document:
`SHARES_END_TO_END_STORY_AND_CHECKLIST.md`

