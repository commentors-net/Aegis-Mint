using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AegisMint.Core.Abstractions;

namespace AegisMint.Core.Storage;

/// <summary>
/// Stores a master key on disk protected by DPAPI (LocalMachine scope).
/// </summary>
[SupportedOSPlatform("windows")]
public class FileProtectedKeyStore : IProtectedKeyStore, IDisposable
{
    private readonly string _keyPath;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileProtectedKeyStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _keyPath = Path.Combine(dataDirectory, "master.key");
    }

    public async Task<byte[]> GetOrCreateKeyAsync(int sizeBytes, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(_keyPath))
            {
                var existingBytes = await File.ReadAllBytesAsync(_keyPath, cancellationToken).ConfigureAwait(false);
                return ProtectedData.Unprotect(existingBytes, null, DataProtectionScope.LocalMachine);
            }

            var key = RandomNumberGenerator.GetBytes(sizeBytes);
            var protectedKey = ProtectedData.Protect(key, null, DataProtectionScope.LocalMachine);
            await File.WriteAllBytesAsync(_keyPath, protectedKey, cancellationToken).ConfigureAwait(false);
            return key;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public void Dispose()
    {
        _mutex.Dispose();
    }
}
