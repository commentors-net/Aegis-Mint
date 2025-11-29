using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AegisMint.Core.Abstractions;
using AegisMint.Core.Configuration;
using AegisMint.Core.Models;
using AegisMint.Core.Security;
using Microsoft.Extensions.Options;
using NBitcoin;

namespace AegisMint.Core.Vault;

public class GenesisVault : IGenesisVault, IDisposable
{
    private const string MnemonicFileName = "genesis.enc";
    private const string MetadataFileName = "device.json";
    private const string SharesFileName = "shares.json";
    private readonly MintOptions _options;
    private readonly IProtectedKeyStore _keyStore;
    private readonly string _dataDirectory;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ShamirSecretSharingService _shamir = new();

    public GenesisVault(IOptions<MintOptions> options, IProtectedKeyStore keyStore)
    {
        _options = options.Value;
        _keyStore = keyStore;
        _dataDirectory = ResolveDataDirectory(_options.DataDirectory);
        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task<string> GetOrCreateMnemonicAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await EnsureMnemonicAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<string?> TryGetMnemonicAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await TryReadMnemonicInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var metadata = await EnsureMetadataAsync(cancellationToken).ConfigureAwait(false);
            return new DeviceInfo(
                metadata.DeviceId,
                metadata.ShareCount,
                metadata.RecoveryThreshold,
                metadata.GovernanceQuorum,
                metadata.UnlockWindowMinutes,
                metadata.ConfigurationVersion);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<bool> HasMnemonicAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var mnemonicPath = Path.Combine(_dataDirectory, MnemonicFileName);
            return File.Exists(mnemonicPath);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SetMnemonicAsync(string mnemonic, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (string.IsNullOrWhiteSpace(mnemonic))
            {
                throw new ArgumentException("Mnemonic cannot be empty", nameof(mnemonic));
            }

            // Validate mnemonic format (should be 12 words)
            var words = mnemonic.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 12)
            {
                throw new ArgumentException("Mnemonic must be exactly 12 words", nameof(mnemonic));
            }

            // Validate using NBitcoin
            try
            {
                var _ = new Mnemonic(mnemonic, Wordlist.English);
            }
            catch
            {
                throw new ArgumentException("Invalid mnemonic phrase. Words must be from BIP39 wordlist.", nameof(mnemonic));
            }

            // Check if mnemonic already exists
            var mnemonicPath = Path.Combine(_dataDirectory, MnemonicFileName);
            if (File.Exists(mnemonicPath))
            {
                throw new InvalidOperationException("Genesis key already exists. Cannot overwrite existing mnemonic.");
            }

            await EnsureMetadataAsync(cancellationToken).ConfigureAwait(false);
            await PersistMnemonicAsync(mnemonic, cancellationToken).ConfigureAwait(false);

            // Generate and save shares immediately
            var sharesPath = Path.Combine(_dataDirectory, SharesFileName);
            var secretBytes = Encoding.UTF8.GetBytes(mnemonic);
            ValidateThresholds(_options.ShareCount, _options.RecoveryThreshold);
            var shares = _shamir.Split(secretBytes, _options.RecoveryThreshold, _options.ShareCount);
            var json = JsonSerializer.Serialize(shares, _serializerOptions);
            await File.WriteAllTextAsync(sharesPath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyCollection<ShamirShare>> GetOrCreateSharesAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sharesPath = Path.Combine(_dataDirectory, SharesFileName);
            if (File.Exists(sharesPath))
            {
                var existingJson = await File.ReadAllTextAsync(sharesPath, cancellationToken).ConfigureAwait(false);
                var existingShares = JsonSerializer.Deserialize<List<ShamirShare>>(existingJson, _serializerOptions);
                if (existingShares is not null && existingShares.Count > 0)
                {
                    return existingShares;
                }
            }

            var mnemonic = await EnsureMnemonicAsync(cancellationToken).ConfigureAwait(false);
            var secretBytes = Encoding.UTF8.GetBytes(mnemonic);
            ValidateThresholds(_options.ShareCount, _options.RecoveryThreshold);
            var shares = _shamir.Split(secretBytes, _options.RecoveryThreshold, _options.ShareCount);
            var json = JsonSerializer.Serialize(shares, _serializerOptions);
            await File.WriteAllTextAsync(sharesPath, json, cancellationToken).ConfigureAwait(false);
            return shares;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<string> EnsureMnemonicAsync(CancellationToken cancellationToken)
    {
        var existing = await TryReadMnemonicInternalAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        await EnsureMetadataAsync(cancellationToken).ConfigureAwait(false);
        var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString();
        await PersistMnemonicAsync(mnemonic, cancellationToken).ConfigureAwait(false);
        return mnemonic;
    }

    private async Task<string?> TryReadMnemonicInternalAsync(CancellationToken cancellationToken)
    {
        var mnemonicPath = Path.Combine(_dataDirectory, MnemonicFileName);
        if (!File.Exists(mnemonicPath))
        {
            return null;
        }

        var encryptedJson = await File.ReadAllTextAsync(mnemonicPath, cancellationToken).ConfigureAwait(false);
        var envelope = JsonSerializer.Deserialize<AesGcmEnvelope>(encryptedJson, _serializerOptions);
        if (envelope is null)
        {
            return null;
        }

        var key = await _keyStore.GetOrCreateKeyAsync(32, cancellationToken).ConfigureAwait(false);
        var plaintext = envelope.Decrypt(key);
        return Encoding.UTF8.GetString(plaintext);
    }

    private async Task PersistMnemonicAsync(string mnemonic, CancellationToken cancellationToken)
    {
        // Never log the mnemonic.
        var key = await _keyStore.GetOrCreateKeyAsync(32, cancellationToken).ConfigureAwait(false);
        var encrypted = AesGcmEnvelope.Encrypt(Encoding.UTF8.GetBytes(mnemonic), key);
        var json = JsonSerializer.Serialize(encrypted, _serializerOptions);
        var mnemonicPath = Path.Combine(_dataDirectory, MnemonicFileName);
        await File.WriteAllTextAsync(mnemonicPath, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MintMetadata> EnsureMetadataAsync(CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(_dataDirectory, MetadataFileName);
        if (File.Exists(metadataPath))
        {
            var content = await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false);
            var existing = JsonSerializer.Deserialize<MintMetadata>(content, _serializerOptions);
            if (existing is not null)
            {
                return existing;
            }
        }

        ValidateThresholds(_options.ShareCount, _options.RecoveryThreshold);
        var metadata = new MintMetadata
        {
            DeviceId = string.IsNullOrWhiteSpace(_options.DeviceId) ? Guid.NewGuid().ToString("N") : _options.DeviceId!,
            ShareCount = _options.ShareCount,
            RecoveryThreshold = _options.RecoveryThreshold,
            GovernanceQuorum = Math.Max(1, _options.GovernanceQuorum),
            UnlockWindowMinutes = Math.Max(1, _options.UnlockWindowMinutes),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(metadata, _serializerOptions);
        await File.WriteAllTextAsync(metadataPath, json, cancellationToken).ConfigureAwait(false);
        return metadata;
    }

    private static string ResolveDataDirectory(string configured)
    {
        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(AppContext.BaseDirectory, configured);
    }

    private static void ValidateThresholds(int shareCount, int threshold)
    {
        if (shareCount <= 0)
        {
            throw new InvalidOperationException("ShareCount must be greater than zero.");
        }

        if (threshold <= 0 || threshold > shareCount)
        {
            throw new InvalidOperationException("RecoveryThreshold must be between 1 and ShareCount.");
        }
    }

    public void Dispose()
    {
        _mutex.Dispose();
        if (_keyStore is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
