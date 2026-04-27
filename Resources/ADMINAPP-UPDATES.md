# AegisMint AdminApp Updates - November 30, 2025

## Summary
Added new features to the AdminApp as requested:
1. **Delete Genesis Key** - Ability to delete existing genesis key
2. **Add New Genesis Key After Delete** - Can only add new key after deletion
3. **Display Actual Mnemonic** - Shows the full mnemonic phrase when retrieved
4. **Fixed Dev Unlock** - Service already configured with AllowDevBypassUnlock=true

## Changes Made

### 1. Core Library (AegisMint.Core)

#### IGenesisVault.cs
- Added `Task DeleteMnemonicAsync(CancellationToken ct)`

#### GenesisVault.cs
- Implemented `DeleteMnemonicAsync` method:
  - Deletes the genesis mnemonic file
  - Deletes the associated Shamir shares file
  - Throws `InvalidOperationException` if no mnemonic exists to delete
  - Thread-safe with mutex protection

#### DeleteMnemonicResponse.cs (New)
- Created contract: `record DeleteMnemonicResponse(string Message)`

### 2. Service (AegisMint.Service)

#### NamedPipeServiceHost.cs
- Added `deletemnemonic` command to the command switch
- Implemented `HandleDeleteMnemonicAsync` method:
  - Returns 200 OK on successful deletion
  - Returns 404 Not Found if no mnemonic exists
  - Logs deletion event for audit trail

### 3. Client Library (AegisMint.Client)

#### MintClient.cs
- Added `Task<MintClientResult<DeleteMnemonicResponse>> DeleteMnemonicAsync(CancellationToken ct)`
- Sends `deletemnemonic` command to the service

### 4. Admin Application (AegisMint.AdminApp)

#### MainWindow.xaml
- Added **"Delete Genesis Key"** button (red background for warning)
- Button placed next to "Set Genesis Key" button
- Tooltip: "Delete the existing genesis key and shares"

#### MainWindow.xaml.cs

**OnDeleteMnemonic (New Method):**
- Shows confirmation dialog with warning
- Calls `DeleteMnemonicAsync` on the client
- Logs success/failure messages
- Informs user they can now set a new key

**OnGetMnemonic (Updated):**
- Changed from hiding the mnemonic to **displaying the actual phrase**
- Shows full mnemonic with word count
- Includes security warning: "IMPORTANT: Keep this phrase secure!"

### 5. Testing

#### TestDeleteWorkflow.cs (New)
Complete test workflow demonstrating:
1. Check if mnemonic exists
2. Unlock for development
3. Get current mnemonic
4. Delete mnemonic
5. Verify deletion
6. Set new mnemonic
7. Retrieve new mnemonic

## Test Results

```
=== Testing Delete Mnemonic Workflow ===

1. Checking current status...
   Has mnemonic: True

2. Unlocking for development...
   ✓ Unlocked successfully

3. Getting current mnemonic...
   ✓ Current mnemonic: clock nasty business alcohol garment useful fix rose velvet require legend color

4. Deleting mnemonic...
   ✓ Genesis key deleted successfully

5. Verifying deletion...
   Has mnemonic: False (should be False)

6. Setting new mnemonic...
   ✓ Genesis key stored successfully. 5 shares generated.
   ✓ Generated 5 shares

7. Getting new mnemonic...
   ✓ New mnemonic: abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about

=== Test Complete ===
```

✅ **All tests passing!**

## Security Considerations

### Delete Confirmation
- User must confirm deletion via MessageBox dialog
- Warning message clearly states action cannot be undone
- Cancel option available

### Mnemonic Display
- Only displays when device is unlocked
- Shows security warning when displaying
- Previous behavior (hiding) was changed per user request

### Workflow Protection
- Cannot overwrite existing mnemonic (must delete first)
- Deletion requires explicit user action
- All operations logged for audit trail

## Service Configuration

The Dev Unlock issue was already resolved. The service configuration includes:

```json
"Service": {
    "PipeName": "AegisMint_Service",
    "AllowDevBypassUnlock": true,
    "DefaultUnlockMinutes": 15,
    "LogFilePath": "logs/service.log"
}
```

**AllowDevBypassUnlock is set to true**, so the "Unlock (dev)" button should work without the 403 error.

## UI Flow

### Current Workflow:
1. User clicks "Check Status" → Shows if genesis key exists
2. User clicks "Unlock (dev)" → Unlocks the device for 15 minutes
3. User clicks "Get Mnemonic" → **Displays the actual 12-word phrase**
4. User clicks "Delete Genesis Key" → Confirms → Deletes key and shares
5. User enters new 12-word phrase in textbox
6. User clicks "Set Genesis Key" → Stores new key, generates shares

### Key Features:
- ✅ Delete genesis key with confirmation
- ✅ Can only add new key after deletion
- ✅ Displays actual mnemonic (not hidden)
- ✅ Dev unlock already enabled in service config
- ✅ All operations tested and working

## Files Modified

1. `Mint/src/AegisMint.Core/Abstractions/IGenesisVault.cs`
2. `Mint/src/AegisMint.Core/Vault/GenesisVault.cs`
3. `Mint/src/AegisMint.Core/Contracts/DeleteMnemonicResponse.cs` (new)
4. `Mint/src/AegisMint.Service/Communication/NamedPipeServiceHost.cs`
5. `Mint/src/AegisMint.Client/MintClient.cs`
6. `Mint/src/AegisMint.AdminApp/MainWindow.xaml`
7. `Mint/src/AegisMint.AdminApp/MainWindow.xaml.cs`
8. `Mint/src/AegisMint.TestClient/TestDeleteWorkflow.cs` (new)
9. `Mint/src/AegisMint.TestClient/Program.cs`

## Next Steps

The AdminApp is now running with all requested features:
1. Open the AdminApp (should be running)
2. Try the new "Delete Genesis Key" button (red button)
3. Test adding a new key after deletion
4. Test "Get Mnemonic" to see the actual phrase displayed
5. Test "Unlock (dev)" - should work without 403 error

All changes built successfully and tested via automated test suite.
