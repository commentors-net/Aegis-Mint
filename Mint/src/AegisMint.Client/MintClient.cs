using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AegisMint.Core.Contracts;

namespace AegisMint.Client;

public class MintClient : IDisposable
{
    private readonly MintClientOptions _options;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private bool _disposed;

    public MintClient(MintClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public static MintClient CreateDefault(MintClientOptions? options = null)
    {
        options ??= new MintClientOptions();
        return new MintClient(options);
    }

    public async Task<MintClientResult<PingResponse>> PingAsync(CancellationToken cancellationToken = default)
    {
        var request = new ServiceRequest("ping");
        return await SendRequestAsync<PingResponse>(request, cancellationToken);
    }

    public async Task<MintClientResult<DeviceInfoResponse>> GetDeviceInfoAsync(CancellationToken cancellationToken = default)
    {
        var request = new ServiceRequest("getdeviceinfo");
        return await SendRequestAsync<DeviceInfoResponse>(request, cancellationToken);
    }

    public async Task<MintClientResult<HasMnemonicResponse>> HasMnemonicAsync(CancellationToken cancellationToken = default)
    {
        var request = new ServiceRequest("hasmnemonic");
        return await SendRequestAsync<HasMnemonicResponse>(request, cancellationToken);
    }

    public async Task<MintClientResult<SetMnemonicResponse>> SetMnemonicAsync(string mnemonic, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object> { { "mnemonic", mnemonic } };
        var request = new ServiceRequest("setmnemonic", parameters);
        return await SendRequestAsync<SetMnemonicResponse>(request, cancellationToken);
    }

    public async Task<MintClientResult<MnemonicResponse>> GetMnemonicAsync(CancellationToken cancellationToken = default)
    {
        var request = new ServiceRequest("getmnemonic");
        return await SendRequestAsync<MnemonicResponse>(request, cancellationToken);
    }

    public async Task<MintClientResult<UnlockStatusResponse>> UnlockForDevelopmentAsync(int minutes = 15, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object> { { "minutes", minutes } };
        var request = new ServiceRequest("unlockdev", parameters);
        return await SendRequestAsync<UnlockStatusResponse>(request, cancellationToken);
    }

    public async Task<MintClientResult<LockStatusResponse>> LockAsync(CancellationToken cancellationToken = default)
    {
        var request = new ServiceRequest("lock");
        return await SendRequestAsync<LockStatusResponse>(request, cancellationToken);
    }

    public async Task<MintClientResult<LogsResponse>> GetRecentLogsAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object> { { "limit", limit } };
        var request = new ServiceRequest("getrecentlogs", parameters);
        return await SendRequestAsync<LogsResponse>(request, cancellationToken);
    }

    private async Task<MintClientResult<T>> SendRequestAsync<T>(ServiceRequest request, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MintClient));
        }

        NamedPipeClientStream? pipeClient = null;
        StreamWriter? writer = null;
        StreamReader? reader = null;

        try
        {
            pipeClient = new NamedPipeClientStream(
                ".",
                _options.PipeName,
                PipeDirection.InOut,
                PipeOptions.None);

            await pipeClient.ConnectAsync(_options.ConnectTimeout, cancellationToken);

            // Send request
            var requestJson = JsonSerializer.Serialize(request, _serializerOptions);
            using (writer = new StreamWriter(pipeClient, Encoding.UTF8, leaveOpen: true) { AutoFlush = false })
            {
                await writer.WriteLineAsync(requestJson);
                await writer.FlushAsync();
            }

            // Read response
            using (reader = new StreamReader(pipeClient, Encoding.UTF8, leaveOpen: true))
            {
                    var responseLine = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(responseLine))
                {
                    return new MintClientResult<T>(false, default, 500, "Empty response from service");
                }

                var serviceResponse = JsonSerializer.Deserialize<ServiceResponse>(responseLine, _serializerOptions);
                if (serviceResponse == null)
                {
                    return new MintClientResult<T>(false, default, 500, "Invalid response format");
                }

                if (!serviceResponse.Success)
                {
                    return new MintClientResult<T>(false, default, serviceResponse.StatusCode, serviceResponse.ErrorMessage);
                }

                if (string.IsNullOrWhiteSpace(serviceResponse.Data))
                {
                    return new MintClientResult<T>(true, default, serviceResponse.StatusCode, null);
                }

                var payload = JsonSerializer.Deserialize<T>(serviceResponse.Data, _serializerOptions);
                return new MintClientResult<T>(true, payload, serviceResponse.StatusCode, null);
            }
        }
        catch (TimeoutException)
        {
            return new MintClientResult<T>(false, default, 408, "Connection timeout - ensure AegisMint Service is running");
        }
        catch (IOException ex)
        {
            return new MintClientResult<T>(false, default, 503, $"Cannot connect to service: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new MintClientResult<T>(false, default, 500, ex.Message);
        }
        finally
        {
            pipeClient?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
