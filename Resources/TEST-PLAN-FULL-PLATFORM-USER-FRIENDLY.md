# AegisMint Full Platform - User-Friendly Test Plan
(Non-Technical, End-to-End)

---

## PURPOSE OF THIS DOCUMENT

This document explains how to test the full AegisMint solution in plain language.

It covers all connected projects:
- `AegisMint.Mint` (Mint)
- `AegisMint.TokenControl` (Token Control)
- `AegisMint.ShareManager` (ShareManager / SM)
- `Web` (Governance Portal / GP)
- `ClientWeb` (Client Share / CS)

---

## SYSTEM OVERVIEW (IN SIMPLE TERMS)

The platform has five connected parts:

1) Mint desktop app  
- Registers desktop with GP  
- Waits for admin approvals  
- Generates treasury and deploys ERC20 token  
- Creates encrypted share files and uploads share metadata to GP

2) Governance Portal (Web / GP)  
- Approves desktop registrations  
- Runs Mint approval flow  
- Assigns governance users for Token Control unlocks  
- Manages Share Users and share assignments

3) Client Share portal (ClientWeb / CS)  
- Share Users log in with MFA  
- Download assigned shares (one-time by default)  
- View download history

4) Token Control desktop app  
- Registers desktop with GP  
- Requires governance approvals to unlock  
- Recovers treasury from share files  
- Unpauses contract, transfers tokens, freezes/unfreezes addresses, and reclaims tokens

5) ShareManager desktop app  
- Recovers mnemonic from shares  
- Changes share count and threshold (share rotation)  
- Uploads rotated share configuration to GP and invalidates old active shares

---

## WHO DOES WHAT

- Desktop Owner
  - Uses Mint and Token Control on desktop
  - Uses ShareManager when rotating recovery policy

- Super Admin (GP)
  - Approves desktops
  - Approves Mint unlock sessions
  - Assigns governance users to Token Control desktops
  - Creates Share Users and assigns shares
  - Re-enables disabled share downloads when needed

- Governance User (GP)
  - Approves assigned Token Control desktops
  - Can approve once per active approval session

- Share User (CS)
  - Logs in with MFA
  - Downloads assigned share files for safekeeping

---

## TEST DATA CHECKLIST

Before you begin:
- Web backend is running (`Web/backend`)
- Web frontend is running (`Web/frontend`)
- ClientWeb backend is running (`ClientWeb/backend`)
- ClientWeb frontend is running (`ClientWeb/frontend`)
- Ethereum network access is available (Localhost and/or Sepolia and/or Mainnet)
- At least one Super Admin account exists
- At least two Governance user accounts exist (for threshold testing)
- Test desktop machines are available for Mint and Token Control
- You have installer packages for Mint, Token Control, and ShareManager

Recommended test token setup:
- Token Name: `Aegis Demo Token`
- Symbol: `ADT`
- Decimals: `18`
- Supply: `1000000`
- Shares: `5`
- Threshold: `3`

---

# TEST SCENARIO 1 - Mint First-Run Registration

## What this step is for
Confirm Mint registers with GP on first launch and then closes.

### Steps
1. Install Mint on a clean desktop.
2. Launch Mint for the first time.

### You should see
- Registration completes
- Mint shows a pending/registration message
- Mint closes automatically after a short delay
- New desktop appears in GP as `Mint` type and `Pending`

---

# TEST SCENARIO 2 - Mint Blocked Before Approval

## What this step is for
Confirm Mint cannot be used while still pending.

### Steps
1. Launch Mint again before admin approval.

### You should see
- Mint remains locked
- Message indicates approval is required
- App closes after waiting message

---

# TEST SCENARIO 3 - Admin Approves Mint Desktop

## What this step is for
Confirm admin can approve Mint desktop registration legitimacy.

### Steps
1. Log in to GP as Super Admin.
2. Go to `Manage Desktops`.
3. Find desktop with app type `Mint`.
4. Click `Approve`.

### You should see
- Desktop status changes from `Pending` to `Active`

---

# TEST SCENARIO 4 - Admin Approves Mint Unlock Session

## What this step is for
Confirm active Mint desktop still needs unlock approval in Mint Approval.

### Steps
1. In GP, open `Mint Approval`.
2. Find the active Mint desktop.
3. Click `Approve Unlock`.

### You should see
- Unlock session approval succeeds
- Mint desktop can be unlocked for use

---

# TEST SCENARIO 5 - Mint Launch After Approval

## What this step is for
Confirm Mint opens usable screen only after unlock is approved.

### Steps
1. Launch Mint after Scenarios 3 and 4.

### You should see
- Mint opens main screen
- App is not locked
- Session countdown is visible when unlock window is active

---

# TEST SCENARIO 6 - Generate Treasury in Mint

## What this step is for
Confirm treasury address generation works.

### Steps
1. In Mint, fill required token/governance fields.
2. Click `Generate Treasury`.

### You should see
- Treasury address is generated and displayed
- Treasury status updates successfully

---

# TEST SCENARIO 7 - Mint ERC20 Token Deployment and Share Creation

## What this step is for
Confirm token deployment plus share generation/upload works end-to-end.

### Steps
1. In Mint, choose network (Localhost/Sepolia/Mainnet).
2. Provide token details and governance split.
3. Click `Mint Token`.
4. Wait for completion.

### You should see
- Contract address is created
- Treasury token balance updates
- Share files are generated locally
- GP receives token deployment and share metadata

---

# TEST SCENARIO 8 - Verify Share Filename Format

## What this step is for
Confirm generated share files follow expected naming.

### Steps
1. Open generated share folder after minting.
2. Inspect filenames.

### You should see
- Filename pattern: `MMDDYYTOKENNAMEXXYY.aegisshare`
- `MMDDYY` = date
- `TOKENNAME` = token name without spaces/special characters
- `XX` = share number
- `YY` = total shares

---

# TEST SCENARIO 9 - Token Appears in GP Share Management

## What this step is for
Confirm minted token and shares are visible in GP.

### Steps
1. In GP, go to `Share Management`.
2. Locate the newly minted token.
3. Expand row and open `Manage Shares`.

### You should see
- Token row exists with correct network and contract
- Shares are listed for assignment

---

# TEST SCENARIO 10 - (Optional) Uninstall Mint After One-Time Use

## What this step is for
Validate operational model where Mint can be removed after deployment.

### Steps
1. Uninstall Mint from desktop.
2. Verify GP data remains intact.

### You should see
- Mint uninstall succeeds
- GP still shows deployed token and share records

---

# TEST SCENARIO 11 - Create Share User in GP

## What this step is for
Confirm admin can create Share User (SU) per token.

### Steps
1. In `Share Management`, expand token row.
2. Click `+ Add User`.
3. Enter name, email, phone (optional), password.
4. Save.

### You should see
- User appears under token user list

---

# TEST SCENARIO 12 - Reuse Existing Share User Email Across Tokens

## What this step is for
Confirm one SU can be assigned to multiple tokens.

### Steps
1. Open another token in `Share Management`.
2. Add a user with email that already exists.
3. Confirm assignment when prompted.

### You should see
- System indicates user already exists
- After confirmation, same user is assigned to this second token
- No duplicate user identity is created

---

# TEST SCENARIO 13 - Assign Shares to Share Users

## What this step is for
Confirm admin can distribute shares to assigned users.

### Steps
1. Open `Manage Shares` for a token.
2. Click `Assign` on unassigned share.
3. Select a Share User and confirm.
4. Repeat for all required shares.

### You should see
- Shares show `Assigned`
- Assigned email is visible
- Download status starts as allowed

---

# TEST SCENARIO 14 - Share User First Login and MFA Setup

## What this step is for
Confirm SU can sign in to CS and complete MFA onboarding.

### Steps
1. Open Client Share portal.
2. Log in using SU email/password from GP.
3. If first login, scan QR code in authenticator app.
4. Enter OTP code.

### You should see
- MFA verification succeeds
- SU reaches dashboard

---

# TEST SCENARIO 15 - Multi-Token Selection in CS

## What this step is for
Confirm SU assigned to multiple tokens can choose token context.

### Steps
1. Log in as SU assigned to multiple tokens.
2. Complete OTP step.

### You should see
- Token selection step appears
- SU can select desired token and continue

---

# TEST SCENARIO 16 - One-Time Share Download

## What this step is for
Confirm share download is one-time by default.

### Steps
1. In CS dashboard, click `Download Share` for assigned share.
2. Confirm warning prompt.

### You should see
- File downloads successfully
- Share status changes to downloaded/disabled

### Additional test
1. Try downloading same share again.

### You should see
- Download is blocked
- User gets message to contact admin for re-enable

---

# TEST SCENARIO 17 - Download History in CS

## What this step is for
Confirm success and failure download attempts are logged.

### Steps
1. In CS dashboard, open Download History section.

### You should see
- Successful download entry exists
- Timestamp and status are shown
- IP shown when available

### Additional test
1. Trigger blocked re-download attempt.
2. Refresh history.

### You should see
- Failed log entry appears with reason

---

# TEST SCENARIO 18 - Admin Re-enables Share Download

## What this step is for
Confirm admin can re-enable lost-share access.

### Steps
1. In GP `Manage Shares`, locate disabled assignment.
2. Click `Re-enable`.
3. SU refreshes CS and downloads again.

### You should see
- GP shows allowed again
- CS download succeeds again
- New success history entry appears

---

# TEST SCENARIO 19 - Admin Unassigns Share

## What this step is for
Confirm unassignment removes user access.

### Steps
1. In GP `Manage Shares`, click `Unassign` for assigned share.
2. SU refreshes CS dashboard.

### You should see
- Share becomes unassigned in GP
- SU no longer sees that share in CS

---

# TEST SCENARIO 20 - Token Control First-Run Registration

## What this step is for
Confirm Token Control behaves like Mint on first run (register then close).

### Steps
1. Install Token Control on a new desktop.
2. Launch Token Control first time.

### You should see
- Registration completes
- Pending/registration message is shown
- App closes automatically
- Desktop appears in GP as `TokenControl` and `Pending`

---

# TEST SCENARIO 21 - Admin Approves Token Control Desktop

## What this step is for
Confirm admin legitimizes Token Control desktop before governance unlocks.

### Steps
1. In GP `Manage Desktops`, find Token Control desktop.
2. Click `Approve`.

### You should see
- Desktop status becomes `Active`

---

# TEST SCENARIO 22 - Admin Assigns Governance Users to Desktop

## What this step is for
Confirm governance authorities are mapped to specific desktop.

### Steps
1. In GP `Assign Desktops`, open Token Control desktop.
2. Select governance users.
3. Save assignments.

### You should see
- Selected governance users are assigned to this desktop
- They can see this desktop in governance view

---

# TEST SCENARIO 23 - Governance Approvals Reach Threshold

## What this step is for
Confirm multi-approver unlock works and one-user-one-approval rule is enforced.

### Steps
1. Governance User A logs in and approves desktop.
2. Governance User A tries approving again in same session.
3. Governance User B logs in and approves desktop.

### You should see
- First approval succeeds
- Duplicate approval in same session is blocked
- Desktop unlocks when threshold is met

---

# TEST SCENARIO 24 - Token Control Waiting State Before Unlock

## What this step is for
Confirm desktop remains locked until governance threshold is reached.

### Steps
1. Launch Token Control before threshold approvals are complete.

### You should see
- Lock/waiting message is shown
- Approval progress is visible
- App polls status until unlocked

---

# TEST SCENARIO 25 - Token Control Main Screen After Unlock

## What this step is for
Confirm unlocked desktop enters token control operations screen.

### Steps
1. Launch Token Control after threshold approvals are completed.

### You should see
- Main token operations screen loads
- Countdown is visible if unlock window has remaining time

---

# TEST SCENARIO 26 - Recover Treasury and Token in Token Control

## What this step is for
Confirm threshold share upload can recover treasury context.

### Steps
1. In Token Control, click `Recover`.
2. Select minimum threshold share files.
3. Complete recovery process.

### You should see
- Recovery succeeds
- Treasury address is populated
- Token/contract context is restored from chain or local snapshot

---

# TEST SCENARIO 27 - Unpause Token (First-Time Control)

## What this step is for
Confirm operator can switch token from paused to active transfers.

### Steps
1. In Token Control, check `System pause`.
2. If paused, switch to unpaused.

### You should see
- Pause operation succeeds
- Token status updates to transfers enabled

---

# TEST SCENARIO 28 - Transfer Tokens from Token Control

## What this step is for
Confirm treasury can transfer tokens to destination wallet.

### Steps
1. Enter destination address and token amount.
2. Submit transfer.

### You should see
- Transfer succeeds
- Balance and logs update
- Transaction hash is shown in operation result

---

# TEST SCENARIO 29 - Freeze and Unfreeze Address

## What this step is for
Confirm freeze controls work for compliance/emergency use.

### Steps
1. Enter target wallet.
2. Set action to `Freeze` and submit.
3. Repeat with `Unfreeze`.

### You should see
- Freeze succeeds and appears in frozen history
- Unfreeze succeeds and appears in unfrozen history

---

# TEST SCENARIO 30 - Retrieve/Reclaim Tokens from Source Address

## What this step is for
Confirm token reclaim operation works for frozen/lost cases.

### Steps
1. Enter source address in retrieve section.
2. Enter reason.
3. Click `Retrieve`.

### You should see
- Retrieve transaction succeeds
- Balances and operation logs update

---

# TEST SCENARIO 31 - Session Expiry and Re-Approval

## What this step is for
Confirm Token Control re-locks after unlock window and requires new approvals.

### Steps
1. Wait until unlock window expires.
2. Continue using app or refresh status.
3. Run governance approvals again.

### You should see
- App re-locks on expiry
- New approval session is required
- After new approvals, access is restored

---

# TEST SCENARIO 32 - ShareManager Load and Validate Share Files

## What this step is for
Confirm ShareManager loads valid share set and rejects mismatched files.

### Steps
1. Open ShareManager.
2. Load share files from same token set.
3. Try loading mixed shares from different sets as negative test.

### You should see
- Valid set loads with threshold summary
- Mismatched files are rejected with clear message

---

# TEST SCENARIO 33 - ShareManager Recover Mnemonic and Token Context

## What this step is for
Confirm ShareManager can recover mnemonic and find token/deployment info.

### Steps
1. Load threshold shares.
2. Click `Recover mnemonic`.
3. Use token lookup features (treasury or token address lookup).

### You should see
- Mnemonic recovery succeeds
- Treasury address is derived
- Token address/name and deployment ID can be populated

---

# TEST SCENARIO 34 - ShareManager Reconfigure Shares and Upload Rotation

## What this step is for
Confirm share policy can be rotated (new total and threshold).

### Steps
1. In ShareManager, set new share configuration.
2. Generate new shares.
3. Upload rotated share set.

### You should see
- New share files are generated
- Upload succeeds
- GP reflects updated share configuration

---

# TEST SCENARIO 35 - Verify Old Shares Become Inactive

## What this step is for
Confirm old share set is invalidated after rotation.

### Steps
1. After Scenario 34, inspect shares via GP/API/database view.

### You should see
- Old shares marked inactive
- New shares marked active
- Old assignments/downloads are no longer usable

---

# TEST SCENARIO 36 - Verify CS Only Shows Active Shares

## What this step is for
Confirm Share Users cannot access inactive (old) rotated shares.

### Steps
1. Log in to CS as assigned Share User after rotation.
2. Open assigned shares list.

### You should see
- Only active rotated shares are listed
- Old shares are not downloadable

---

# TEST SCENARIO 37 - Recovery Works with New Threshold Only

## What this step is for
Confirm disaster recovery uses new share policy after rotation.

### Steps
1. Attempt recovery using only old inactive shares.
2. Attempt recovery with new active shares meeting new threshold.

### You should see
- Old inactive set is rejected
- New active set recovers successfully

---

## IMPORTANT THINGS TO REMEMBER

- Desktop registration and desktop unlock are separate controls.
- Mint desktop requires admin approval and Mint unlock approval.
- Token Control desktop requires admin approval plus governance threshold approvals.
- Governance users can approve once per active session per desktop.
- Share download is one-time by default and fully logged.
- ShareManager rotation invalidates old active shares and enforces new policy.

---

## EXECUTION CHECKLIST

- [ ] Mint registration and approval flow validated
- [ ] Mint token deployment and share generation validated
- [ ] GP Share Management and SU assignment validated
- [ ] CS MFA and one-time download controls validated
- [ ] Token Control governance unlock and token operations validated
- [ ] Session expiry and re-approval validated
- [ ] ShareManager recovery and share rotation validated
- [ ] Old share invalidation and new share enforcement validated

---

## OUT OF SCOPE

- Deep smart-contract code audit
- Production infrastructure hardening checks
- External notification channel testing (SMS/phone integrations)

---

# END OF TEST PLAN
