using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AegisMint.Client;
using AegisMint.Core.Models;
using AegisMint.Core.Security;

namespace AegisMint.TestClient;

public class TestExportImportWorkflow
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Testing Export/Import Shares Workflow ===\n");

        var client = MintClient.CreateDefault();

        try
        {
            // Step 1: Unlock device
            Console.WriteLine("1. Unlocking device...");
            var unlockResult = await client.UnlockForDevelopmentAsync();
            if (unlockResult.Success)
            {
                Console.WriteLine("   ✓ Device unlocked");
            }
            else
            {
                Console.WriteLine($"   ✗ Unlock failed: {unlockResult.ErrorMessage}");
                return;
            }

            // Step 2: Get mnemonic
            Console.WriteLine("\n2. Getting mnemonic...");
            var mnemonicResult = await client.GetMnemonicAsync();
            if (!mnemonicResult.Success || mnemonicResult.Value is null)
            {
                Console.WriteLine($"   ✗ Failed: {mnemonicResult.ErrorMessage}");
                return;
            }

            var originalMnemonic = mnemonicResult.Value.Mnemonic ?? string.Empty;
            Console.WriteLine($"   ✓ Original mnemonic: {originalMnemonic}");

            // Step 3: Create 8 shares with threshold 3
            Console.WriteLine("\n3. Creating 8 Shamir shares (threshold 3)...");
            var secretBytes = System.Text.Encoding.UTF8.GetBytes(originalMnemonic);
            var shamir = new ShamirSecretSharingService();
            var shares = shamir.Split(secretBytes, threshold: 3, shareCount: 8);
            Console.WriteLine($"   ✓ Generated {shares.Count} shares");

            // Step 4: Export to 8 separate JSON files
            Console.WriteLine("\n4. Exporting 8 separate share files...");
            var testFolder = Path.Combine(Path.GetTempPath(), $"test-shares-{DateTime.Now:yyyyMMddHHmmss}");
            Directory.CreateDirectory(testFolder);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var savedFiles = new List<string>();

            foreach (var share in shares)
            {
                var shareData = new
                {
                    Version = 1,
                    Timestamp = DateTimeOffset.UtcNow,
                    Threshold = 3,
                    TotalShares = 8,
                    ShareId = share.Id,
                    ShareValue = share.Value
                };

                var fileName = $"aegis-share-{share.Id}-{timestamp}.json";
                var filePath = Path.Combine(testFolder, fileName);
                
                var json = JsonSerializer.Serialize(shareData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
                savedFiles.Add(fileName);
            }

            Console.WriteLine($"   ✓ Exported to: {testFolder}");
            Console.WriteLine($"   ✓ Created 8 files: {string.Join(", ", savedFiles.Take(3))}...");

            // Step 5: Import and reconstruct using 3 shares
            Console.WriteLine("\n5. Importing and reconstructing from 3 shares...");
            var importedShares = new List<ShamirShare>();
            var filesToLoad = savedFiles.Take(3).ToList();

            foreach (var fileName in filesToLoad)
            {
                var filePath = Path.Combine(testFolder, fileName);
                var importedJson = await File.ReadAllTextAsync(filePath);
                var shareData = JsonSerializer.Deserialize<JsonElement>(importedJson);
                
                var id = shareData.GetProperty("ShareId").GetByte();
                var value = shareData.GetProperty("ShareValue").GetString() ?? string.Empty;
                importedShares.Add(new ShamirShare(id, value));
            }

            Console.WriteLine($"   ✓ Loaded {importedShares.Count} shares from files");
            Console.WriteLine($"   Using shares: {string.Join(", ", importedShares.Select(s => $"#{s.Id}"))}");

            var reconstructedBytes = shamir.Combine(importedShares, threshold: 3);
            var reconstructedMnemonic = System.Text.Encoding.UTF8.GetString(reconstructedBytes);

            Console.WriteLine($"   ✓ Reconstructed: {reconstructedMnemonic}");

            // Step 6: Verify reconstruction
            Console.WriteLine("\n6. Verifying reconstruction...");
            if (reconstructedMnemonic == originalMnemonic)
            {
                Console.WriteLine("   ✓✓✓ SUCCESS: Reconstructed mnemonic matches original!");
            }
            else
            {
                Console.WriteLine("   ✗✗✗ FAILED: Reconstructed mnemonic does NOT match!");
                Console.WriteLine($"   Original:      {originalMnemonic}");
                Console.WriteLine($"   Reconstructed: {reconstructedMnemonic}");
            }

            // Step 7: Test with different combinations of 3 shares from all 8 files
            Console.WriteLine("\n7. Testing different share combinations...");
            var allSharesFromFiles = new List<ShamirShare>();
            foreach (var fileName in savedFiles)
            {
                var filePath = Path.Combine(testFolder, fileName);
                var json = await File.ReadAllTextAsync(filePath);
                var shareData = JsonSerializer.Deserialize<JsonElement>(json);
                var id = shareData.GetProperty("ShareId").GetByte();
                var value = shareData.GetProperty("ShareValue").GetString() ?? string.Empty;
                allSharesFromFiles.Add(new ShamirShare(id, value));
            }

            var testCombinations = new[]
            {
                new[] { 0, 1, 2 },
                new[] { 0, 3, 6 },
                new[] { 2, 5, 7 }
            };

            foreach (var combo in testCombinations)
            {
                var comboShares = combo.Select(i => allSharesFromFiles[i]).ToList();
                var comboBytes = shamir.Combine(comboShares, threshold: 3);
                var comboMnemonic = System.Text.Encoding.UTF8.GetString(comboBytes);
                var match = comboMnemonic == originalMnemonic ? "✓" : "✗";
                Console.WriteLine($"   {match} Shares {string.Join(",", combo)}: {(comboMnemonic == originalMnemonic ? "MATCH" : "MISMATCH")}");
            }

            // Cleanup
            Directory.Delete(testFolder, true);
            Console.WriteLine($"\n✓ Cleaned up test folder");

            Console.WriteLine("\n=== Export/Import Test Complete ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
        finally
        {
            client.Dispose();
        }
    }
}
