using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using NBitcoin;
using Nethereum.Util;

namespace AegisMint.Mint.Services;

/// <summary>
/// Manages vault operations including mnemonic generation and secure storage in Windows Registry.
/// </summary>
public class VaultManager
{
    private const string RegistryPath = @"Software\AegisMint\Vault";
    private const string TreasuryMnemonicKey = "TreasuryMnemonic";

    /// <summary>
    /// Generates a new Treasury vault with a 12-word mnemonic and returns the Ethereum address.
    /// The mnemonic is encrypted and stored in the Windows Registry.
    /// </summary>
    /// <returns>The Ethereum address derived from the mnemonic.</returns>
    public string GenerateTreasury()
    {
        // Generate 12-word mnemonic (128-bit entropy)
        var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
        
        // Derive Ethereum address from mnemonic
        var ethereumAddress = DeriveEthereumAddress(mnemonic);
        
        // Store encrypted mnemonic in registry
        StoreEncryptedMnemonic(TreasuryMnemonicKey, mnemonic.ToString());
        
        return ethereumAddress;
    }

    /// <summary>
    /// Retrieves the Treasury Ethereum address if it exists.
    /// </summary>
    /// <returns>The Treasury address or null if not generated.</returns>
    public string? GetTreasuryAddress()
    {
        var mnemonic = RetrieveDecryptedMnemonic(TreasuryMnemonicKey);
        if (mnemonic == null) return null;
        
        try
        {
            var mn = new Mnemonic(mnemonic, Wordlist.English);
            return DeriveEthereumAddress(mn);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves the private key (hex) for the Treasury account if it exists.
    /// </summary>
    public string? GetTreasuryPrivateKey()
    {
        var mnemonic = RetrieveDecryptedMnemonic(TreasuryMnemonicKey);
        if (mnemonic == null) return null;
        
        try
        {
            var mn = new Mnemonic(mnemonic, Wordlist.English);
            return DerivePrivateKeyHex(mn);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if the Treasury vault has been generated.
    /// </summary>
    public bool HasTreasury()
    {
        return GetTreasuryAddress() != null;
    }

    /// <summary>
    /// Clears all vault data from the registry.
    /// </summary>
    public void ClearVaults()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true);
            if (key != null)
            {
                key.DeleteValue(TreasuryMnemonicKey, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    /// <summary>
    /// Derives an Ethereum address from a BIP39 mnemonic using BIP44 path m/44'/60'/0'/0/0.
    /// </summary>
    private string DeriveEthereumAddress(Mnemonic mnemonic)
    {
        var key = DeriveExtKey(mnemonic);
        
        // Derive public key (uncompressed, 65 bytes)
        var pubKey = key.PrivateKey.PubKey.ToBytes();
        
        // For Ethereum, we need the uncompressed public key without the 0x04 prefix
        // Then compute Keccak256 hash and take last 20 bytes
        byte[] publicKeyWithoutPrefix;
        if (pubKey.Length == 65 && pubKey[0] == 0x04)
        {
            publicKeyWithoutPrefix = new byte[64];
            Array.Copy(pubKey, 1, publicKeyWithoutPrefix, 0, 64);
        }
        else if (pubKey.Length == 64)
        {
            publicKeyWithoutPrefix = pubKey;
        }
        else
        {
            // Compressed public key - decompress it
            var ecKey = key.PrivateKey.PubKey;
            var uncompressed = ecKey.Decompress().ToBytes();
            publicKeyWithoutPrefix = new byte[64];
            Array.Copy(uncompressed, 1, publicKeyWithoutPrefix, 0, 64);
        }
        
        // Compute Keccak256 hash
        var hash = ComputeKeccak256(publicKeyWithoutPrefix);
        
        // Take last 20 bytes and format as Ethereum address
        var addressBytes = new byte[20];
        Array.Copy(hash, hash.Length - 20, addressBytes, 0, 20);
        
        return "0x" + BitConverter.ToString(addressBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Derives the private key in 0x-prefixed hex form for the Treasury path.
    /// </summary>
    private string DerivePrivateKeyHex(Mnemonic mnemonic)
    {
        var key = DeriveExtKey(mnemonic);
        var privateKeyBytes = key.PrivateKey.ToBytes();
        return "0x" + BitConverter.ToString(privateKeyBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Returns the extended key for the Treasury derivation path.
    /// </summary>
    private ExtKey DeriveExtKey(Mnemonic mnemonic)
    {
        // Derive seed from mnemonic
        var seed = mnemonic.DeriveExtKey();
        
        // Use Ethereum's BIP44 path: m/44'/60'/0'/0/0
        // 44' = purpose (BIP44)
        // 60' = Ethereum coin type
        // 0' = account
        // 0 = external chain
        // 0 = address index
        var path = new KeyPath("m/44'/60'/0'/0/0");
        return seed.Derive(path);
    }

    /// <summary>
    /// Computes Keccak256 hash (used by Ethereum).
    /// </summary>
    private byte[] ComputeKeccak256(byte[] input)
    {
        var sha3 = new Sha3Keccack();
        return sha3.CalculateHash(input);
    }

    /// <summary>
    /// Stores an encrypted mnemonic in the Windows Registry using DPAPI.
    /// </summary>
    private void StoreEncryptedMnemonic(string keyName, string mnemonic)
    {
        try
        {
            // Encrypt the mnemonic using DPAPI (Data Protection API)
            var mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
            var encryptedBytes = ProtectedData.Protect(
                mnemonicBytes,
                null, // entropy
                DataProtectionScope.CurrentUser
            );
            
            // Convert to Base64 for storage
            var encryptedBase64 = Convert.ToBase64String(encryptedBytes);
            
            // Store in registry
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key.SetValue(keyName, encryptedBase64, RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to store encrypted mnemonic: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves and decrypts a mnemonic from the Windows Registry using DPAPI.
    /// </summary>
    private string? RetrieveDecryptedMnemonic(string keyName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key == null) return null;
            
            var encryptedBase64 = key.GetValue(keyName) as string;
            if (string.IsNullOrEmpty(encryptedBase64)) return null;
            
            // Decrypt using DPAPI
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null, // entropy
                DataProtectionScope.CurrentUser
            );
            
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch
        {
            return null;
        }
    }
}
