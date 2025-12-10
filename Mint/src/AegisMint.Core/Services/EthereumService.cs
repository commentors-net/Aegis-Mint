using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Util;
using Nethereum.Contracts;

namespace AegisMint.Core.Services;

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
    /// Deploys an ERC-20 token contract with proxy and initialization.
    /// </summary>
    public async Task<TokenDeploymentResult> DeployTokenAsync(
        string privateKey,
        string tokenAbi,
        string tokenBytecode,
        string name,
        string symbol,
        byte decimals,
        BigInteger initialSupply,
        string? proxyAdminAddress = null)
    {
        try
        {
            Logger.Info($"Deploying token: {name} ({symbol}) with {decimals} decimals");

            // Step 1: Deploy the implementation contract (the actual token contract)
            Logger.Info("Step 1: Deploying implementation contract...");
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

            var implementationAddress = deployResult.ContractAddress;
            Logger.Info($"Implementation contract deployed at: {implementationAddress}");

            // Step 2: Deploy the proxy contract
            Logger.Info("Step 2: Deploying proxy contract...");
            var proxyBytecode = GetProxyBytecode();
            var proxyAbi = GetProxyAbi();
            
            var proxyDeployResult = await _deployer.DeployContractAsync(
                privateKey,
                proxyBytecode,
                proxyAbi,
                implementationAddress); // Pass implementation address as constructor parameter

            if (!proxyDeployResult.Success || string.IsNullOrEmpty(proxyDeployResult.ContractAddress))
            {
                Logger.Error($"Proxy deployment failed: {proxyDeployResult.ErrorMessage}");
                return new TokenDeploymentResult
                {
                    Success = false,
                    ErrorMessage = $"Proxy deployment failed: {proxyDeployResult.ErrorMessage}"
                };
            }

            var proxyAddress = proxyDeployResult.ContractAddress;
            Logger.Info($"Proxy contract deployed at: {proxyAddress}");

            // Step 3: Change proxy admin (defaults to provided address, otherwise the deployer)
            string? changeAdminTxHash = null;
            var deployerAddress = new TransactionSigner(privateKey).GetAddress();
            var targetAdmin = string.IsNullOrWhiteSpace(proxyAdminAddress)
                ? deployerAddress
                : proxyAdminAddress;

            try
            {
                Logger.Info($"Step 3: Changing proxy admin to {targetAdmin}...");
                changeAdminTxHash = await _deployer.CallContractMethodAsync(
                    privateKey,
                    proxyAddress,
                    proxyAbi,
                    "changeAdmin",
                    targetAdmin);

                Logger.Info($"Proxy admin changed successfully. Tx: {changeAdminTxHash}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Change admin failed: {ex.Message}");
            }

            // Step 4: Initialize the token through the proxy
            string? initTxHash = null;
            try
            {
                Logger.Info("Step 4: Initializing token through proxy...");
                initTxHash = await _deployer.CallContractMethodAsync(
                    privateKey,
                    proxyAddress, // Call through proxy, not implementation
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

            // Step 5: Set enforcement role (asset protection role) to deployer
            string? assetProtectionTxHash = null;
            try
            {
                Logger.Info("Step 5: Setting asset protection role...");
                var signer = new TransactionSigner(privateKey);
                var deployerAddressForRole = signer.GetAddress();
                
                assetProtectionTxHash = await _deployer.CallContractMethodAsync(
                    privateKey,
                    proxyAddress, // Call through proxy
                    tokenAbi,
                    "setAssetProtectionRole",
                    deployerAddressForRole);

                Logger.Info($"Asset protection role set successfully. Tx: {assetProtectionTxHash}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Set asset protection role failed: {ex.Message}");
            }

            // Step 6: Mint initial supply to the caller (treasury) via increaseSupply
            string? increaseSupplyTxHash = null;
            try
            {
                if (initialSupply > 0)
                {
                    Logger.Info($"Step 6: Minting initial supply ({initialSupply} base units)...");
                    increaseSupplyTxHash = await _deployer.CallContractMethodAsync(
                        privateKey,
                        proxyAddress, // Call through proxy
                        tokenAbi,
                        "increaseSupply",
                        initialSupply);

                    Logger.Info($"Initial supply minted. Tx: {increaseSupplyTxHash}");
                }
                else
                {
                    Logger.Warning("Initial supply was zero; skipping increaseSupply");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"increaseSupply failed: {ex.Message}");
            }

            Logger.Info("✓ Token deployment with proxy completed successfully!");
            Logger.Info($"  - Implementation: {implementationAddress}");
            Logger.Info($"  - Proxy (use this address): {proxyAddress}");

            return new TokenDeploymentResult
            {
                Success = true,
                ContractAddress = implementationAddress,
                ProxyAddress = proxyAddress,
                DeploymentTxHash = deployResult.TransactionHash,
                ProxyDeploymentTxHash = proxyDeployResult.TransactionHash,
                ChangeAdminTxHash = changeAdminTxHash,
                InitializeTxHash = initTxHash,
                AssetProtectionTxHash = assetProtectionTxHash,
                IncreaseSupplyTxHash = increaseSupplyTxHash,
                GasUsed = deployResult.GasUsed,
                BlockNumber = deployResult.BlockNumber
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Token deployment with proxy failed", ex);
            return new TokenDeploymentResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets the AdminUpgradeabilityProxy bytecode.
    /// This is the OpenZeppelin AdminUpgradeabilityProxy v2.5.0 bytecode.
    /// </summary>
    private string GetProxyBytecode()
    {
        // OpenZeppelin AdminUpgradeabilityProxy bytecode (runtime + creation code)
        // This is the complete, working bytecode that doesn't have the JUMP issue
        return "0x608060405234801561001057600080fd5b5060405160208061080d83398101604081815291517f6f72672e7a657070656c696e6f732e70726f78792e696d706c656d656e74617482527f696f6e00000000000000000000000000000000000000000000000000000000006020830152915190819003602301902081906000805160206107ed8339815191521461009157fe5b6100a381640100000000610104810204565b50604080517f6f72672e7a657070656c696e6f732e70726f78792e61646d696e0000000000008152905190819003601a0190206000805160206107cd833981519152146100ec57fe5b6100fe336401000000006101c2810204565b506101dc565b600061011c826401000000006105ae6101d482021704565b15156101af57604080517f08c379a000000000000000000000000000000000000000000000000000000000815260206004820152603b60248201527f43616e6e6f742073657420612070726f787920696d706c656d656e746174696f60448201527f6e20746f2061206e6f6e2d636f6e747261637420616464726573730000000000606482015290519081900360840190fd5b506000805160206107ed83398151915255565b6000805160206107cd83398151915255565b6000903b1190565b6105e2806101eb6000396000f30060806040526004361061006c5763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416633659cfe681146100765780634f1ef286146100975780635c60da1b146100b75780638f283970146100e8578063f851a44014610109575b61007461011e565b005b34801561008257600080fd5b50610074600160a060020a0360043516610138565b61007460048035600160a060020a03169060248035908101910135610172565b3480156100c357600080fd5b506100cc6101ea565b60408051600160a060020a039092168252519081900360200190f35b3480156100f457600080fd5b50610074600160a060020a0360043516610227565b34801561011557600080fd5b506100cc610339565b610126610364565b610136610131610411565b610436565b565b61014061045a565b600160a060020a031633600160a060020a03161415610167576101628161047f565b61016f565b61016f61011e565b50565b61017a61045a565b600160a060020a031633600160a060020a031614156101dd5761019c8361047f565b30600160a060020a03163483836040518083838082843782019150509250505060006040518083038185875af19250505015156101d857600080fd5b6101e5565b6101e561011e565b505050565b60006101f461045a565b600160a060020a031633600160a060020a0316141561021c57610215610411565b9050610224565b61022461011e565b90565b61022f61045a565b600160a060020a031633600160a060020a0316141561016757600160a060020a03811615156102e557604080517f08c379a000000000000000000000000000000000000000000000000000000000815260206004820152603660248201527f43616e6e6f74206368616e6765207468652061646d696e206f6620612070726f60448201527f787920746f20746865207a65726f206164647265737300000000000000000000606482015290519081900360840190fd5b7f7e644d79422f17c01e4894b5f4f588d331ebfa28653d42ae832dc59e38c9798f61030e61045a565b60408051600160a060020a03928316815291841660208301528051918290030190a1610162816104c7565b600061034361045a565b600160a060020a031633600160a060020a0316141561021c5761021561045a565b61036c61045a565b600160a060020a031633141561040957604080517f08c379a000000000000000000000000000000000000000000000000000000000815260206004820152603260248201527f43616e6e6f742063616c6c2066616c6c6261636b2066756e6374696f6e20667260448201527f6f6d207468652070726f78792061646d696e0000000000000000000000000000606482015290519081900360840190fd5b610136610136565b7f7050c9e0f4ca769c69bd3a8ef740bc37934f8e2c036e5a723fd8ee048ed3f8c35490565b3660008037600080366000845af43d6000803e808015610455573d6000f35b3d6000fd5b7f10d6a54a4754c8869d6886b5f5d7fbfa5b4522237ea5c60d11bc4e7a1ff9390b5490565b610488816104eb565b60408051600160a060020a038316815290517fbc7cd75a20ee27fd9adebab32041f755214dbc6bffa90cc0225b39da2e5c2d3b9181900360200190a150565b7f10d6a54a4754c8869d6886b5f5d7fbfa5b4522237ea5c60d11bc4e7a1ff9390b55565b60006104f6826105ae565b151561058957604080517f08c379a000000000000000000000000000000000000000000000000000000000815260206004820152603b60248201527f43616e6e6f742073657420612070726f787920696d706c656d656e746174696f60448201527f6e20746f2061206e6f6e2d636f6e747261637420616464726573730000000000606482015290519081900360840190fd5b507f7050c9e0f4ca769c69bd3a8ef740bc37934f8e2c036e5a723fd8ee048ed3f8c355565b6000903b11905600a165627a7a723058207d34409da9a956ec4a00adb0105e7af3a389440ad43fa99793797bfd87a95a14002910d6a54a4754c8869d6886b5f5d7fbfa5b4522237ea5c60d11bc4e7a1ff9390b7050c9e0f4ca769c69bd3a8ef740bc37934f8e2c036e5a723fd8ee048ed3f8c3";
    }

    /// <summary>
    /// Gets the AdminUpgradeabilityProxy ABI.
    /// This matches the OpenZeppelin AdminUpgradeabilityProxy v2.5.0.
    /// </summary>
    private string GetProxyAbi()
    {
        return @"[{'constant':false,'inputs':[{'name':'newImplementation','type':'address'}],'name':'upgradeTo','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':false,'inputs':[{'name':'newImplementation','type':'address'},{'name':'data','type':'bytes'}],'name':'upgradeToAndCall','outputs':[],'payable':true,'stateMutability':'payable','type':'function'},{'constant':true,'inputs':[],'name':'implementation','outputs':[{'name':'','type':'address'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'newAdmin','type':'address'}],'name':'changeAdmin','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':true,'inputs':[],'name':'admin','outputs':[{'name':'','type':'address'}],'payable':false,'stateMutability':'view','type':'function'},{'inputs':[{'name':'_implementation','type':'address'}],'payable':false,'stateMutability':'nonpayable','type':'constructor'},{'payable':true,'stateMutability':'payable','type':'fallback'},{'anonymous':false,'inputs':[{'indexed':false,'name':'previousAdmin','type':'address'},{'indexed':false,'name':'newAdmin','type':'address'}],'name':'AdminChanged','type':'event'},{'anonymous':false,'inputs':[{'indexed':false,'name':'implementation','type':'address'}],'name':'Upgraded','type':'event'}]";
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
    /// Gets the ERC-20 token balance for an owner address (using balanceOf).
    /// </summary>
    public async Task<decimal> GetTokenBalanceAsync(string tokenAbi, string tokenAddress, string ownerAddress, int decimals)
    {
        try
        {
            var contract = _web3.Eth.GetContract(tokenAbi, tokenAddress);
            var balanceOf = contract.GetFunction("balanceOf");
            var raw = await balanceOf.CallAsync<BigInteger>(ownerAddress);
            return Web3.Convert.FromWei(raw, decimals);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get token balance for {ownerAddress} on {tokenAddress}", ex);
            throw new InvalidOperationException($"Failed to get token balance: {ex.Message}", ex);
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

        // Prefix with 0 to force unsigned interpretation (avoid negative BigInteger)
        return BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber);
    }
}

public class TokenDeploymentResult
{
    public bool Success { get; set; }
    
    /// <summary>
    /// The implementation contract address (actual token logic)
    /// </summary>
    public string? ContractAddress { get; set; }
    
    /// <summary>
    /// The proxy contract address (use this address for interacting with the token)
    /// </summary>
    public string? ProxyAddress { get; set; }
    
    public string? DeploymentTxHash { get; set; }
    public string? ProxyDeploymentTxHash { get; set; }
    public string? ChangeAdminTxHash { get; set; }
    public string? InitializeTxHash { get; set; }
    public string? AssetProtectionTxHash { get; set; }
    public string? IncreaseSupplyTxHash { get; set; }
    public string? GasUsed { get; set; }
    public string? BlockNumber { get; set; }
    public string? ErrorMessage { get; set; }
}
