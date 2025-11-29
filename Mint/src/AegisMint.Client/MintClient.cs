using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AegisMint.Core.Contracts;

namespace AegisMint.Client;

public class MintClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public MintClient(HttpClient httpClient, MintClientOptions options)
    {
        _httpClient = httpClient;
        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = options.BaseAddress;
        }
        _httpClient.Timeout = options.Timeout;
    }

    public static MintClient CreateDefault(MintClientOptions? options = null)
    {
        options ??= new MintClientOptions();
        var client = new HttpClient
        {
            BaseAddress = options.BaseAddress,
            Timeout = options.Timeout
        };
        return new MintClient(client, options);
    }

    public async Task<MintClientResult<PingResponse>> PingAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("ping", cancellationToken).ConfigureAwait(false);
        return await ReadResultAsync<PingResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MintClientResult<DeviceInfoResponse>> GetDeviceInfoAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("getDeviceInfo", cancellationToken).ConfigureAwait(false);
        return await ReadResultAsync<DeviceInfoResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MintClientResult<MnemonicResponse>> GetMnemonicAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("getMnemonic", cancellationToken).ConfigureAwait(false);
        return await ReadResultAsync<MnemonicResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MintClientResult<object>> UnlockForDevelopmentAsync(int minutes = 15, CancellationToken cancellationToken = default)
    {
        var request = new UnlockRequest(minutes);
        var response = await _httpClient.PostAsJsonAsync("governance/unlock/dev", request, _serializerOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadResultAsync<object>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MintClientResult<object>> LockAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("governance/lock", null, cancellationToken).ConfigureAwait(false);
        return await ReadResultAsync<object>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MintClientResult<LogsResponse>> GetRecentLogsAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"logs/recent?limit={limit}", cancellationToken).ConfigureAwait(false);
        return await ReadResultAsync<LogsResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MintClientResult<T>> ReadResultAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<T>(_serializerOptions, cancellationToken).ConfigureAwait(false);
                return new MintClientResult<T>(true, payload, response.StatusCode, null);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new MintClientResult<T>(false, default, response.StatusCode,
                string.IsNullOrWhiteSpace(errorBody) ? response.ReasonPhrase : errorBody);
        }
        catch (Exception ex)
        {
            return new MintClientResult<T>(false, default, response.StatusCode, ex.Message);
        }
        finally
        {
            response.Dispose();
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
