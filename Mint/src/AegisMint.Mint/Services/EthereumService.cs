using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Util;

namespace AegisMint.Mint.Services;

/// <summary>
/// Service for interacting with Ethereum blockchain.
/// Supports both Nethereum (high-level) and JSON-RPC (low-level) approaches.
/// </summary>
public class EthereumService
{
    private string _rpcUrl;
    private Web3 _web3;
    private JsonRpcClient _jsonRpc;
    private ContractDeployer _deployer;

    public EthereumService(string rpcUrl)
    {
        _rpcUrl = rpcUrl;
        _web3 = new Web3(rpcUrl);
        _jsonRpc = new JsonRpcClient(rpcUrl);
        _deployer = new ContractDeployer(_jsonRpc, rpcUrl);
        
        Logger.Info($"EthereumService initialized with RPC: {rpcUrl}");
    }

    public void SetRpcUrl(string rpcUrl)
    {
        _rpcUrl = rpcUrl;
        _web3 = new Web3(rpcUrl);
        _jsonRpc = new JsonRpcClient(rpcUrl);
        _deployer = new ContractDeployer(_jsonRpc, rpcUrl);
        
        Logger.Info($"RPC URL updated to: {rpcUrl}");
    }

    /// <summary>
    /// Deploys a contract using pure JSON-RPC with full control.
    /// </summary>
    public async Task<DeploymentResult> DeployContractAsync(
        string privateKey,
        string contractBytecode,
        string contractAbi,
        params object[] constructorParams)
    {
        Logger.Info("Deploying contract via JSON-RPC");
        return await _deployer.DeployContractAsync(privateKey, contractBytecode, contractAbi, constructorParams);
    }

    /// <summary>
    /// Calls a contract method after deployment (e.g., initialize).
    /// </summary>
    public async Task<string> CallContractMethodAsync(
        string privateKey,
        string contractAddress,
        string contractAbi,
        string methodName,
        params object[] parameters)
    {
        Logger.Info($"Calling contract method: {methodName}");
        return await _deployer.CallContractMethodAsync(privateKey, contractAddress, contractAbi, methodName, parameters);
    }

    /// <summary>
    /// Deploys an ERC-20 token contract with initialization.
    /// </summary>
    public async Task<TokenDeploymentResult> DeployTokenAsync(
        string privateKey,
        string tokenAbi,
        string tokenBytecode,
        string name,
        string symbol,
        byte decimals)
    {
        try
        {
            Logger.Info($"Deploying token: {name} ({symbol}) with {decimals} decimals");

            // Step 1: Deploy the contract (constructor parameters if needed)
            var deployResult = await _deployer.DeployContractAsync(
                privateKey,
                tokenBytecode,
                tokenAbi);

            if (!deployResult.Success || string.IsNullOrEmpty(deployResult.ContractAddress))
            {
                Logger.Error($"Token deployment failed: {deployResult.ErrorMessage}");
                return new TokenDeploymentResult
                {
                    Success = false,
                    ErrorMessage = deployResult.ErrorMessage ?? "Deployment failed"
                };
            }

            Logger.Info($"Token contract deployed at: {deployResult.ContractAddress}");

            // Step 2: Initialize the token (if it has an initialize method)
            string? initTxHash = null;
            try
            {
                Logger.Info("Calling initialize method...");
                initTxHash = await _deployer.CallContractMethodAsync(
                    privateKey,
                    deployResult.ContractAddress,
                    tokenAbi,
                    "initialize",
                    name,
                    symbol,
                    decimals);

                Logger.Info($"Token initialized successfully. Tx: {initTxHash}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Initialize call failed (might not be needed): {ex.Message}");
                // Some tokens don't need initialization, so this is not necessarily an error
            }

            return new TokenDeploymentResult
            {
                Success = true,
                ContractAddress = deployResult.ContractAddress,
                DeploymentTxHash = deployResult.TransactionHash,
                InitializeTxHash = initTxHash,
                GasUsed = deployResult.GasUsed,
                BlockNumber = deployResult.BlockNumber
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Token deployment failed", ex);
            return new TokenDeploymentResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets the ETH balance for an address using direct JSON-RPC.
    /// </summary>
    public async Task<decimal> GetBalanceAsync(string address)
    {
        try
        {
            Logger.Debug($"Getting balance for address: {address}");
            
            // Use JSON-RPC directly
            var balanceHex = await _jsonRpc.GetBalanceAsync(address);
            var balanceWei = HexToBigInteger(balanceHex);
            var balanceEth = Web3.Convert.FromWei(balanceWei);
            
            Logger.Debug($"Balance for {address}: {balanceEth} ETH");
            return balanceEth;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get balance for {address}", ex);
            throw new InvalidOperationException($"Failed to get balance for {address}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the ETH balance using Nethereum (fallback method).
    /// </summary>
    public async Task<decimal> GetBalanceWithNethereumAsync(string address)
    {
        try
        {
            Logger.Debug($"Getting balance (Nethereum) for address: {address}");
            var balance = await _web3.Eth.GetBalance.SendRequestAsync(address);
            return Web3.Convert.FromWei(balance.Value);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get balance (Nethereum) for {address}", ex);
            throw new InvalidOperationException($"Failed to get balance for {address}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if an address has sufficient balance for gas.
    /// </summary>
    public async Task<bool> HasSufficientBalanceAsync(string address, decimal minimumBalanceEth = 0.01m)
    {
        try
        {
            var balance = await GetBalanceAsync(address);
            return balance >= minimumBalanceEth;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error checking balance for {address}", ex);
            return false;
        }
    }

    /// <summary>
    /// Gets the current network name using JSON-RPC.
    /// </summary>
    public async Task<string> GetNetworkNameAsync()
    {
        try
        {
            var chainIdHex = await _jsonRpc.GetChainIdAsync();
            var chainId = (int)HexToBigInteger(chainIdHex);
            
            var networkName = chainId switch
            {
                1 => "Ethereum Mainnet",
                11155111 => "Sepolia Testnet",
                5 => "Goerli Testnet",
                1337 => "Localhost",
                _ => $"Unknown Network (Chain ID: {chainId})"
            };
            
            Logger.Info($"Connected to network: {networkName} (Chain ID: {chainId})");
            return networkName;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to get network name", ex);
            throw new InvalidOperationException($"Failed to get network name: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates that the RPC endpoint is accessible using JSON-RPC.
    /// </summary>
    public async Task<bool> ValidateConnectionAsync()
    {
        try
        {
            Logger.Debug("Validating RPC connection...");
            await _jsonRpc.GetChainIdAsync();
            Logger.Info("RPC connection validated successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("RPC connection validation failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Gets the current gas price in Gwei.
    /// </summary>
    public async Task<decimal> GetGasPriceGweiAsync()
    {
        try
        {
            var gasPriceHex = await _jsonRpc.GetGasPriceAsync();
            var gasPriceWei = HexToBigInteger(gasPriceHex);
            var gasPriceGwei = Web3.Convert.FromWei(gasPriceWei, UnitConversion.EthUnit.Gwei);
            
            Logger.Debug($"Current gas price: {gasPriceGwei} Gwei");
            return gasPriceGwei;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to get gas price", ex);
            throw new InvalidOperationException($"Failed to get gas price: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the transaction count (nonce) for an address.
    /// </summary>
    public async Task<BigInteger> GetNonceAsync(string address)
    {
        try
        {
            var nonceHex = await _jsonRpc.GetTransactionCountAsync(address);
            var nonce = HexToBigInteger(nonceHex);
            
            Logger.Debug($"Nonce for {address}: {nonce}");
            return nonce;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get nonce for {address}", ex);
            throw new InvalidOperationException($"Failed to get nonce: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Access to the underlying JSON-RPC client for custom calls.
    /// </summary>
    public JsonRpcClient JsonRpc => _jsonRpc;

    /// <summary>
    /// Access to the underlying Nethereum Web3 instance for complex operations.
    /// </summary>
    public Web3 Web3 => _web3;

    /// <summary>
    /// Access to the contract deployer for direct deployment control.
    /// </summary>
    public ContractDeployer Deployer => _deployer;

    private static BigInteger HexToBigInteger(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex == "0x" || hex == "0x0")
            return BigInteger.Zero;

        // Remove 0x prefix
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Substring(2);

        return BigInteger.Parse(hex, System.Globalization.NumberStyles.HexNumber);
    }
}

public class TokenDeploymentResult
{
    public bool Success { get; set; }
    public string? ContractAddress { get; set; }
    public string? DeploymentTxHash { get; set; }
    public string? InitializeTxHash { get; set; }
    public string? GasUsed { get; set; }
    public string? BlockNumber { get; set; }
    public string? ErrorMessage { get; set; }
}
