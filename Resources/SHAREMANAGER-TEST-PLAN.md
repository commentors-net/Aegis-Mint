# AegisMint Share Manager – User-Friendly Test Plan
(Non-Technical)

---

## PURPOSE OF THIS DOCUMENT

This document explains **how to test the Share Manager desktop application** in clear, simple language.
You do **not** need to understand cryptography or blockchain internals to follow this guide.

The Share Manager is used to **recover and reconfigure security shares** for token deployments.

---

## SYSTEM OVERVIEW (IN SIMPLE TERMS)

The Share Manager has **two main functions**:

1. **Recovery**
   - Load share files
   - Recover the secret mnemonic
   - Look up token information

2. **Reconfiguration**
   - Change how many shares exist
   - Change how many shares are needed for recovery
   - Generate new shares and replace old ones

---

## WHO DOES WHAT

- **Token Administrator**
  - Securely stores share files
  - Uses Share Manager to recover or reconfigure shares
  - Has access to backend API for upload operations

---

## TEST DATA CHECKLIST

Before you begin:
- Windows desktop with AegisMint.ShareManager installed
- Backend API is running if testing uploads
- Database migration for soft delete is applied
- At least one set of `.aegisshare` files from a token deployment
- Network RPC access (Sepolia, Mainnet, or Localhost)

For Localhost testing:
- Running node on `http://127.0.0.1:7545`
- Token deployed from the treasury address

---

# TEST SCENARIO 1 – Load and Validate Share Files

## What this step is for
Confirm that the application can load share files and validate they belong to the same set.

### Steps
1. Open **Share Manager** application.
2. Click **Load share files...** button.
3. Select valid `.aegisshare` files from the same token deployment.

### You should see
- Share list shows each file name
- Threshold text shows correct format (e.g., "3-of-8")
- File count displayed
- No error messages

---

# TEST SCENARIO 2 – Reject Invalid Share Combinations

## What this step is for
Confirm that mismatched shares from different deployments are rejected.

### Steps
1. Click **Load share files...**.
2. Select shares from **different token deployments** or with different thresholds.

### You should see
- Error message indicating metadata mismatch
- Status bar shows which files were rejected
- Only valid shares are added to the list

---

# TEST SCENARIO 3 – Recover Mnemonic from Shares

## What this step is for
Confirm that loading enough shares allows recovery of the secret mnemonic.

### Steps
1. Load at least the threshold number of shares (e.g., 3 shares if threshold is 3-of-8).
2. Click **Recover mnemonic** button.

### You should see
- Operation log shows "Recovering mnemonic from X shares..."
- Mnemonic phrase appears (12 or 24 words)
- Primary Address and Secondary Address displayed
- Treasury Address field auto-filled with primary address
- Success message in operation log

---

# TEST SCENARIO 4 – Copy Mnemonic to Clipboard

## What this step is for
Confirm the mnemonic can be copied for backup or use elsewhere.

### Steps
1. After recovering mnemonic (see Scenario 3).
2. Click **Copy** button next to the mnemonic.
3. Open a text editor and paste.

### You should see
- Mnemonic phrase appears in the text editor
- All words are separated by spaces
- No extra formatting or characters

---

# TEST SCENARIO 5 – Lookup Token by Treasury Address (On-Chain)

## What this step is for
Confirm that the application can find token contracts deployed by a treasury address.

### Steps
1. After recovering mnemonic (Treasury Address is filled), or manually enter a known treasury address.
2. Select a network from the **Network** dropdown (Sepolia, Mainnet, or Localhost).
3. Click **Lookup by treasury** button.

### You should see
- Operation log shows "Looking up token by treasury..."
- Progress indicator appears
- Token Address field populated with contract address
- Token Name field populated (e.g., "FEB04")
- Success message: "✓ Found token: [name] at [address]"

---

# TEST SCENARIO 6 – Lookup Fails for Non-Existent Contract

## What this step is for
Confirm proper error handling when no contract exists for the treasury.

### Steps
1. Enter a treasury address that has **not deployed any contracts** on the selected network.
2. Click **Lookup by treasury**.

### You should see
- Operation log shows "Looking up token by treasury..."
- Progress indicator appears briefly
- Error message: "No contract found for treasury..."
- Token Address and Token Name remain empty

---

# TEST SCENARIO 7 – Lookup Deployment ID by Token Address

## What this step is for
Confirm that the application can query the backend for deployment IDs.

### Steps
1. Ensure **Backend API is running** and reachable.
2. Enter a known **Token Address** (or use value from Scenario 5).
3. Click **Lookup by address** button.

### You should see
- Operation log shows "Looking up deployment by contract address..."
- Progress indicator appears
- Deployment ID field populated with GUID
- Success message in operation log

---

# TEST SCENARIO 8 – Validate Reconfiguration Inputs

## What this step is for
Confirm that invalid share configurations are rejected before generation.

### Steps
1. Load shares and recover mnemonic.
2. Enter **Token Name**, ensure **Token Address** and **Deployment ID** are set.
3. Set **New Threshold** higher than **New Total** (e.g., Threshold=5, Total=3).
4. Click **Generate & Upload New Shares**.

### You should see
- Error message: "Threshold cannot exceed total shares"
- No files generated
- No upload attempted

### Additional tests
Repeat with these invalid inputs:
- Total < 2
- Threshold < 2
- Total > 99
- Threshold > Total

Each should show a clear validation error.

---

# TEST SCENARIO 9 – Generate and Upload New Share Configuration

## What this step is for
Confirm that new shares can be generated and uploaded, replacing old shares.

### Steps
1. Load valid shares and recover mnemonic.
2. Fill in all required fields:
   - Token Name (e.g., "FEB04")
   - Token Address (from Scenario 5 or manual entry)
   - Deployment ID (from Scenario 7)
   - New Total (e.g., 8)
   - New Threshold (e.g., 5)
   - Output Folder (browse to a writable location)
3. Click **Generate & Upload New Shares** button.

### You should see
- Operation log shows detailed progress:
  - "Encrypting share vault..."
  - "Generating 8 shares with 5-of-8 threshold..."
  - "Generated 8 shares"
  - "Writing share files to [folder]..."
  - "✓ Wrote 8 share files"
  - "Uploading share rotation..."
  - "✓ API response: Created 8 shares"
- New `.aegisshare` files in output folder
  - File names like: `020926FEB0410108.aegisshare` to `020926FEB0410808.aegisshare`
  - Total count matches New Total
- Success message in status bar

---

# TEST SCENARIO 10 – Verify Backend Soft Delete Behavior

## What this step is for
Confirm that old shares are marked inactive but not deleted from the database.

### Steps
1. Complete Scenario 9 (generate and upload new shares).
2. Open database tool and query the `share_files` table for the token deployment.
3. Look for shares with `share_number` from the old configuration.

### You should see (in database)
- Old shares:
  - `is_active = 0` (or false)
  - `replaced_at_utc` has a timestamp
- New shares:
  - `is_active = 1` (or true)
  - `replaced_at_utc = NULL`
- All shares still exist in the table (none deleted)

---

# TEST SCENARIO 11 – Client Download Excludes Inactive Shares

## What this step is for
Confirm that only active shares are available to users in ClientWeb portal.

### Steps
1. After completing Scenario 9, log into **ClientWeb** as a token user.
2. Navigate to share download page.
3. Attempt to download shares.

### You should see
- Only **new shares** (from latest reconfiguration) are available
- Old share numbers do not appear in the download list
- Download count reflects new total (e.g., 8 shares if reconfigured to 8)

---

# TEST SCENARIO 12 – UI Elements Readable in All States

## What this step is for
Confirm that buttons and controls are readable in enabled and disabled states.

### Steps
1. Open Share Manager with **no shares loaded**.
2. Observe button states.

### You should see
- **Recover mnemonic** button is grayed out but text is readable
- **Generate & Upload New Shares** button is grayed out but text is readable
- **Network** dropdown text is readable in dark theme
- All disabled controls have sufficient contrast

### Additional test
1. Load shares.
2. Confirm buttons become enabled and remain readable.

---

## EXPECTED LOGS

Throughout testing, the **Operation Log** panel should display:
- Timestamped entries for each action
- Detailed progress messages (e.g., "Encrypting...", "Generating shares...")
- Success indicators (✓) for completed operations
- Error messages with clear descriptions

---

## TROUBLESHOOTING NOTES

**On-chain lookup doesn't find contract:**
- The lookup scans recent transactions. If the token was deployed long ago, the scan may not reach far enough.
- Solution: Manually enter the Token Address and use Scenario 7 to get Deployment ID.

**Backend upload fails:**
- Ensure Backend API is running and accessible.
- Check that authentication token is valid (desktop auth service must be running).

**Localhost testing:**
- Requires a local Ethereum node running on `http://127.0.0.1:7545`.
- Token must be deployed from the same treasury address being tested.

---

## WHAT THIS TESTING COVERS

✓ Share file loading and validation  
✓ Mnemonic recovery from threshold shares  
✓ On-chain token lookup by treasury address  
✓ Backend deployment ID lookup  
✓ Share reconfiguration (generate + upload)  
✓ Input validation for share parameters  
✓ Backend soft delete (old shares marked inactive)  
✓ Client portal excludes inactive shares  
✓ UI readability in all states  

## OUT OF SCOPE

✗ Token deployment or minting flows  
✗ Governance web UI operations  
✗ Deep cryptographic validation  
✗ User assignment and download workflows (covered in Share Management test plan)
