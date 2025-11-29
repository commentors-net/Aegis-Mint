using System;
using System.Threading.Tasks;
using AegisMint.Client;

namespace AegisMint.TestClient;

public class TestMnemonicWorkflow
{
    public static async Task RunAsync()
    {
        Console.WriteLine("\n=== Testing Genesis Key Workflow ===\n");
        
        var client = new MintClient(new MintClientOptions());
        
        // Step 1: Check if mnemonic exists
        Console.WriteLine("1. Checking if genesis key exists...");
        var hasResult = await client.HasMnemonicAsync();
        if (!hasResult.Success)
        {
            Console.WriteLine($"   ✗ Error: {hasResult.ErrorMessage}");
            return;
        }
        Console.WriteLine($"   ✓ Has mnemonic: {hasResult.Value?.HasMnemonic ?? false}");
        
        // Step 2: Try to set a new mnemonic (valid 12-word phrase)
        Console.WriteLine("\n2. Testing SetMnemonic with valid 12-word phrase...");
        var testMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        
        var setResult = await client.SetMnemonicAsync(testMnemonic);
        if (!setResult.Success)
        {
            Console.WriteLine($"   ✗ Error: {setResult.ErrorMessage}");
            if (setResult.StatusCode == 409)
            {
                Console.WriteLine("   (This is expected if mnemonic was already set)");
            }
        }
        else
        {
            Console.WriteLine($"   ✓ Mnemonic set successfully!");
            Console.WriteLine($"   ✓ Shamir shares generated:");
            if (setResult.Value != null && setResult.Value.Shares != null)
            {
                int i = 1;
                foreach (var share in setResult.Value.Shares)
                {
                    Console.WriteLine($"      Share {i} (ID: {share.Id}):");
                    Console.WriteLine($"      {share.Value}");
                    i++;
                }
            }
        }
        
        // Step 3: Verify mnemonic exists now
        Console.WriteLine("\n3. Verifying genesis key exists after setting...");
        hasResult = await client.HasMnemonicAsync();
        if (!hasResult.Success)
        {
            Console.WriteLine($"   ✗ Error: {hasResult.ErrorMessage}");
            return;
        }
        Console.WriteLine($"   ✓ Has mnemonic: {hasResult.Value?.HasMnemonic ?? false}");
        
        // Step 4: Try to set again (should fail with 409)
        Console.WriteLine("\n4. Testing duplicate set (should fail with 409)...");
        var duplicateResult = await client.SetMnemonicAsync(testMnemonic);
        if (!duplicateResult.Success && duplicateResult.StatusCode == 409)
        {
            Console.WriteLine($"   ✓ Correctly prevented: {duplicateResult.ErrorMessage}");
        }
        else
        {
            Console.WriteLine($"   ✗ Expected 409 Conflict but got: {duplicateResult.StatusCode}");
        }
        
        Console.WriteLine("\n=== Workflow Test Complete ===\n");
    }
}
