
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

**Expected Results**:
- Locked screen displays with padlock icon (🔒)
- Initial message: "Checking authorization..."
- Registration confirmation message: "Your application has been registered and is pending approval by an administrator. The application will close now. Please restart after approval."
- Application automatically closes after approximately
- 
**What You Should See**:
- A locked screen appears with a padlock icon (🔒)
- Message says: "Checking authorization..."
- After a few seconds, message changes to: "Your application has been registered and is pending approval by an administrator. The application will close now. Please restart after approval."
- Application automatically closes

 <img width="976" height="1036" alt="image" src="https://github.com/user-attachments/assets/1ccd6555-8d31-4c31-b8c6-27cbd5e29b3c" />
 

---

### Step 2.2: Verify Pending Desktop Registration

**Purpose**: Confirm that administrators can view newly registered desktops awaiting approval.

**Test Steps**:
1. Open a web browser
2. Navigate to the Admin Portal: https://apkserve.com/governance
3. Log in using admin credentials
4. Select "Manage Desktops" from the navigation menu
5. Locate the newly registered desktop in the pending list

**Expected Results**:
- Desktop appears in the pending desktops list
- Status indicator displays "Pending" (yellow/orange color)
- Desktop details are visible:
  - Computer name and Windows username
  - Unique DesktopAppId
  - Required Approvals: 2
  - Unlock Duration: 15 minutesor orange
- You can see:
  - Computer name & Windows username
  - DesktopAppId
  - Required Approvals: 2
  - Unlock Duration: 15 minutes

Screen 1: Login screen
<img width="1148" height="499" alt="image" src="https://github.com/user-attachments/assets/9a1b9acc-1e16-4ec6-8d80-792f37fa29de" />

Screen 2: Manage Desktops
<img width="1142" height="671" alt="image" src="https://github.com/user-attachments/assets/8c05c845-24eb-42fc-9a50-71e8c9c9e749" />


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

Screen 1: Pending Status, Approve button and Reject button
<img width="1142" height="671" alt="image" src="https://github.com/user-attachments/assets/443ae26f-d9d0-468b-a5b1-1cdddccc9c30" />

Screen 2: Active status, Edit button with Disable button
<img width="1145" height="661" alt="image" src="https://github.com/user-attachments/assets/73860a5a-84b9-42a3-93fb-daf86062148f" />

---

### Step 2.4: Assign Desktop to Governance Authorities

**Purpose**: Verify that administrators can assign desktops to governance users who will provide multi-party authorization.

**Method 1: Using the Assign Desktops Page (Recommended)**

**Test Steps**:
1. Navigate to the "Assign Desktops" tab in the Admin Portal
2. Locate your newly approved desktop in the grid of desktop cards
3. Click on the desktop card to open the assignment dialog
4. In the assignment dialog:
   - Review the list of all governance users
   - Check the boxes next to "gov@example.com" and "new2@example.com"
   - (Optional) Use the search box to filter users if there are many
5. Click "Save Assignments"
6. Verify the dialog closes

**Expected Results**:
- Desktop cards are displayed in a visual grid layout
- Each card shows desktop icon, name, status badge, and configuration details
- Assignment dialog opens showing all governance users with checkboxes
- Checked boxes indicate which users are assigned
- Search functionality helps filter users by email
- Dialog closes automatically after saving
- Both governance users now have access to the desktop

Screen 1: Assign desktops
<img width="1152" height="526" alt="image" src="https://github.com/user-attachments/assets/5b062c91-a25e-47fb-b05e-aefda8a2551c" />

Screen 2: Click on desktop to assign the selected desktop to governance user
<img width="1152" height="814" alt="image" src="https://github.com/user-attachments/assets/f67bd6f8-fa06-40c5-88c1-2a9c3a688e90" />


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

Screen: Waiting for authorization
<img width="973" height="1036" alt="image" src="https://github.com/user-attachments/assets/183b4975-9ca3-41d7-a976-5b12c0c320e8" />


---

## Step 2.1 – Launch Token Control

1. Open **Token Control**

You will see:
- A lock icon
- “Checking authorization…”

### Possible outcomes

Screen 1: Governance dashboard. User: gov@example.com
<img width="1161" height="581" alt="image" src="https://github.com/user-attachments/assets/bf38d90f-e0ef-40d5-b825-6933b31110fd" />

Screen 2: Approved by gov@example.com
<img width="1156" height="584" alt="image" src="https://github.com/user-attachments/assets/7359e060-2dac-4151-9f62-743772d6f808" />

Screen 3: Token Control wait for one more approval
<img width="969" height="1032" alt="image" src="https://github.com/user-attachments/assets/380184ab-fe50-45b9-8ba1-8b0d59f287a9" />


**B. Already registered**
- Message says approvals are required
- App stays open

Screen 1: Governance dashboard. User: new2@example.com
<img width="1152" height="543" alt="image" src="https://github.com/user-attachments/assets/9d12d3f6-70b1-45a9-90d9-93e935dc0b45" />

Screen 2: Approved by new2@example.com
<img width="1146" height="555" alt="image" src="https://github.com/user-attachments/assets/d83c42a6-97d2-4ceb-9e46-5bde05124e37" />




---

## Step 2.2 – Admin Verifies Registration

1. Admin logs into the governance website
2. Opens **Manage Desktops**

You should see:
- The computer listed
- Status = Pending

Screen 1: Token Control unlocked
<img width="988" height="1051" alt="image" src="https://github.com/user-attachments/assets/6d71f79c-ef29-4f3b-a995-67f0b83086a4" />


---

## Step 2.3 – Admin Approves Computer

1. Admin clicks **Approve**

Screen 1: No information and recover button is available
<img width="988" height="1050" alt="image" src="https://github.com/user-attachments/assets/62f7e5af-e311-42ad-bbf2-2c585956c4fd" />

Screen 2: Process of recovery 
<img width="972" height="1030" alt="image" src="https://github.com/user-attachments/assets/1f7ef4e6-7dfd-4846-9c66-6e3e99613c80" />

Screen 3: After recovery done from the minimum shares
<img width="982" height="1040" alt="image" src="https://github.com/user-attachments/assets/a33bc39f-4691-423d-9e1e-761c9e6a266a" />
#This is a demo scree, in actual scenario the contract number will be recovred from blockchain.


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
