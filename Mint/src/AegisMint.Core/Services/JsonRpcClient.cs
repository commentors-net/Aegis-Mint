using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AegisMint.Core.Services;

/// <summary>
/// Direct JSON-RPC client for Ethereum communication.
/// Provides more control and flexibility than Nethereum abstractions.
/// </summary>
public class JsonRpcClient
{
    private readonly HttpClient _httpClient;
    private readonly string _rpcUrl;
    private int _requestId = 1;

    public JsonRpcClient(string rpcUrl)
    {
        _rpcUrl = rpcUrl;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Sends a JSON-RPC request and returns the result.
    /// </summary>
    public async Task<JsonElement> SendRequestAsync(string method, params object[] parameters)
    {
        var requestId = _requestId++;
        
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Method = method,
            Params = parameters,
            Id = requestId
        };

        var jsonRequest = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        Logger.Debug($"JSON-RPC Request: {method} - {jsonRequest}");

        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        
        try
        {
            var response = await _httpClient.PostAsync(_rpcUrl, content);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            Logger.Debug($"JSON-RPC Response: {jsonResponse}");

            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"HTTP error {response.StatusCode}: {jsonResponse}");
                throw new Exception($"HTTP error {response.StatusCode}: {jsonResponse}");
            }

            var rpcResponse = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            if (rpcResponse?.Error != null)
            {
                var errorMessage = $"RPC Error {rpcResponse.Error.Code}: {rpcResponse.Error.Message}";
                Logger.Error(errorMessage);
                throw new Exception(errorMessage);
            }

            if (rpcResponse?.Result == null)
            {
                throw new Exception("No result in RPC response");
            }

            return rpcResponse.Result.Value;
        }
        catch (Exception ex)
        {
            Logger.Error($"JSON-RPC request failed: {method}", ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the balance of an Ethereum address in Wei.
    /// </summary>
    public async Task<string> GetBalanceAsync(string address, string blockTag = "latest")
    {
        var result = await SendRequestAsync("eth_getBalance", address, blockTag);
        return result.GetString() ?? "0x0";
    }

    /// <summary>
    /// Gets the current chain ID.
    /// </summary>
    public async Task<string> GetChainIdAsync()
    {
        var result = await SendRequestAsync("eth_chainId");
        return result.GetString() ?? "0x0";
    }

    /// <summary>
    /// Gets the transaction count (nonce) for an address.
    /// </summary>
    public async Task<string> GetTransactionCountAsync(string address, string blockTag = "latest")
    {
        var result = await SendRequestAsync("eth_getTransactionCount", address, blockTag);
        return result.GetString() ?? "0x0";
    }

    /// <summary>
    /// Estimates gas for a transaction.
    /// </summary>
    public async Task<string> EstimateGasAsync(object transaction)
    {
        var result = await SendRequestAsync("eth_estimateGas", transaction);
        return result.GetString() ?? "0x0";
    }

    /// <summary>
    /// Gets the current gas price.
    /// </summary>
    public async Task<string> GetGasPriceAsync()
    {
        var result = await SendRequestAsync("eth_gasPrice");
        return result.GetString() ?? "0x0";
    }

    /// <summary>
    /// Sends a raw signed transaction.
    /// </summary>
    public async Task<string> SendRawTransactionAsync(string signedTransaction)
    {
        var result = await SendRequestAsync("eth_sendRawTransaction", signedTransaction);
        return result.GetString() ?? string.Empty;
    }

    /// <summary>
    /// Gets a transaction receipt.
    /// </summary>
    public async Task<JsonElement> GetTransactionReceiptAsync(string txHash)
    {
        return await SendRequestAsync("eth_getTransactionReceipt", txHash);
    }

    /// <summary>
    /// Calls a contract method (read-only).
    /// </summary>
    public async Task<string> CallAsync(object transaction, string blockTag = "latest")
    {
        var result = await SendRequestAsync("eth_call", transaction, blockTag);
        return result.GetString() ?? "0x";
    }

    /// <summary>
    /// Gets the current block number.
    /// </summary>
    public async Task<string> GetBlockNumberAsync()
    {
        var result = await SendRequestAsync("eth_blockNumber");
        return result.GetString() ?? "0x0";
    }

    private class JsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public object[] Params { get; set; } = Array.Empty<object>();

        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    private class JsonRpcResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string? JsonRpc { get; set; }

        [JsonPropertyName("result")]
        public JsonElement? Result { get; set; }

        [JsonPropertyName("error")]
        public JsonRpcError? Error { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    private class JsonRpcError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public JsonElement? Data { get; set; }
    }
}
