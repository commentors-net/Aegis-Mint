
# AegisMint End-to-End User Story & Test Checklist

---

## 1. End-to-End User Story

### Part A — Mint Installation & Desktop Registration

- Install the latest version of **AegisMint.Mint** (Mint).
- Launch the Mint application.
- Mint automatically registers the desktop with the Governance Web Application (GWA).
- Mint displays a registration message and closes automatically after ~5 seconds.
- Message indicates that admin approval is required before use.

---

### Part B — Desktop Approval & Unlock (GWA Admin)

- Admin logs in to **GWA**.
- Navigate to **Manage Desktops**.
- Search for desktop containing the name **Mint**.
- Desktop appears with:
  - Status: **Pending**
  - Actions: **Approve**, **Reject**
- Admin clicks **Approve**.
- Status changes to **Active**.
- Actions change to **Edit**, **Disable**.
- Admin navigates to **Mint Approval**.
- Admin clicks **Approve Unlock**.
- Mint desktop is now unlocked for use.

---

### Part C — Token Creation Using Mint

- Mint user launches Mint again.
- Application remains open and usable.
- User completes **TEST SCENARIO 1**:
  https://github.com/commentors-net/Aegis-Mint/blob/main/TEST-PLAN-USER-FRIENDLY.md
- Token is successfully created.
- Mint sends token and share information to GWA.
- GWA logs all Mint actions.

---

### Part D — Token & Share Management (GWA Admin)

- Admin navigates to **Share Management**.
- Token appears in list with:
  - Token Name
  - Symbol
  - Network
  - Contract Address
  - Share Status
  - Date Created
  - Actions
- Admin clicks **+** to expand token row.
- Admin clicks **Add User**.
- User creation form fields:
  - Name
  - Email (username for SMP)
  - Phone
  - Password
- Same email can be assigned to multiple tokens.
- Admin adds required token users.

---

### Part E — Share Assignment

- Admin clicks **Manage Shares** for a token.
- Token details and unassigned shares are displayed.
- Admin clicks **Assign** on a share.
- Assignment form shows:
  - Token users list
  - Admin-only notes field
- Admin assigns share to a user.
- Action changes to **Unassign**.
- Same user may receive multiple shares.
- Admin completes share distribution.

---

### Part F — Token User Access & Download (SMP)

- Token users receive login credentials via a secure channel.
- Token user logs in to **Share Management Portal (SMP)**.
- First-time login:
  - QR code displayed
  - User enrolls authenticator
  - Enters 2FA code
- User sees assigned share cards.
- Each card shows token name and **Download** button.
- User clicks **Download**.
- Warning displayed: one-time download only.
- Download completes and button becomes disabled.
- Process repeated for all token users.

---

## 2. Execution Checklist

### Mint & Desktop Registration
- [ ] Mint installed successfully
- [ ] Mint registers desktop with GWA
- [ ] Mint closes automatically after registration

### GWA Desktop Approval
- [ ] Desktop visible in Manage Desktops
- [ ] Status shown as Pending
- [ ] Desktop approved
- [ ] Status changed to Active
- [ ] Mint unlocked via Mint Approval

### Token Creation
- [ ] Mint launches successfully after unlock
- [ ] TEST SCENARIO 1 completed
- [ ] Token created successfully
- [ ] Token synced to GWA
- [ ] Mint actions logged in GWA

### Token User Management
- [ ] Token visible in Share Management
- [ ] Token details correct
- [ ] Token users added successfully
- [ ] Same email usable across multiple tokens

### Share Assignment
- [ ] Unassigned shares visible
- [ ] Shares assigned to token users
- [ ] Multiple shares assignable to same user
- [ ] Unassign option available

### SMP Access & Download
- [ ] Token user login successful
- [ ] 2FA enrollment completed
- [ ] Assigned shares visible
- [ ] One-time download warning shown
- [ ] Download completes successfully
- [ ] Download disabled after use

---

**Document Purpose**
This document serves as both:
- A step-by-step execution story for testers and stakeholders
- A live checklist to track execution progress

