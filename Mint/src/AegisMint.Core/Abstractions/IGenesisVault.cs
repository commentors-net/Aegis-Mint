using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AegisMint.Core.Models;

namespace AegisMint.Core.Abstractions;

public interface IGenesisVault
{
    Task<string> GetOrCreateMnemonicAsync(CancellationToken cancellationToken);
    Task<string?> TryGetMnemonicAsync(CancellationToken cancellationToken);
    Task SetMnemonicAsync(string mnemonic, CancellationToken cancellationToken);
    Task<bool> HasMnemonicAsync(CancellationToken cancellationToken);
    Task DeleteMnemonicAsync(CancellationToken cancellationToken);
    Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ShamirShare>> GetOrCreateSharesAsync(CancellationToken cancellationToken);
}
