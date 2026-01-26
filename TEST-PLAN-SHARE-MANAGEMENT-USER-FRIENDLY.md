# Share Management - User-Friendly Test Plan
(Non-Technical)

---

## PURPOSE OF THIS DOCUMENT

This document explains how to test the Share Management system end-to-end in plain language.
You do not need to know cryptography or blockchain details to follow this guide.

---

## SYSTEM OVERVIEW (IN SIMPLE TERMS)

The system has three parts:

1) Desktop Mint App
   - Creates the token and generates share files
   - Uploads shares to the backend

2) Admin Web Portal (Web)
   - Creates token users
   - Assigns shares to those users
   - Re-enables downloads and views status

3) Share Portal (ClientWeb)
   - Token users log in with MFA
   - Download their assigned shares
   - View download history

---

## WHO DOES WHAT

- Admin (Super Admin)
  - Manages token deployments
  - Creates token users and assigns shares
  - Re-enables downloads when needed

- Token User
  - Logs in with MFA
  - Downloads assigned shares
  - Views download history

---

## TEST DATA CHECKLIST

Before you begin:
- Backend API is running (Web backend)
- ClientWeb backend proxy is running
- Admin portal and Share Portal frontends are running
- At least one token deployment exists
- Shares are uploaded for that token

If no shares exist yet, mint a token in the desktop app to generate and upload them.

---

# TEST SCENARIO 1 - Shares Uploaded and Visible to Admin

## What this step is for
Confirm that shares uploaded by the desktop app appear in the Admin portal.

### Steps
1. Log in to the Admin portal as Super Admin.
2. Open Share Management -> Tokens.
3. Expand a token and click Manage Shares.

### You should see
- A list of shares for that token
- Share numbers and file names
- Unassigned status if no users are assigned yet

---

# TEST SCENARIO 2 - Create Token User and Assign to Token

## What this step is for
Confirm that token users can be created and assigned to tokens.

### Steps
1. In Tokens list, click Add User for a token.
2. Enter name, email, phone (optional), and password.
3. Save the user.

### You should see
- The user appears under the token
- No error about email duplication

### Additional test - same email for another token
1. Go to a different token and add the same email.
2. Confirm the dialog that says the email already exists.
3. Choose Yes to assign the same user.

### You should see
- The user is now assigned to both tokens
- No duplicate user records on the token list

---

# TEST SCENARIO 3 - Assign Share to Token User

## What this step is for
Confirm that shares can be assigned to a token user.

### Steps
1. Open a token in Share Management and view its shares.
2. Pick an unassigned share and click Assign.
3. Choose a token user and confirm.

### You should see
- The share shows as Assigned
- The user's email is shown
- Download status shows Allowed

---

# TEST SCENARIO 4 - Token User Login with MFA

## What this step is for
Confirm token users can log in and complete MFA setup.

### Steps
1. Open the Share Portal (ClientWeb).
2. Log in with the token user's email and password.
3. If this is the first login, scan the QR code in an authenticator app.
4. Enter the 6-digit OTP.

### You should see
- The dashboard loads
- Assigned shares are visible

### If the user has access to multiple tokens
- You should see a token selection screen
- Select a token to continue

---

# TEST SCENARIO 5 - Download Share (One-Time)

## What this step is for
Confirm download works and is disabled after first download.

### Steps
1. In Share Portal, click Download for an assigned share.
2. Confirm the prompt.

### You should see
- The file downloads to your machine
- The share status changes to Downloaded

### Try downloading again
- You should get a message that download is not allowed

---

# TEST SCENARIO 6 - Download History

## What this step is for
Confirm download history shows successful and failed attempts.

### Steps
1. In Share Portal, scroll to Download History.

### You should see
- A history row for the download
- Status = Success
- IP address shown if available

### Optional - failed attempt
1. Attempt to download a share that is already disabled.
2. Refresh Download History.

### You should see
- A new row with Status = Failed
- A failure reason

---

# TEST SCENARIO 7 - Admin Re-enables Download

## What this step is for
Confirm admin can re-enable a download.

### Steps
1. In Admin portal, open the token and Manage Shares.
2. Find the share with Download Disabled.
3. Click Re-enable and confirm.

### You should see
- Download status changes to Allowed

### Back in Share Portal
1. Refresh the dashboard.
2. Download the share again.

### You should see
- The download succeeds
- History shows a new Success row

---

# TEST SCENARIO 8 - Unassign Share

## What this step is for
Confirm that unassign removes access for the user.

### Steps
1. In Admin portal, open the token and Manage Shares.
2. Click Unassign for the share.

### You should see
- The share becomes Unassigned

### Back in Share Portal
1. Refresh the dashboard.

### You should see
- The share is no longer listed

---

# IMPORTANT THINGS TO REMEMBER

- A share can be downloaded once by default
- Admin can re-enable a download if needed
- Download history records both success and failure attempts
- Token users are global by email and can be assigned to multiple tokens

---

# END OF TEST PLAN
