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
    /// Gets token decimals from the snapshots table for the current network.
    /// </summary>
    private int GetTokenDecimals()
    {
        try
        {
            var snapshot = _dataStore.GetDeploymentSnapshot(_currentNetwork);
            if (snapshot != null)
            {
                return snapshot.TokenDecimals;
            }
            
            Logger.Warning($"No snapshot found for network {_currentNetwork}, defaulting to 18 decimals");
            return 18; // Fallback to standard ERC20 decimals
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get token decimals from snapshot, defaulting to 18", ex);
            return 18; // Fallback to standard ERC20 decimals
        }
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

            // Get token decimals from database
            var decimals = GetTokenDecimals();
            Logger.Info($"Using {decimals} decimals for token calculations");

            // Convert amount to wei
            var amountInWei = ConvertToWei(amount, decimals);

            // Validate token balance using balanceOf from ABI
            var tokenBalance = await GetTokenBalanceInternalAsync(contractAddress, fromAddress);
            
            if (tokenBalance < amountInWei)
            {
                var errorMsg = $"Insufficient token balance. Balance: {FormatTokenAmount(tokenBalance, decimals)}, Required: {amount}";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenTransferStatus(transferId, "failed", null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            Logger.Info($"Token balance check passed. Balance: {FormatTokenAmount(tokenBalance, decimals)}, Transfer: {amount}");

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
    /// Gets token balance (public API for UI).
    /// </summary>
    public async Task<decimal?> GetTokenBalanceAsync(string contractAddress, string ownerAddress)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(contractAddress) || string.IsNullOrWhiteSpace(ownerAddress))
            {
                Logger.Warning("Contract address or owner address is empty");
                return null;
            }

            if (_rpcClient == null)
            {
                Logger.Warning("RPC client not initialized");
                return null;
            }

            var balanceWei = await GetTokenBalanceInternalAsync(contractAddress, ownerAddress);
            var decimals = GetTokenDecimals();
            var formatted = FormatTokenAmount(balanceWei, decimals);
            return decimal.TryParse(formatted, out var result) ? result : 0m;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get token balance for {ownerAddress}", ex);
            return null;
        }
    }

    /// <summary>
    /// Gets token balance in wei using balanceOf(address) function from the ABI.
    /// </summary>
    private async Task<BigInteger> GetTokenBalanceInternalAsync(string contractAddress, string ownerAddress)
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
    /// Gets ETH balance for an address (public API for UI).
    /// </summary>
    public async Task<decimal?> GetEthBalanceAsync(string address)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                Logger.Warning("Address is empty");
                return null;
            }

            if (_rpcClient == null)
            {
                Logger.Warning("RPC client not initialized");
                return null;
            }

            var balanceHex = await _rpcClient.GetBalanceAsync(address);
            var balanceWei = HexToBigInteger(balanceHex);
            var formatted = FormatWei(balanceWei);
            return decimal.TryParse(formatted, out var result) ? result : 0m;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get ETH balance for {address}", ex);
            return null;
        }
    }

    /// <summary>
    /// Gets total supply of tokens (public API for UI).
    /// </summary>
    public async Task<decimal?> GetTotalSupplyAsync(string contractAddress)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(contractAddress))
            {
                Logger.Warning("Contract address is empty");
                return null;
            }

            if (_rpcClient == null)
            {
                Logger.Warning("RPC client not initialized");
                return null;
            }

            // Load ABI
            var artifactLoader = new ContractArtifactLoader();
            var artifacts = artifactLoader.LoadTokenImplementation();

            // Encode totalSupply() function call
            var encodedData = EncodeFunctionCall(artifacts.Abi ?? string.Empty, "totalSupply", Array.Empty<object>());

            var callTransaction = new
            {
                to = contractAddress,
                data = encodedData
            };

            var resultHex = await _rpcClient.CallAsync(callTransaction);
            
            // Decode the result
            if (string.IsNullOrEmpty(resultHex) || resultHex == "0x")
            {
                return 0m;
            }

            var supplyWei = HexToBigInteger(resultHex);
            var decimals = GetTokenDecimals();
            var formatted = FormatTokenAmount(supplyWei, decimals);
            return decimal.TryParse(formatted, out var result) ? result : 0m;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get total supply for {contractAddress}", ex);
            return null;
        }
    }

    /// <summary>
    /// Returns current paused state of the token contract.
    /// </summary>
    public async Task<bool?> GetPausedStatusAsync(string contractAddress)
    {
        try
        {
            if (_rpcClient == null)
            {
                Logger.Warning("RPC client not initialized for pause status");
                return null;
            }

            if (string.IsNullOrWhiteSpace(contractAddress))
            {
                Logger.Warning("Contract address is empty for pause status");
                return null;
            }

            var artifacts = new ContractArtifactLoader().LoadTokenImplementation();
            if (!artifacts.HasAbi)
            {
                Logger.Warning("Token ABI not found for pause status");
                return null;
            }

            var data = EncodeFunctionCall(artifacts.Abi!, "paused", Array.Empty<object>());
            var call = new { to = contractAddress, data };
            var resultHex = await _rpcClient.CallAsync(call);
            if (string.IsNullOrWhiteSpace(resultHex) || resultHex == "0x")
            {
                Logger.Warning("Pause status call returned empty result");
                return null;
            }

            var value = HexToBigInteger(resultHex);
            return value != BigInteger.Zero;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to get paused status", ex);
            return null;
        }
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
        // Split decimal into whole and fractional parts to avoid precision loss
        var wholePart = decimal.Truncate(amount);
        var fractionalPart = amount - wholePart;
        
        var multiplier = BigInteger.Pow(10, decimals);
        
        // Convert whole part (safe, no precision loss)
        var wholeWei = (BigInteger)wholePart * multiplier;
        
        // Convert fractional part (multiply first, then truncate)
        var fractionalWei = (BigInteger)(fractionalPart * (decimal)multiplier);
        
        return wholeWei + fractionalWei;
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
    /// Freezes or unfreezes an address using freeze/unfreeze functions from the ABI.
    /// </summary>
    public async Task<OperationResult> FreezeAddressAsync(
        string contractAddress,
        string targetAddress,
        bool freeze,
        string? reason = null)
    {
        if (_rpcClient == null)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = "Network not initialized. Please select a network."
            };
        }

        var freezeRecord = new FreezeOperation
        {
            Network = _currentNetwork,
            ContractAddress = contractAddress,
            TargetAddress = targetAddress,
            IsFrozen = freeze,
            Reason = reason,
            Status = "pending",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var freezeId = _dataStore.SaveFreezeOperation(freezeRecord);

        try
        {
            Logger.Info($"{(freeze ? "Freezing" : "Unfreezing")} address {targetAddress} on contract {contractAddress}");

            // Get treasury private key for signing
            var privateKey = _vaultManager.GetTreasuryPrivateKey();
            if (string.IsNullOrWhiteSpace(privateKey))
            {
                var errorMsg = "Treasury private key not found";
                Logger.Error(errorMsg);
                _dataStore.UpdateFreezeOperationStatus(freezeId, "failed", null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            var fromAddress = _vaultManager.GetTreasuryAddress();
            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                var errorMsg = "Treasury address not found";
                Logger.Error(errorMsg);
                _dataStore.UpdateFreezeOperationStatus(freezeId, "failed", null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            // Check ETH balance for gas
            var ethBalanceHex = await _rpcClient.GetBalanceAsync(fromAddress);
            var ethBalance = HexToBigInteger(ethBalanceHex);
            var gasPriceHex = await _rpcClient.GetGasPriceAsync();
            var gasPrice = HexToBigInteger(gasPriceHex);
            var estimatedGasLimit = BigInteger.Parse("50000"); // Estimated gas for freeze/unfreeze
            var estimatedGasCost = gasPrice * estimatedGasLimit;

            if (ethBalance < estimatedGasCost)
            {
                var errorMsg = $"Insufficient ETH for gas. Balance: {FormatWei(ethBalance)} ETH, Required: ~{FormatWei(estimatedGasCost)} ETH";
                Logger.Error(errorMsg);
                _dataStore.UpdateFreezeOperationStatus(freezeId, "failed", null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            Logger.Info($"ETH balance check passed. Balance: {FormatWei(ethBalance)} ETH");

            // Load contract ABI
            var artifactLoader = new ContractArtifactLoader();
            var artifacts = artifactLoader.LoadTokenImplementation();
            
            if (!artifacts.HasAbi)
            {
                _dataStore.UpdateFreezeOperationStatus(freezeId, "failed", null, "Token ABI not found in Resources folder");
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = "Token ABI not found in Resources folder"
                };
            }

            // Encode freeze or unfreeze function call
            var functionName = freeze ? "freeze" : "unfreeze";
            var encodedData = EncodeFunctionCall(artifacts.Abi ?? string.Empty, functionName, new object[] { targetAddress });

            // Get chain ID
            var chainIdHex = await _rpcClient.GetChainIdAsync();
            var chainId = HexToBigInteger(chainIdHex);

            // Get nonce
            var nonceHex = await _rpcClient.GetTransactionCountAsync(fromAddress, "pending");
            var nonce = HexToBigInteger(nonceHex);

            // Create transaction signer
            var signer = new TransactionSigner(privateKey);

            // Sign transaction
            var signedTx = signer.SignContractCallTransaction(
                nonce,
                gasPrice,
                estimatedGasLimit,
                contractAddress,
                encodedData,
                chainId);

            // Send transaction via JSON-RPC
            var txHash = await _rpcClient.SendRawTransactionAsync(signedTx);

            if (string.IsNullOrEmpty(txHash))
            {
                var errorMsg = "Transaction signing failed";
                Logger.Error(errorMsg);
                _dataStore.UpdateFreezeOperationStatus(freezeId, "failed", null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            Logger.Info($"Transaction sent: {txHash}");
            _dataStore.UpdateFreezeOperationStatus(freezeId, "submitted", txHash, null);

            // Wait for transaction receipt
            var receipt = await WaitForTransactionReceiptAsync(txHash);
            
            if (receipt == null)
            {
                var errorMsg = "Transaction receipt not received within timeout";
                Logger.Warning(errorMsg);
                _dataStore.UpdateFreezeOperationStatus(freezeId, "pending", txHash, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    TransactionHash = txHash,
                    ErrorMessage = errorMsg
                };
            }

            // Check transaction status
            var status = receipt.Value.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "0x0";
            var success = status == "0x1";

            if (success)
            {
                Logger.Info($"✓ Address {(freeze ? "frozen" : "unfrozen")} successfully");
                _dataStore.UpdateFreezeOperationStatus(freezeId, "confirmed", txHash, null);
                
                return new OperationResult
                {
                    Success = true,
                    TransactionHash = txHash
                };
            }
            else
            {
                var errorMsg = "Transaction failed on blockchain";
                Logger.Error($"{errorMsg}: {txHash}");
                _dataStore.UpdateFreezeOperationStatus(freezeId, "failed", txHash, errorMsg);
                
                return new OperationResult
                {
                    Success = false,
                    TransactionHash = txHash,
                    ErrorMessage = errorMsg
                };
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Freeze operation failed", ex);
            _dataStore.UpdateFreezeOperationStatus(freezeId, "failed", null, ex.Message);
            
            return new OperationResult
            {
                Success = false,
                ErrorMessage = $"Freeze operation failed: {ex.Message}"
            };
        }
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
        if (_rpcClient == null)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = "Network not initialized. Please select a network."
            };
        }

        var retrieveRecord = new TokenRetrieval
        {
            Network = _currentNetwork,
            ContractAddress = contractAddress,
            FromAddress = fromAddress,
            ToAddress = toAddress,
            Amount = amount?.ToString() ?? string.Empty,
            Reason = reason,
            Status = "pending",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var retrieveId = _dataStore.SaveTokenRetrieval(retrieveRecord);

        try
        {
            Logger.Info($"Retrieving tokens from {fromAddress} to {toAddress}");

            // Capture frozen balance (raw) for restore; wipe burns supply so we mint it back
            var decimals = GetTokenDecimals();
            var frozenBalanceRaw = await GetTokenBalanceInternalAsync(contractAddress, fromAddress);
            var frozenBalanceTokens = decimal.Parse(FormatTokenAmount(frozenBalanceRaw, decimals));
            Logger.Info($"Frozen address balance before wipe: {frozenBalanceTokens} tokens");

            // Get treasury private key for signing
            var privateKey = _vaultManager.GetTreasuryPrivateKey();
            if (string.IsNullOrWhiteSpace(privateKey))
            {
                var errorMsg = "Treasury private key not found";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenRetrievalStatus(retrieveId, "failed", null, null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            var signer = new TransactionSigner(privateKey);
            var fromAddr = signer.GetAddress();

            // Validate ETH balance for gas (need gas for 3 transactions)
            var ethBalanceHex = await _rpcClient.GetBalanceAsync(fromAddr);
            var ethBalance = HexToBigInteger(ethBalanceHex);

            var gasPriceHex = await _rpcClient.GetGasPriceAsync();
            var gasPrice = HexToBigInteger(gasPriceHex);
            var estimatedGasLimit = BigInteger.Parse("100000");
            var estimatedGasCost = gasPrice * estimatedGasLimit * 3; // Three transactions

            if (ethBalance < estimatedGasCost)
            {
                var errorMsg = $"Insufficient ETH for gas. Balance: {FormatWei(ethBalance)} ETH, Required: ~{FormatWei(estimatedGasCost)} ETH (for 3 transactions)";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenRetrievalStatus(retrieveId, "failed", null, null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            // Load contract ABI
            var artifactLoader = new ContractArtifactLoader();
            var artifacts = artifactLoader.LoadTokenImplementation();

            if (!artifacts.HasAbi)
            {
                var errorMsg = "Failed to load contract ABI";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenRetrievalStatus(retrieveId, "failed", null, null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            // Step 1: Call wipeFrozenAddress(address) to remove tokens from frozen address
            Logger.Info($"Step 1: Wiping frozen address {fromAddress}");
            var wipeData = EncodeFunctionCall(artifacts.Abi!, "wipeFrozenAddress", new object[] { fromAddress });
            if (string.IsNullOrWhiteSpace(wipeData))
            {
                var errorMsg = "Failed to encode wipeFrozenAddress function";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenRetrievalStatus(retrieveId, "failed", null, null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            var chainIdHex = await _rpcClient.GetChainIdAsync();
            var chainId = Convert.ToInt32(chainIdHex, 16);

            var nonceHex = await _rpcClient.GetTransactionCountAsync(fromAddr);
            var nonce = Convert.ToInt64(nonceHex, 16);

            var wipeTx = signer.SignContractCallTransaction(
                nonce,
                gasPrice,
                estimatedGasLimit,
                contractAddress,
                wipeData,
                chainId
            );

            var wipeTxHash = await _rpcClient.SendRawTransactionAsync(wipeTx);
            Logger.Info($"WipeFrozenAddress tx sent: {wipeTxHash}");

            // Wait for wipe transaction receipt
            var wipeReceipt = await WaitForTransactionReceiptAsync(wipeTxHash);
            if (wipeReceipt == null)
            {
                var errorMsg = "WipeFrozenAddress transaction receipt not received";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenRetrievalStatus(retrieveId, "failed", wipeTxHash, null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg,
                    TransactionHash = wipeTxHash
                };
            }

            var wipeStatus = wipeReceipt.Value.GetProperty("status").GetString();
            if (wipeStatus != "0x1")
            {
                var errorMsg = $"WipeFrozenAddress transaction failed with status {wipeStatus}";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenRetrievalStatus(retrieveId, "failed", wipeTxHash, null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg,
                    TransactionHash = wipeTxHash
                };
            }

            Logger.Info($"WipeFrozenAddress successful: {wipeTxHash}");

            // Step 2: restore burned supply to treasury via increaseSupply
            Logger.Info($"Step 2: Restoring wiped supply to treasury via increaseSupply({frozenBalanceRaw})");
            var increaseSupplyData = EncodeFunctionCall(artifacts.Abi!, "increaseSupply", new object[] { frozenBalanceRaw });
            if (string.IsNullOrWhiteSpace(increaseSupplyData))
            {
                var errorMsg = "Failed to encode increaseSupply function";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenRetrievalStatus(retrieveId, "failed", wipeTxHash, null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg,
                    TransactionHash = wipeTxHash
                };
            }

            var nonce2Hex = await _rpcClient.GetTransactionCountAsync(fromAddr);
            var nonce2 = Convert.ToInt64(nonce2Hex, 16);

            var increaseSupplyTx = signer.SignContractCallTransaction(
                nonce2,
                gasPrice,
                estimatedGasLimit,
                contractAddress,
                increaseSupplyData,
                chainId
            );

            var increaseSupplyTxHash = await _rpcClient.SendRawTransactionAsync(increaseSupplyTx);
            Logger.Info($"IncreaseSupply tx sent: {increaseSupplyTxHash}");

            // Wait for increaseSupply receipt
            var increaseSupplyReceipt = await WaitForTransactionReceiptAsync(increaseSupplyTxHash);
            if (increaseSupplyReceipt == null)
            {
                var errorMsg = "IncreaseSupply transaction receipt not received";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenRetrievalStatus(retrieveId, "wipe_success_mint_failed", wipeTxHash, increaseSupplyTxHash, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg,
                    TransactionHash = increaseSupplyTxHash
                };
            }

            var mintStatus = increaseSupplyReceipt.Value.GetProperty("status").GetString();
            if (mintStatus != "0x1")
            {
                var errorMsg = $"IncreaseSupply transaction failed with status {mintStatus}";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenRetrievalStatus(retrieveId, "wipe_success_mint_failed", wipeTxHash, increaseSupplyTxHash, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg,
                    TransactionHash = increaseSupplyTxHash
                };
            }

            Logger.Info($"IncreaseSupply successful: {increaseSupplyTxHash}");

            // Step 3: reclaimDIGG to sweep any contract-held tokens back to owner/treasury
            var reclaimData = EncodeFunctionCall(artifacts.Abi!, "reclaimDIGG", Array.Empty<object>());
            if (string.IsNullOrWhiteSpace(reclaimData))
            {
                var errorMsg = "Failed to encode reclaimDIGG function";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenRetrievalStatus(retrieveId, "failed", wipeTxHash, increaseSupplyTxHash, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg,
                    TransactionHash = wipeTxHash
                };
            }

            var nonce3Hex = await _rpcClient.GetTransactionCountAsync(fromAddr);
            var nonce3 = Convert.ToInt64(nonce3Hex, 16);

            var reclaimTx = signer.SignContractCallTransaction(
                nonce3,
                gasPrice,
                estimatedGasLimit,
                contractAddress,
                reclaimData,
                chainId
            );

            var reclaimTxHash = await _rpcClient.SendRawTransactionAsync(reclaimTx);
            Logger.Info($"ReclaimDIGG tx sent: {reclaimTxHash}");

            // Wait for reclaimDIGG transaction receipt
            var reclaimReceipt = await WaitForTransactionReceiptAsync(reclaimTxHash);
            if (reclaimReceipt == null)
            {
                var errorMsg = "ReclaimDIGG transaction receipt not received";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenRetrievalStatus(retrieveId, "wipe_success_mint_success_reclaim_failed", wipeTxHash, $"{increaseSupplyTxHash},{reclaimTxHash}", errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg,
                    TransactionHash = reclaimTxHash
                };
            }

            var reclaimStatus = reclaimReceipt.Value.GetProperty("status").GetString();
            if (reclaimStatus != "0x1")
            {
                var errorMsg = $"ReclaimDIGG transaction failed with status {reclaimStatus}";
                Logger.Error(errorMsg);
                _dataStore.UpdateTokenRetrievalStatus(retrieveId, "wipe_success_mint_success_reclaim_failed", wipeTxHash, $"{increaseSupplyTxHash},{reclaimTxHash}", errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg,
                    TransactionHash = reclaimTxHash
                };
            }

            Logger.Info($"ReclaimDIGG successful: {reclaimTxHash}");

            // Update database with success
            _dataStore.UpdateTokenRetrievalStatus(retrieveId, "completed", wipeTxHash, $"{increaseSupplyTxHash},{reclaimTxHash}", null);

            Logger.Info($"Token retrieval completed successfully. Wipe: {wipeTxHash}, Mint: {increaseSupplyTxHash}, Reclaim: {reclaimTxHash}");

            return new OperationResult
            {
                Success = true,
                TransactionHash = $"{wipeTxHash},{increaseSupplyTxHash},{reclaimTxHash}"
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"RetrieveTokensAsync failed", ex);
            _dataStore.UpdateTokenRetrievalStatus(retrieveId, "failed", null, null, ex.Message);
            return new OperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Pauses or unpauses the token contract using pause/unpause functions from the ABI.
    /// </summary>
    public async Task<OperationResult> SetPausedAsync(string contractAddress, bool pause)
    {
        if (_rpcClient == null)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = "Network not initialized. Please select a network."
            };
        }

        var pauseRecord = new PauseOperation
        {
            Network = _currentNetwork,
            ContractAddress = contractAddress,
            IsPaused = pause,
            Status = "pending",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var pauseId = _dataStore.SavePauseOperation(pauseRecord);

        try
        {
            Logger.Info($"{(pause ? "Pausing" : "Unpausing")} contract {contractAddress}");

            // Get treasury private key for signing
            var privateKey = _vaultManager.GetTreasuryPrivateKey();
            if (string.IsNullOrWhiteSpace(privateKey))
            {
                var errorMsg = "Treasury private key not found";
                Logger.Error(errorMsg);
                _dataStore.UpdatePauseOperationStatus(pauseId, "failed", null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            var fromAddress = _vaultManager.GetTreasuryAddress();
            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                var errorMsg = "Treasury address not found";
                Logger.Error(errorMsg);
                _dataStore.UpdatePauseOperationStatus(pauseId, "failed", null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            // Check ETH balance for gas
            var ethBalanceHex = await _rpcClient.GetBalanceAsync(fromAddress);
            var ethBalance = HexToBigInteger(ethBalanceHex);
            var gasPriceHex = await _rpcClient.GetGasPriceAsync();
            var gasPrice = HexToBigInteger(gasPriceHex);
            var estimatedGasLimit = BigInteger.Parse("50000"); // Estimated gas for pause/unpause
            var estimatedGasCost = gasPrice * estimatedGasLimit;

            if (ethBalance < estimatedGasCost)
            {
                var errorMsg = $"Insufficient ETH for gas. Balance: {FormatWei(ethBalance)} ETH, Required: ~{FormatWei(estimatedGasCost)} ETH";
                Logger.Error(errorMsg);
                _dataStore.UpdatePauseOperationStatus(pauseId, "failed", null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            Logger.Info($"ETH balance check passed. Balance: {FormatWei(ethBalance)} ETH");

            // Load contract ABI
            var artifactLoader = new ContractArtifactLoader();
            var artifacts = artifactLoader.LoadTokenImplementation();
            
            if (!artifacts.HasAbi)
            {
                _dataStore.UpdatePauseOperationStatus(pauseId, "failed", null, "Token ABI not found in Resources folder");
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = "Token ABI not found in Resources folder"
                };
            }

            // Encode pause or unpause function call (no parameters)
            var functionName = pause ? "pause" : "unpause";
            var encodedData = EncodeFunctionCall(artifacts.Abi ?? string.Empty, functionName, Array.Empty<object>());

            // Get chain ID
            var chainIdHex = await _rpcClient.GetChainIdAsync();
            var chainId = HexToBigInteger(chainIdHex);

            // Get nonce
            var nonceHex = await _rpcClient.GetTransactionCountAsync(fromAddress, "pending");
            var nonce = HexToBigInteger(nonceHex);

            // Create transaction signer
            var signer = new TransactionSigner(privateKey);

            // Sign transaction
            var signedTx = signer.SignContractCallTransaction(
                nonce,
                gasPrice,
                estimatedGasLimit,
                contractAddress,
                encodedData,
                chainId);

            // Send transaction via JSON-RPC
            var txHash = await _rpcClient.SendRawTransactionAsync(signedTx);

            if (string.IsNullOrEmpty(txHash))
            {
                var errorMsg = "Transaction signing failed";
                Logger.Error(errorMsg);
                _dataStore.UpdatePauseOperationStatus(pauseId, "failed", null, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            Logger.Info($"Transaction sent: {txHash}");
            _dataStore.UpdatePauseOperationStatus(pauseId, "submitted", txHash, null);

            // Wait for transaction receipt
            var receipt = await WaitForTransactionReceiptAsync(txHash);
            
            if (receipt == null)
            {
                var errorMsg = "Transaction receipt not received within timeout";
                Logger.Warning(errorMsg);
                _dataStore.UpdatePauseOperationStatus(pauseId, "pending", txHash, errorMsg);
                return new OperationResult
                {
                    Success = false,
                    TransactionHash = txHash,
                    ErrorMessage = errorMsg
                };
            }

            // Check transaction status
            var status = receipt.Value.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "0x0";
            var success = status == "0x1";

            if (success)
            {
                Logger.Info($"✓ Contract {(pause ? "paused" : "unpaused")} successfully");
                _dataStore.UpdatePauseOperationStatus(pauseId, "confirmed", txHash, null);
                
                return new OperationResult
                {
                    Success = true,
                    TransactionHash = txHash
                };
            }
            else
            {
                var errorMsg = "Transaction failed on blockchain";
                Logger.Error($"{errorMsg}: {txHash}");
                _dataStore.UpdatePauseOperationStatus(pauseId, "failed", txHash, errorMsg);
                
                return new OperationResult
                {
                    Success = false,
                    TransactionHash = txHash,
                    ErrorMessage = errorMsg
                };
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Pause operation failed", ex);
            _dataStore.UpdatePauseOperationStatus(pauseId, "failed", null, ex.Message);
            
            return new OperationResult
            {
                Success = false,
                ErrorMessage = $"Pause operation failed: {ex.Message}"
            };
        }
    }
}
