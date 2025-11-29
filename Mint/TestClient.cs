using AegisMint.Client;

Console.WriteLine("Testing AegisMint Named Pipe Communication...");
Console.WriteLine();

using var client = MintClient.CreateDefault();

try
{
    Console.WriteLine("1. Testing Ping...");
    var pingResult = await client.PingAsync();
    if (pingResult.Success && pingResult.Value != null)
    {
        Console.WriteLine($"   ✓ Ping successful: {pingResult.Value.Status} at {pingResult.Value.Utc:O}");
    }
    else
    {
        Console.WriteLine($"   ✗ Ping failed ({pingResult.StatusCode}): {pingResult.ErrorMessage}");
    }
    Console.WriteLine();

    Console.WriteLine("2. Getting Device Info...");
    var deviceResult = await client.GetDeviceInfoAsync();
    if (deviceResult.Success && deviceResult.Value != null)
    {
        var device = deviceResult.Value.Device;
        Console.WriteLine($"   ✓ Device ID: {device.DeviceId}");
        Console.WriteLine($"   ✓ Shares: {device.RecoveryThreshold}/{device.ShareCount}");
        Console.WriteLine($"   ✓ Governance Quorum: {device.GovernanceQuorum}");
        Console.WriteLine($"   ✓ Unlock Window: {device.UnlockWindowMinutes} minutes");
    }
    else
    {
        Console.WriteLine($"   ✗ Device info failed ({deviceResult.StatusCode}): {deviceResult.ErrorMessage}");
    }
    Console.WriteLine();

    Console.WriteLine("3. Testing Dev Unlock...");
    var unlockResult = await client.UnlockForDevelopmentAsync(15);
    if (unlockResult.Success && unlockResult.Value != null)
    {
        Console.WriteLine($"   ✓ Unlocked until: {unlockResult.Value.ExpiresAt:O}");
    }
    else
    {
        Console.WriteLine($"   ✗ Unlock failed ({unlockResult.StatusCode}): {unlockResult.ErrorMessage}");
    }
    Console.WriteLine();

    Console.WriteLine("4. Getting Mnemonic (should work now)...");
    var mnemonicResult = await client.GetMnemonicAsync();
    if (mnemonicResult.Success && mnemonicResult.Value != null)
    {
        var wordCount = mnemonicResult.Value.Mnemonic?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        Console.WriteLine($"   ✓ Mnemonic retrieved ({wordCount} words, value hidden for security)");
    }
    else
    {
        Console.WriteLine($"   ✗ Mnemonic failed ({mnemonicResult.StatusCode}): {mnemonicResult.ErrorMessage}");
    }
    Console.WriteLine();

    Console.WriteLine("5. Testing Lock...");
    var lockResult = await client.LockAsync();
    if (lockResult.Success && lockResult.Value != null)
    {
        Console.WriteLine($"   ✓ Device locked: {lockResult.Value.Status}");
    }
    else
    {
        Console.WriteLine($"   ✗ Lock failed ({lockResult.StatusCode}): {lockResult.ErrorMessage}");
    }
    Console.WriteLine();

    Console.WriteLine("6. Getting Recent Logs...");
    var logsResult = await client.GetRecentLogsAsync(10);
    if (logsResult.Success && logsResult.Value != null)
    {
        Console.WriteLine($"   ✓ Retrieved {logsResult.Value.Lines.Count} log entries");
        Console.WriteLine("   Last 3 entries:");
        foreach (var line in logsResult.Value.Lines.TakeLast(3))
        {
            Console.WriteLine($"     {line}");
        }
    }
    else
    {
        Console.WriteLine($"   ✗ Logs failed ({logsResult.StatusCode}): {logsResult.ErrorMessage}");
    }

    Console.WriteLine();
    Console.WriteLine("All tests completed!");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
