using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NBitcoin;
using Nethereum.Util;
using Microsoft.Win32;
using AegisMint.Core.Models;

namespace AegisMint.Core.Services;

/// <summary>
/// Manages vault operations including mnemonic generation and secure storage in encrypted SQLite.
/// </summary>
public class VaultManager
{
    private const string LegacyRegistryPath = @"Software\AegisMint\Vault";
    private readonly VaultDataStore _store = new();
    private const string TreasuryMnemonicKey = "TreasuryMnemonic";
    private const string LastNetworkKey = "LastNetwork";
    private const string ExternalTreasuryAddressKey = "ExternalTreasuryAddress";
    private const string BootstrapThresholdKey = "BootstrapThreshold";
    private const string DesktopAppIdKey = "DesktopAppId";
    private const string DesktopSecretKeyKey = "DesktopSecretKey";
    private const string ApiBaseUrlKey = "ApiBaseUrl";
    private bool _legacyMigrationAttempted;

    public VaultManager()
    {
        TryMigrateFromRegistry();
    }

    /// <summary>
    /// Generates a new Treasury vault with a 12-word mnemonic and returns the Ethereum address.
    /// The mnemonic is encrypted and stored in SQLite (SQLCipher).
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
    /// Imports an existing mnemonic and stores it securely, returning the derived treasury address.
    /// </summary>
    public string ImportTreasuryMnemonic(string mnemonicPhrase)
    {
        if (string.IsNullOrWhiteSpace(mnemonicPhrase))
        {
            throw new ArgumentException("Mnemonic cannot be empty", nameof(mnemonicPhrase));
        }

        // Validate/normalize mnemonic
        var mnemonic = new Mnemonic(mnemonicPhrase.Trim(), Wordlist.English);
        var ethereumAddress = DeriveEthereumAddress(mnemonic);

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
    /// Clears all vault data from the encrypted database.
    /// </summary>
    public void ClearVaults()
    {
        try
        {
            _store.ClearAll();
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    /// <summary>
    /// Records a deployed contract address so the app cannot deploy again.
    /// </summary>
    public void RecordContractDeployment(string contractAddress, string network)
    {
        if (string.IsNullOrWhiteSpace(contractAddress))
        {
            throw new ArgumentException("Contract address cannot be empty", nameof(contractAddress));
        }

        try
        {
            _store.SaveContractDeployment(NormalizeNetwork(network), contractAddress);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to persist deployed contract address: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Returns the deployed contract address if one is recorded.
    /// </summary>
    public string? GetDeployedContractAddress(string network)
    {
        try
        {
            return _store.GetContractDeployment(NormalizeNetwork(network));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Indicates whether a contract has already been deployed.
    /// </summary>
    public bool HasDeployedContract(string network)
    {
        return !string.IsNullOrWhiteSpace(GetDeployedContractAddress(network));
    }

    public void RecordDeploymentSnapshot(string network, DeploymentSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));

        try
        {
            _store.SaveDeploymentSnapshot(snapshot with { Network = NormalizeNetwork(network) });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to persist deployment snapshot: {ex.Message}", ex);
        }
    }

    public DeploymentSnapshot? GetDeploymentSnapshot(string network)
    {
        try
        {
            return _store.GetDeploymentSnapshot(NormalizeNetwork(network));
        }
        catch
        {
            return null;
        }
    }

    public void SaveLastNetwork(string network)
    {
        try
        {
            _store.SaveSetting(LastNetworkKey, network);
        }
        catch
        {
            // ignore persistence issues for last network
        }
    }

    public string GetLastNetwork()
    {
        try
        {
            var value = _store.GetSetting(LastNetworkKey);
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }
        catch
        {
            return string.Empty;
        }
    }

    public IReadOnlyList<FreezeOperation> GetFreezeOperations(string network, int limit = 100)
    {
        return _store.GetFreezeOperations(NormalizeNetwork(network), limit);
    }

    /// <summary>
    /// Returns a known treasury address. Falls back to a user-provided address if the mnemonic is missing.
    /// </summary>
    public string? GetKnownTreasuryAddress()
    {
        return GetTreasuryAddress() ?? _store.GetSetting(ExternalTreasuryAddressKey);
    }

    /// <summary>
    /// Saves an externally provided treasury address for display/use when no mnemonic is present.
    /// </summary>
    public void SaveExternalTreasuryAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        _store.SaveSetting(ExternalTreasuryAddressKey, address.Trim());
    }

    public void SaveBootstrapThreshold(int threshold)
    {
        if (threshold <= 0)
        {
            return;
        }

        _store.SaveSetting(BootstrapThresholdKey, threshold.ToString());
    }

    public int? GetBootstrapThreshold()
    {
        var value = _store.GetSetting(BootstrapThresholdKey);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    // Desktop Registration Methods
    public string GetDesktopAppId()
    {
        var existing = _store.GetSetting(DesktopAppIdKey);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        // Generate new GUID
        var newId = Guid.NewGuid().ToString();
        _store.SaveSetting(DesktopAppIdKey, newId);
        return newId;
    }

    public string GetDesktopSecretKey(string appType = "TokenControl")
    {
        var keyName = $"DesktopSecretKey_{appType}";
        var existing = _store.GetSetting(keyName);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        // Generate new secret key
        var newSecret = AegisMint.Core.Security.HmacSignature.GenerateSecretKey();
        _store.SaveSetting(keyName, newSecret);
        return newSecret;
    }

    public void SaveDesktopSecretKey(string secretKey, string appType = "TokenControl")
    {
        var keyName = $"DesktopSecretKey_{appType}";
        _store.SaveSetting(keyName, secretKey);
    }

    public void SaveApiBaseUrl(string url)
    {
        _store.SaveSetting(ApiBaseUrlKey, url);
    }

    public string GetApiBaseUrl()
    {
        var value = _store.GetSetting(ApiBaseUrlKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        // Try to read from appsettings.json
        try
        {
            var appSettingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (System.IO.File.Exists(appSettingsPath))
            {
                var json = System.IO.File.ReadAllText(appSettingsPath);
                var settings = System.Text.Json.JsonDocument.Parse(json);
                if (settings.RootElement.TryGetProperty("ApiSettings", out var apiSettings))
                {
                    if (apiSettings.TryGetProperty("BaseUrl", out var baseUrl))
                    {
                        var url = baseUrl.GetString();
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            // Save to vault for future use
                            _store.SaveSetting(ApiBaseUrlKey, url);
                            return url;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore errors reading appsettings.json
        }

        // Default to localhost for development
        var defaultUrl = "http://127.0.0.1:8000";
        _store.SaveSetting(ApiBaseUrlKey, defaultUrl);
        return defaultUrl;
    }

    // Certificate Storage Methods
    public void SaveCertificate(string certificatePem)
    {
        _store.SaveSetting("desktop_certificate", certificatePem);
    }

    public void SavePrivateKey(string privateKeyPem)
    {
        _store.SaveSetting("desktop_private_key", privateKeyPem);
    }

    public string? GetCertificate()
    {
        return _store.GetSetting("desktop_certificate");
    }

    public string? GetPrivateKey()
    {
        return _store.GetSetting("desktop_private_key");
    }

    private static string NormalizeNetwork(string network)
    {
        var normalized = string.IsNullOrWhiteSpace(network)
            ? "default"
            : network.Trim().ToLowerInvariant().Replace(" ", "_");
        return normalized;
    }

    private void TryMigrateFromRegistry()
    {
        if (_legacyMigrationAttempted)
        {
            return;
        }

        _legacyMigrationAttempted = true;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(LegacyRegistryPath, writable: false);
            if (key == null)
            {
                return;
            }

            // Treasury mnemonic (already DPAPI encrypted)
            var legacyMnemonic = key.GetValue(TreasuryMnemonicKey) as string;
            if (!string.IsNullOrWhiteSpace(legacyMnemonic))
            {
                _store.SaveEncryptedMnemonic(TreasuryMnemonicKey, legacyMnemonic);
            }

            // Last network
            var legacyNetwork = key.GetValue(LastNetworkKey) as string;
            if (!string.IsNullOrWhiteSpace(legacyNetwork))
            {
                _store.SaveSetting(LastNetworkKey, legacyNetwork);
            }

            // Contracts and snapshots
            foreach (var valueName in key.GetValueNames())
            {
                if (valueName.StartsWith("ContractAddress_", StringComparison.OrdinalIgnoreCase))
                {
                    var networkSuffix = valueName.Substring("ContractAddress_".Length);
                    var address = key.GetValue(valueName) as string;
                    if (!string.IsNullOrWhiteSpace(address))
                    {
                        _store.SaveContractDeployment(NormalizeNetwork(networkSuffix), address);
                    }
                }
                else if (valueName.StartsWith("DeploymentSnapshot_", StringComparison.OrdinalIgnoreCase))
                {
                    var networkSuffix = valueName.Substring("DeploymentSnapshot_".Length);
                    var json = key.GetValue(valueName) as string;
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        continue;
                    }

                    try
                    {
                        var snapshot = JsonSerializer.Deserialize<DeploymentSnapshot>(json);
                        if (snapshot != null)
                        {
                            var network = string.IsNullOrWhiteSpace(snapshot.Network) ? networkSuffix : snapshot.Network;
                            _store.SaveDeploymentSnapshot(snapshot with { Network = NormalizeNetwork(network) });
                        }
                    }
                    catch
                    {
                        // ignore malformed legacy snapshot
                    }
                }
            }
        }
        catch
        {
            // best-effort migration only
        }
    }

    /// <summary>
    /// Derives an Ethereum address from a BIP39 mnemonic using BIP44 path m/44'/60'/0'/0/0.
    /// </summary>
    private string DeriveEthereumAddress(Mnemonic mnemonic, int index = 0)
    {
        var key = DeriveExtKey(mnemonic, index);
        
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
    private string DerivePrivateKeyHex(Mnemonic mnemonic, int index = 0)
    {
        var key = DeriveExtKey(mnemonic, index);
        var privateKeyBytes = key.PrivateKey.ToBytes();
        return "0x" + BitConverter.ToString(privateKeyBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Returns the extended key for the Treasury derivation path.
    /// </summary>
    private ExtKey DeriveExtKey(Mnemonic mnemonic, int index = 0)
    {
        // Derive seed from mnemonic
        var seed = mnemonic.DeriveExtKey();
        
        // Use Ethereum's BIP44 path: m/44'/60'/0'/0/0
        // 44' = purpose (BIP44)
        // 60' = Ethereum coin type
        // 0' = account
        // 0 = external chain
        // 0 = address index
        var path = new KeyPath($"m/44'/60'/0'/0/{index}");
        return seed.Derive(path);
    }

    /// <summary>
    /// Gets the second derived address (index 1) from the stored mnemonic for proxy admin usage.
    /// </summary>
    public string? GetSecondaryAddress()
    {
        var mnemonic = RetrieveDecryptedMnemonic(TreasuryMnemonicKey);
        if (mnemonic == null) return null;

        try
        {
            var mn = new Mnemonic(mnemonic, Wordlist.English);
            return DeriveEthereumAddress(mn, 1);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the second derived private key (index 1) from the stored mnemonic.
    /// </summary>
    public string? GetSecondaryPrivateKey()
    {
        var mnemonic = RetrieveDecryptedMnemonic(TreasuryMnemonicKey);
        if (mnemonic == null) return null;

        try
        {
            var mn = new Mnemonic(mnemonic, Wordlist.English);
            return DerivePrivateKeyHex(mn, 1);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the stored treasury mnemonic for recovery workflows. Never log this value.
    /// </summary>
    public string? GetTreasuryMnemonic()
    {
        return RetrieveDecryptedMnemonic(TreasuryMnemonicKey);
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
    /// Encrypts arbitrary data using DPAPI (Data Protection API) for the current user.
    /// Returns the encrypted data as a Base64-encoded string.
    /// </summary>
    public string EncryptData(string data)
    {
        try
        {
            // Encrypt the data using DPAPI (Data Protection API)
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var encryptedBytes = ProtectedData.Protect(
                dataBytes,
                null, // entropy
                DataProtectionScope.CurrentUser
            );
            
            // Convert to Base64 for storage/transmission
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to encrypt data: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Stores an encrypted mnemonic in the SQLCipher database using DPAPI-protected payload.
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
            
            _store.SaveEncryptedMnemonic(keyName, encryptedBase64);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to store encrypted mnemonic: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves and decrypts a mnemonic from the SQLCipher database using DPAPI.
    /// </summary>
    private string? RetrieveDecryptedMnemonic(string keyName)
    {
        try
        {
            var encryptedBase64 = _store.GetEncryptedMnemonic(keyName);
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

public record DeploymentSnapshot(
    string Network,
    string ContractAddress,
    string TokenName,
    string TokenSupply,
    int TokenDecimals,
    int GovShares,
    int GovThreshold,
    string TreasuryAddress,
    string TreasuryEth,
    string TreasuryTokens,
    DateTimeOffset CreatedAtUtc);
