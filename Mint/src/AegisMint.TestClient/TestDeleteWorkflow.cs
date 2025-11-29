using System;
using System.Threading.Tasks;
using AegisMint.Client;

namespace AegisMint.TestClient;

public class TestDeleteWorkflow
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Testing Delete Mnemonic Workflow ===\n");

var client = MintClient.CreateDefault();

try
{
    // Step 1: Check current status
    Console.WriteLine("1. Checking current status...");
    var hasResult = await client.HasMnemonicAsync();
    if (hasResult.Success && hasResult.Value is not null)
    {
        Console.WriteLine($"   Has mnemonic: {hasResult.Value.HasMnemonic}");
    }

    // Step 2: Unlock for dev
    Console.WriteLine("\n2. Unlocking for development...");
    var unlockResult = await client.UnlockForDevelopmentAsync();
    if (unlockResult.Success)
    {
        Console.WriteLine("   ✓ Unlocked successfully");
    }
    else
    {
        Console.WriteLine($"   ✗ Unlock failed: {unlockResult.ErrorMessage}");
    }

    // Step 3: Get current mnemonic if exists
    Console.WriteLine("\n3. Getting current mnemonic...");
    var getResult = await client.GetMnemonicAsync();
    if (getResult.Success && getResult.Value is not null)
    {
        Console.WriteLine($"   ✓ Current mnemonic: {getResult.Value.Mnemonic}");
    }
    else
    {
        Console.WriteLine($"   No mnemonic or failed: {getResult.ErrorMessage}");
    }

    // Step 4: Delete mnemonic
    Console.WriteLine("\n4. Deleting mnemonic...");
    var deleteResult = await client.DeleteMnemonicAsync();
    if (deleteResult.Success && deleteResult.Value is not null)
    {
        Console.WriteLine($"   ✓ {deleteResult.Value.Message}");
    }
    else
    {
        Console.WriteLine($"   ✗ Delete failed ({deleteResult.StatusCode}): {deleteResult.ErrorMessage}");
    }

    // Step 5: Verify deletion
    Console.WriteLine("\n5. Verifying deletion...");
    hasResult = await client.HasMnemonicAsync();
    if (hasResult.Success && hasResult.Value is not null)
    {
        Console.WriteLine($"   Has mnemonic: {hasResult.Value.HasMnemonic} (should be False)");
    }

    // Step 6: Set new mnemonic
    Console.WriteLine("\n6. Setting new mnemonic...");
    var newMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    var setResult = await client.SetMnemonicAsync(newMnemonic);
    if (setResult.Success && setResult.Value is not null)
    {
        Console.WriteLine($"   ✓ {setResult.Value.Message}");
        Console.WriteLine($"   ✓ Generated {setResult.Value.Shares?.Count ?? 0} shares");
    }
    else
    {
        Console.WriteLine($"   ✗ Set failed: {setResult.ErrorMessage}");
    }

    // Step 7: Get new mnemonic
    Console.WriteLine("\n7. Getting new mnemonic...");
    getResult = await client.GetMnemonicAsync();
    if (getResult.Success && getResult.Value is not null)
    {
        Console.WriteLine($"   ✓ New mnemonic: {getResult.Value.Mnemonic}");
    }
    else
    {
        Console.WriteLine($"   ✗ Failed: {getResult.ErrorMessage}");
    }

        Console.WriteLine("\n=== Test Complete ===");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError: {ex.Message}");
    }
    finally
    {
        client.Dispose();
    }
    }
}
