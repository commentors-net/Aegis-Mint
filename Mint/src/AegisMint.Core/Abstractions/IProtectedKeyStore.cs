using System.Threading;
using System.Threading.Tasks;

namespace AegisMint.Core.Abstractions;

public interface IProtectedKeyStore
{
    Task<byte[]> GetOrCreateKeyAsync(int sizeBytes, CancellationToken cancellationToken);
}
