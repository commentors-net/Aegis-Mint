using System;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;

namespace AegisMint.Mint.Services;

/// <summary>
/// Handles contract deployment using pure JSON-RPC calls.
/// Provides full control over the deployment process without Web3 abstractions.
/// </summary>
public class ContractDeployer
{
    private readonly JsonRpcClient _rpcClient;
    private readonly string _rpcUrl;

    public ContractDeployer(JsonRpcClient rpcClient, string rpcUrl)
    {
        _rpcClient = rpcClient;
        _rpcUrl = rpcUrl;
        Logger.Info("ContractDeployer initialized");
    }

    /// <summary>
    /// Deploys a contract and returns the contract address and transaction hash.
    /// </summary>
    public async Task<DeploymentResult> DeployContractAsync(
        string privateKey,
        string contractBytecode,
        string contractAbi,
        params object[] constructorParams)
    {
        try
        {
            Logger.Info("Starting contract deployment");

            // Step 1: Create transaction signer
            var signer = new TransactionSigner(privateKey);
            var fromAddress = signer.GetAddress();
            Logger.Info($"Deploying from address: {fromAddress}");

            // Step 2: Get chain ID
            var chainIdHex = await _rpcClient.GetChainIdAsync();
            var chainId = HexToBigInteger(chainIdHex);
            Logger.Info($"Chain ID: {chainId}");

            // Step 3: Get current nonce
            var nonceHex = await _rpcClient.GetTransactionCountAsync(fromAddress, "pending");
            var nonce = HexToBigInteger(nonceHex);
            Logger.Info($"Current nonce: {nonce}");

            // Step 4: Encode constructor parameters if any
            var deploymentData = contractBytecode;
            
            // Remove 0x prefix if present for processing
            if (deploymentData.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                deploymentData = deploymentData.Substring(2);
            }
            
            if (constructorParams != null && constructorParams.Length > 0)
            {
                Logger.Debug($"Encoding {constructorParams.Length} constructor parameters");
                var encodedParams = EncodeConstructorParameters(contractAbi, constructorParams);
                deploymentData = deploymentData + encodedParams; // Concatenate without 0x
            }

            // Ensure 0x prefix for the final data
            deploymentData = "0x" + deploymentData;

            Logger.Debug($"Deployment data length: {deploymentData.Length} characters");

            // Step 5: Estimate gas
            var estimateTransaction = new
            {
                from = fromAddress,
                data = deploymentData
            };

            BigInteger gasLimit;
            try
            {
                var gasEstimateHex = await _rpcClient.EstimateGasAsync(estimateTransaction);
                gasLimit = HexToBigInteger(gasEstimateHex);
                // Add 20% buffer
                gasLimit = gasLimit * 120 / 100;
                Logger.Info($"Estimated gas (with 20% buffer): {gasLimit}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Gas estimation failed, using default: {ex.Message}");
                gasLimit = 3_000_000; // Default fallback
            }

            // Step 6: Get current gas price
            var gasPriceHex = await _rpcClient.GetGasPriceAsync();
            var gasPrice = HexToBigInteger(gasPriceHex);
            // Add 10% to ensure transaction goes through
            gasPrice = gasPrice * 110 / 100;
            Logger.Info($"Gas price (with 10% buffer): {gasPrice} wei");

            // Step 7: Sign the deployment transaction
            var signedTx = signer.SignDeploymentTransaction(
                nonce,
                gasPrice,
                gasLimit,
                deploymentData,
                chainId);

            Logger.Debug("Transaction signed successfully");

            // Step 8: Send the signed transaction
            Logger.Info("Broadcasting deployment transaction...");
            var txHash = await _rpcClient.SendRawTransactionAsync(signedTx);
            Logger.Info($"Deployment transaction sent: {txHash}");

            // Step 9: Wait for transaction receipt
            Logger.Info("Waiting for transaction receipt...");
            var receipt = await WaitForTransactionReceiptAsync(txHash, maxAttempts: 60, delayMs: 2000);

            if (receipt == null)
            {
                throw new Exception("Transaction receipt not received after timeout");
            }

            // Step 10: Extract contract address from receipt
            var contractAddress = receipt.Value.TryGetProperty("contractAddress", out var addrProp)
                ? addrProp.GetString()
                : null;

            var status = receipt.Value.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString()
                : "0x0";

            if (status == "0x0")
            {
                Logger.Error("Transaction failed - status: 0x0");
                throw new Exception("Contract deployment transaction failed");
            }

            if (string.IsNullOrEmpty(contractAddress))
            {
                throw new Exception("Contract address not found in receipt");
            }

            Logger.Info($"? Contract deployed successfully at: {contractAddress}");

            return new DeploymentResult
            {
                ContractAddress = contractAddress,
                TransactionHash = txHash,
                Success = true,
                GasUsed = receipt.Value.TryGetProperty("gasUsed", out var gasUsedProp)
                    ? HexToBigInteger(gasUsedProp.GetString() ?? "0x0").ToString()
                    : "0",
                BlockNumber = receipt.Value.TryGetProperty("blockNumber", out var blockProp)
                    ? HexToBigInteger(blockProp.GetString() ?? "0x0").ToString()
                    : "0"
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Contract deployment failed", ex);
            return new DeploymentResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Calls a contract method after deployment (e.g., initialize()).
    /// </summary>
    public async Task<string> CallContractMethodAsync(
        string privateKey,
        string contractAddress,
        string contractAbi,
        string methodName,
        params object[] parameters)
    {
        try
        {
            Logger.Info($"Calling contract method: {methodName} on {contractAddress}");

            var signer = new TransactionSigner(privateKey);
            var fromAddress = signer.GetAddress();

            // Encode function call
            var functionCallData = EncodeFunctionCall(contractAbi, methodName, parameters);
            Logger.Debug($"Encoded function data: {functionCallData}");

            // Get chain ID
            var chainIdHex = await _rpcClient.GetChainIdAsync();
            var chainId = HexToBigInteger(chainIdHex);

            // Get nonce
            var nonceHex = await _rpcClient.GetTransactionCountAsync(fromAddress, "pending");
            var nonce = HexToBigInteger(nonceHex);

            // Estimate gas
            var estimateTransaction = new
            {
                from = fromAddress,
                to = contractAddress,
                data = functionCallData
            };

            var gasEstimateHex = await _rpcClient.EstimateGasAsync(estimateTransaction);
            var gasLimit = HexToBigInteger(gasEstimateHex) * 120 / 100; // 20% buffer

            // Get gas price
            var gasPriceHex = await _rpcClient.GetGasPriceAsync();
            var gasPrice = HexToBigInteger(gasPriceHex) * 110 / 100; // 10% buffer

            // Sign transaction
            var signedTx = signer.SignContractCallTransaction(
                nonce,
                gasPrice,
                gasLimit,
                contractAddress,
                functionCallData,
                chainId);

            // Send transaction
            var txHash = await _rpcClient.SendRawTransactionAsync(signedTx);
            Logger.Info($"Contract method call transaction sent: {txHash}");

            // Wait for receipt
            var receipt = await WaitForTransactionReceiptAsync(txHash, maxAttempts: 60, delayMs: 2000);

            if (receipt == null)
            {
                throw new Exception("Transaction receipt not received");
            }

            var status = receipt.Value.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString()
                : "0x0";

            if (status == "0x0")
            {
                throw new Exception("Transaction failed");
            }

            Logger.Info($"? Contract method {methodName} executed successfully");
            return txHash;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to call contract method {methodName}", ex);
            throw;
        }
    }

    private async Task<JsonElement?> WaitForTransactionReceiptAsync(string txHash, int maxAttempts = 60, int delayMs = 2000)
    {
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

    private string EncodeConstructorParameters(string abi, object[] parameters)
    {
        try
        {
            if (parameters == null || parameters.Length == 0)
            {
                return string.Empty;
            }

            // Parse ABI
            var contract = new ContractBuilder(abi, string.Empty);
            var constructorAbi = contract.ContractABI.Constructor;

            if (constructorAbi == null || constructorAbi.InputParameters == null || constructorAbi.InputParameters.Length == 0)
            {
                Logger.Warning("No constructor parameters found in ABI");
                return string.Empty;
            }

            // Use ParametersEncoder to encode the constructor parameters
            var encoder = new Nethereum.ABI.FunctionEncoding.ParametersEncoder();
            var encodedParamsBytes = encoder.EncodeParameters(constructorAbi.InputParameters, parameters);
            
            // Convert bytes to hex string (without 0x prefix)
            return encodedParamsBytes.ToHex();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to encode constructor parameters", ex);
            throw;
        }
    }

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

    private static BigInteger HexToBigInteger(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex == "0x" || hex == "0x0")
            return BigInteger.Zero;

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Substring(2);

        // Prefix with 0 to avoid two's complement negative interpretation
        return BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber);
    }
}

public class DeploymentResult
{
    public bool Success { get; set; }
    public string? ContractAddress { get; set; }
    public string? TransactionHash { get; set; }
    public string? GasUsed { get; set; }
    public string? BlockNumber { get; set; }
    public string? ErrorMessage { get; set; }
}
