using System;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using AegisMint.Core.Models;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;

namespace AegisMint.Core.Services;

/// <summary>
/// Service for managing token control operations (transfer, freeze, retrieve, pause).
/// Uses JSON-RPC for direct blockchain interaction following the project's architecture.
/// </summary>
public class TokenControlService
{
    private readonly VaultDataStore _dataStore;
    private readonly VaultManager _vaultManager;
    private JsonRpcClient? _rpcClient;
    private string _currentNetwork = "sepolia";
    private string _currentRpcUrl = string.Empty;

    public TokenControlService(VaultManager vaultManager)
    {
        _dataStore = new VaultDataStore();
        _vaultManager = vaultManager;
    }

    public void SetNetwork(string network, string rpcUrl)
    {
        _currentNetwork = network;
        _currentRpcUrl = rpcUrl;
        _rpcClient = new JsonRpcClient(rpcUrl);
        Logger.Info($"TokenControlService network set to: {network} ({rpcUrl})");
    }

    /// <summary>
    /// Executes a token transfer from the treasury to a recipient.
    /// Validates ETH balance for gas and token balance before executing.
    /// Uses the transfer(address _to, uint256 _value) function from the TokenImplementationV2 ABI.
    /// </summary>
    public async Task<OperationResult> TransferTokensAsync(
        string contractAddress,
        string toAddress,
        decimal amount,
        string? memo = null)
    {
        if (_rpcClient == null)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = "Network not initialized. Please select a network."
            };
        }

        var transferRecord = new TokenTransfer
        {
            Network = _currentNetwork,
            ContractAddress = contractAddress,
            FromAddress = _vaultManager.GetTreasuryAddress() ?? "unknown",
            ToAddress = toAddress,
            Amount = amount.ToString(),
            Memo = memo,
            Status = "pending",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        long transferId = 0;

        try
        {
            // Save to database
            transferId = _dataStore.SaveTokenTransfer(transferRecord);
            Logger.Info($"Token transfer created with ID: {transferId}");

            // Get private key from vault
            var privateKey = GetPrivateKeyFromVault();
            if (string.IsNullOrEmpty(privateKey))
            {
                _dataStore.UpdateTokenTransferStatus(transferId, "failed", null, "Failed to retrieve private key from vault");
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = "Failed to retrieve private key from vault"
                };
            }

            var signer = new TransactionSigner(privateKey);
            var fromAddress = signer.GetAddress();

            // Validate ETH balance for gas
            var ethBalanceHex = await _rpcClient.GetBalanceAsync(fromAddress);
            var ethBalance = HexToBigInteger(ethBalanceHex);
            
            // Estimate gas cost (gas limit * gas price)
            var gasPriceHex = await _rpcClient.GetGasPriceAsync();
            var gasPrice = HexToBigInteger(gasPriceHex);
            var estimatedGasLimit = BigInteger.Parse("100000"); // Estimated gas for transfer
            var estimatedGasCost = gasPrice * estimatedGasLimit;

            if (ethBalance < estimatedGasCost)
            {
                var errorMsg = $"Insufficient ETH for gas. Balance: {FormatWei(ethBalance)} ETH, Required: ~{FormatWei(estimatedGasCost)} ETH";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenTransferStatus(transferId, "failed", null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            Logger.Info($"ETH balance check passed. Balance: {FormatWei(ethBalance)} ETH");

            // Load contract ABI from Resources folder
            var artifactLoader = new ContractArtifactLoader();
            var artifacts = artifactLoader.LoadTokenImplementation();
            
            if (!artifacts.HasAbi)
            {
                _dataStore.UpdateTokenTransferStatus(transferId, "failed", null, "Token ABI not found in Resources folder");
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = "Token ABI not found in Resources folder"
                };
            }

            // Convert amount to wei (assuming 18 decimals)
            var amountInWei = ConvertToWei(amount, 18);

            // Validate token balance using balanceOf from ABI
            var tokenBalance = await GetTokenBalanceAsync(contractAddress, fromAddress);
            
            if (tokenBalance < amountInWei)
            {
                var errorMsg = $"Insufficient token balance. Balance: {FormatTokenAmount(tokenBalance, 18)}, Required: {amount}";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenTransferStatus(transferId, "failed", null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            Logger.Info($"Token balance check passed. Balance: {FormatTokenAmount(tokenBalance, 18)}, Transfer: {amount}");

            // Encode transfer(address _to, uint256 _value) function call from ABI
            var functionCallData = EncodeFunctionCall(artifacts.Abi ?? string.Empty, "transfer", new object[] { toAddress, amountInWei });

            // Get chain ID
            var chainIdHex = await _rpcClient.GetChainIdAsync();
            var chainId = HexToBigInteger(chainIdHex);

            // Get nonce
            var nonceHex = await _rpcClient.GetTransactionCountAsync(fromAddress, "pending");
            var nonce = HexToBigInteger(nonceHex);

            // Estimate actual gas
            var estimateTransaction = new
            {
                from = fromAddress,
                to = contractAddress,
                data = functionCallData
            };

            var gasEstimateHex = await _rpcClient.EstimateGasAsync(estimateTransaction);
            var gasLimit = HexToBigInteger(gasEstimateHex) * 120 / 100; // 20% buffer

            // Update gas price with buffer
            gasPrice = gasPrice * 110 / 100; // 10% buffer

            Logger.Info($"Transfer - From: {fromAddress}, To: {toAddress}, Amount: {amount} tokens ({amountInWei} wei)");
            Logger.Info($"Gas - Limit: {gasLimit}, Price: {gasPrice} wei");

            // Sign transaction using JSON-RPC approach
            var signedTx = signer.SignContractCallTransaction(
                nonce,
                gasPrice,
                gasLimit,
                contractAddress,
                functionCallData,
                chainId);

            // Send transaction via JSON-RPC
            var transactionHash = await _rpcClient.SendRawTransactionAsync(signedTx);
            Logger.Info($"Transfer transaction sent: {transactionHash}");

            // Update database with transaction hash
            _dataStore.UpdateTokenTransferStatus(transferId, "pending", transactionHash);

            // Wait for transaction receipt
            var receipt = await WaitForTransactionReceiptAsync(transactionHash);

            if (receipt != null)
            {
                var status = receipt.Value.TryGetProperty("status", out var statusProp)
                    ? statusProp.GetString()
                    : "0x0";

                var success = status == "0x1";
                var finalStatus = success ? "success" : "failed";
                var error = !success ? "Transaction reverted" : null;
                
                _dataStore.UpdateTokenTransferStatus(transferId, finalStatus, transactionHash, error);

                Logger.Info($"Transfer completed with status: {finalStatus}");

                return new OperationResult
                {
                    Success = success,
                    TransactionHash = transactionHash,
                    ErrorMessage = error
                };
            }
            else
            {
                Logger.Warning("Transfer transaction receipt not available after timeout");
                return new OperationResult
                {
                    Success = true,
                    TransactionHash = transactionHash,
                    ErrorMessage = "Transaction submitted but receipt not confirmed yet"
                };
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Transfer failed", ex);
            
            if (transferId > 0)
            {
                _dataStore.UpdateTokenTransferStatus(transferId, "failed", null, ex.Message);
            }

            return new OperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Encodes a function call using the ABI and ContractBuilder.
    /// This matches the pattern used in ContractDeployer for consistency.
    /// </summary>
    private string EncodeFunctionCall(string abi, string functionName, object[] parameters)
    {
        try
        {
            var contract = new ContractBuilder(abi, string.Empty);
            var function = contract.GetFunctionBuilder(functionName);

            if (function == null)
            {
                throw new Exception($"Function {functionName} not found in ABI");
            }

            // Use the function builder to get encoded data
            var encoded = function.GetData(parameters);
            
            return encoded;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to encode function call for {functionName}", ex);
            throw;
        }
    }

    /// <summary>
    /// Gets token balance using balanceOf(address) function from the ABI.
    /// </summary>
    private async Task<BigInteger> GetTokenBalanceAsync(string contractAddress, string ownerAddress)
    {
        if (_rpcClient == null)
        {
            throw new InvalidOperationException("RPC client not initialized");
        }

        // Load ABI
        var artifactLoader = new ContractArtifactLoader();
        var artifacts = artifactLoader.LoadTokenImplementation();

        // Encode balanceOf(address) function call
        var encodedData = EncodeFunctionCall(artifacts.Abi ?? string.Empty, "balanceOf", new object[] { ownerAddress });

        var callTransaction = new
        {
            to = contractAddress,
            data = encodedData
        };

        var resultHex = await _rpcClient.CallAsync(callTransaction);
        
        // Decode the result
        if (string.IsNullOrEmpty(resultHex) || resultHex == "0x")
        {
            return BigInteger.Zero;
        }

        var balance = HexToBigInteger(resultHex);
        return balance;
    }

    /// <summary>
    /// Waits for transaction receipt with retries using JSON-RPC.
    /// </summary>
    private async Task<JsonElement?> WaitForTransactionReceiptAsync(string txHash, int maxAttempts = 60, int delayMs = 2000)
    {
        if (_rpcClient == null)
        {
            return null;
        }

        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                var receipt = await _rpcClient.GetTransactionReceiptAsync(txHash);
                
                if (receipt.ValueKind != JsonValueKind.Null)
                {
                    Logger.Debug($"Receipt received after {i + 1} attempts");
                    return receipt;
                }
            }
            catch
            {
                // Receipt not yet available
            }

            if (i < maxAttempts - 1)
            {
                await Task.Delay(delayMs);
            }
        }

        Logger.Warning($"Transaction receipt not received after {maxAttempts} attempts");
        return null;
    }

    /// <summary>
    /// Converts hex string to BigInteger.
    /// </summary>
    private static BigInteger HexToBigInteger(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex == "0x" || hex == "0x0")
        {
            return BigInteger.Zero;
        }

        var cleanHex = hex.StartsWith("0x") ? hex.Substring(2) : hex;
        return BigInteger.Parse("0" + cleanHex, System.Globalization.NumberStyles.HexNumber);
    }

    /// <summary>
    /// Converts decimal amount to wei.
    /// </summary>
    private static BigInteger ConvertToWei(decimal amount, int decimals)
    {
        var multiplier = BigInteger.Pow(10, decimals);
        var amountInWei = (BigInteger)(amount * (decimal)multiplier);
        return amountInWei;
    }

    /// <summary>
    /// Formats wei to ETH string.
    /// </summary>
    private static string FormatWei(BigInteger wei)
    {
        var eth = (decimal)wei / (decimal)BigInteger.Pow(10, 18);
        return eth.ToString("0.########");
    }

    /// <summary>
    /// Formats token amount from wei.
    /// </summary>
    private static string FormatTokenAmount(BigInteger amount, int decimals)
    {
        var divisor = BigInteger.Pow(10, decimals);
        var tokens = (decimal)amount / (decimal)divisor;
        return tokens.ToString("0.########");
    }

    /// <summary>
    /// Gets private key from vault (synchronous).
    /// </summary>
    private string? GetPrivateKeyFromVault()
    {
        try
        {
            var privateKey = _vaultManager.GetTreasuryPrivateKey();
            if (string.IsNullOrEmpty(privateKey))
            {
                Logger.Error("No treasury private key found in vault");
                return null;
            }

            return privateKey;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to get private key from vault", ex);
            return null;
        }
    }

    /// <summary>
    /// Freezes or unfreezes an address (stub - to be implemented with full JSON-RPC).
    /// </summary>
    public async Task<OperationResult> FreezeAddressAsync(
        string contractAddress,
        string targetAddress,
        bool freeze,
        string? reason = null)
    {
        // TODO: Implement using JSON-RPC pattern like TransferTokensAsync
        await Task.CompletedTask;
        return new OperationResult
        {
            Success = false,
            ErrorMessage = "Freeze operation not yet fully implemented"
        };
    }

    /// <summary>
    /// Retrieves tokens from an address (stub - to be implemented with full JSON-RPC).
    /// </summary>
    public async Task<OperationResult> RetrieveTokensAsync(
        string contractAddress,
        string fromAddress,
        string toAddress,
        decimal? amount = null,
        string? reason = null)
    {
        // TODO: Implement using JSON-RPC pattern like TransferTokensAsync
        await Task.CompletedTask;
        return new OperationResult
        {
            Success = false,
            ErrorMessage = "Retrieve operation not yet fully implemented"
        };
    }

    /// <summary>
    /// Pauses or unpauses the token contract (stub - to be implemented with full JSON-RPC).
    /// </summary>
    public async Task<OperationResult> SetPausedAsync(string contractAddress, bool pause)
    {
        // TODO: Implement using JSON-RPC pattern like TransferTokensAsync
        await Task.CompletedTask;
        return new OperationResult
        {
            Success = false,
            ErrorMessage = "Pause operation not yet fully implemented"
        };
    }
}
