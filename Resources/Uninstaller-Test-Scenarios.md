# AegisMint Uninstaller Test Scenarios

This document provides comprehensive test scenarios to validate the enhanced uninstaller functionality for both AegisMint.Mint and AegisMint.TokenControl applications.

## Overview

The enhanced uninstaller handles:
- Running process detection and automatic termination
- Locked file detection and retry mechanisms
- Scheduled deletion on reboot for stubborn locks
- Complete cleanup of Program Files and AppData folders
- User-friendly prompts and clear error messages

---

## Test Scenario 1: Normal Uninstall (Baseline)

**Goal:** Verify clean uninstall with no locks

**Steps:**
1. Install AegisMint Token Control (or Mint)
2. Close all AegisMint applications
3. Run uninstaller from Control Panel or Start Menu
4. When asked "Do you want to delete all application data?", choose **YES**

**Expected Results:**
- Quick uninstall with no reboot required
- Both folders deleted successfully:
  - `C:\Program Files\AegisMint\TokenControl` (or `\Mint`)
  - `C:\Users\<YourName>\AppData\Local\AegisMint`
- No error messages or warnings

---

## Test Scenario 2: Application Running

**Goal:** Test process detection and automatic termination

**Steps:**
1. Install and launch `AegisMint.TokenControl.exe`
2. Keep the application running
3. Start the uninstaller

**Expected Results:**
Dialog appears showing:
```
The following AegisMint processes are currently running:

  • AegisMint.TokenControl.exe (PID: 12345)

These processes must be closed before uninstalling.
Click OK to automatically close them, or Cancel to abort uninstallation.
```

4. Click **OK**

**Expected Results:**
- Process terminates automatically
- Uninstall completes successfully
- No reboot required

---

## Test Scenario 3: User Refuses Process Termination

**Goal:** Verify cancellation works properly

**Steps:**
1. Launch `AegisMint.TokenControl.exe`
2. Start uninstaller
3. When process termination dialog appears, click **Cancel**

**Expected Results:**
- Uninstaller exits immediately
- Application continues running
- Nothing is deleted or modified
- User can close app manually and retry uninstall

---

## Test Scenario 4: Files Locked by Explorer/Editor

**Goal:** Test locked file handling and reboot scheduling

**Steps:**
1. Install the application
2. Navigate to `C:\Program Files\AegisMint\TokenControl`
3. Open `appsettings.json` in Notepad - **DON'T CLOSE IT**
4. In Windows Explorer, open the installation folder and select multiple files
5. Run uninstaller, choose **YES** to delete data

**Expected Results:**
Dialog appears:
```
Some files could not be deleted because they are in use.

They have been scheduled for deletion after system restart.

Please restart your computer to complete the uninstallation.
```

6. Restart computer
7. After restart, verify both folders are completely gone

**Verification Command (before restart):**
```powershell
Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name PendingFileRenameOperations
```

---

## Test Scenario 5: Multiple Running Processes

**Goal:** Test handling of multiple processes simultaneously

**Steps:**
1. Install both AegisMint.Mint and AegisMint.TokenControl
2. Launch BOTH applications simultaneously
3. Run TokenControl uninstaller

**Expected Results:**
Dialog lists ALL AegisMint processes:
```
The following AegisMint processes are currently running:

  • AegisMint.Mint.exe (PID: 12345)
  • AegisMint.TokenControl.exe (PID: 67890)

These processes must be closed before uninstalling.
Click OK to automatically close them, or Cancel to abort uninstallation.
```

4. Click **OK**

**Expected Results:**
- Both processes killed simultaneously
- Uninstall completes successfully for TokenControl
- Mint application closes but its installation remains

---

## Test Scenario 6: Keep Application Data

**Goal:** Verify selective deletion works correctly

**Steps:**
1. Install and use the application to create vault/database files
2. Close all applications
3. Run uninstaller
4. When asked about deleting data, choose **NO**

**Expected Results:**
- `C:\Program Files\AegisMint\TokenControl` deleted
- `C:\Users\<YourName>\AppData\Local\AegisMint` remains intact with all user data

5. Reinstall the same or newer version

**Expected Results:**
- Old data is picked up automatically
- Vault database and keys are preserved

---

## Test Scenario 7: Database File Locked

**Goal:** Test SQLite database lock handling

**Steps:**
1. Install and run the application to create the vault database
2. Close the main application
3. Open the database file with a SQLite tool or run:
   ```powershell
   cd "C:\Users\<YourName>\AppData\Local\AegisMint"
   sqlite3 vault.db
   ```
4. Keep the database connection open
5. Run uninstaller and choose **YES** to delete data

**Expected Results:**
- Reboot notification appears
- Database deletion scheduled for next boot
- After restart, AppData folder is completely removed

---

## Test Scenario 8: Mixed Locks (Process + Files)

**Goal:** Test complex scenario with multiple blocking factors

**Steps:**
1. Launch `AegisMint.TokenControl.exe`
2. Open `appsettings.json` from install directory in Notepad
3. Open a database file from AppData in a hex editor or text editor
4. Run uninstaller, choose **YES** for data deletion

**Expected Results:**
- First dialog asks to close running processes, click **OK**
- Application terminates automatically
- Second dialog notifies about locked files requiring reboot
- After system restart, all files and folders deleted

---

## Test Scenario 9: Reinstall Over Old Version

**Goal:** Verify the original problem (locked files preventing clean install) is solved

**Steps:**
1. Install version 0.1.0
2. Use the app to create vault data
3. Open a configuration file in Notepad (keep it open)
4. Try to uninstall but click **Cancel** when asked about file locks
5. Without closing Notepad, install version 0.1.1 over existing installation

**Expected Results:**
- Installer proceeds with upgrade
- Prompts to close running processes if any
- Successfully upgrades with minimal issues
- Old locked files don't interfere with new installation

---

## Test Scenario 10: Permission Denied / Admin Rights

**Goal:** Test behavior with insufficient permissions

**Steps:**
1. Install as administrator
2. Log in as a standard user (non-admin)
3. Try to run uninstaller

**Expected Results:**
- UAC prompt appears requesting admin credentials
- After providing admin credentials, uninstaller proceeds normally
- All features work as expected

---

## Testing Tools & Commands

### PowerShell Commands for Testing

**Simulate locked file (keep handle open):**
```powershell
$file = [System.IO.File]::Open("C:\Program Files\AegisMint\TokenControl\appsettings.json", 'Open', 'Read', 'None')
# Run uninstaller now while this PowerShell window is open
# When done testing:
$file.Close()
```

**Monitor AegisMint processes in real-time:**
```powershell
while ($true) { 
    Get-Process AegisMint* -ErrorAction SilentlyContinue | 
    Select-Object Name, Id, StartTime | 
    Format-Table -AutoSize
    Start-Sleep -Seconds 2
    Clear-Host 
}
```

**Check pending file operations (scheduled deletions):**
```powershell
Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name PendingFileRenameOperations -ErrorAction SilentlyContinue
```

**Verify folders exist:**
```powershell
Test-Path "C:\Program Files\AegisMint\TokenControl"
Test-Path "C:\Program Files\AegisMint\Mint"
Test-Path "$env:LOCALAPPDATA\AegisMint"
```

**List all files in installation:**
```powershell
Get-ChildItem "C:\Program Files\AegisMint" -Recurse | Select-Object FullName
Get-ChildItem "$env:LOCALAPPDATA\AegisMint" -Recurse | Select-Object FullName
```

---

## Expected Results Summary

| Scenario | Processes Killed? | Files Deleted Immediately? | Reboot Required? | Notes |
|----------|------------------|---------------------------|------------------|-------|
| 1 - Normal | N/A | ✅ Yes | ❌ No | Baseline test |
| 2 - App Running | ✅ Yes | ✅ Yes | ❌ No | Auto-termination works |
| 3 - User Cancels | ❌ No | ❌ No | ❌ No | Safe abort |
| 4 - Explorer Lock | N/A | ⚠️ Partial | ✅ Yes | Scheduled deletion |
| 5 - Multiple Processes | ✅ Yes | ✅ Yes | ❌ No | All processes handled |
| 6 - Keep Data | N/A | ⚠️ Program Files Only | ❌ No | Selective deletion |
| 7 - DB Locked | N/A | ⚠️ Partial | ✅ Yes | Database lock handled |
| 8 - Mixed Locks | ✅ Yes | ⚠️ Partial | ✅ Yes | Complex scenario |
| 9 - Reinstall | Varies | Varies | Varies | Upgrade path |
| 10 - Permissions | N/A | ✅ Yes | ❌ No | UAC handling |

---

## Common Issues & Troubleshooting

### Issue: Uninstaller doesn't detect running process

**Cause:** Process name doesn't contain "AegisMint"

**Solution:** Check process name in Task Manager. The WMI query uses `LIKE '%AegisMint%'`

### Issue: Files still present after reboot

**Cause:** Scheduled deletion failed or wasn't registered

**Verification:**
```powershell
Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name PendingFileRenameOperations
```

**Solution:** Manually delete or check for permission issues

### Issue: "Access Denied" when killing process

**Cause:** Process owned by different user or system process

**Solution:** Run uninstaller as administrator

### Issue: Installer fails with "Directory not empty"

**Cause:** Previous uninstall didn't complete

**Solution:** 
1. Manually delete folders
2. Restart computer
3. Reinstall

---

## Test Sign-Off Checklist

- [ ] Scenario 1: Normal uninstall - PASSED
- [ ] Scenario 2: Application running - PASSED
- [ ] Scenario 3: User cancellation - PASSED
- [ ] Scenario 4: Locked files - PASSED
- [ ] Scenario 5: Multiple processes - PASSED
- [ ] Scenario 6: Keep data option - PASSED
- [ ] Scenario 7: Database locked - PASSED
- [ ] Scenario 8: Mixed locks - PASSED
- [ ] Scenario 9: Version upgrade - PASSED
- [ ] Scenario 10: Admin permissions - PASSED

**Tester Name:** _________________

**Test Date:** _________________

**Version Tested:** _________________

**Overall Result:** PASS / FAIL

**Notes:**
```
[Additional observations or issues encountered]
```

---

## Conclusion

The enhanced uninstaller ensures complete removal of AegisMint applications even when "silly" users leave files open or processes running. The automatic process termination, retry logic, and scheduled deletion features provide a robust solution that handles edge cases gracefully while maintaining user control and transparency.
