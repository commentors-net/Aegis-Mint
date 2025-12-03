using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Util;

namespace AegisMint.Mint.Services;

/// <summary>
/// Service for interacting with Ethereum blockchain.
/// </summary>
public class EthereumService
{
    private string _rpcUrl;
    private Web3 _web3;

    public EthereumService(string rpcUrl)
    {
        _rpcUrl = rpcUrl;
        _web3 = new Web3(rpcUrl);
    }

    public void SetRpcUrl(string rpcUrl)
    {
        _rpcUrl = rpcUrl;
        _web3 = new Web3(rpcUrl);
    }

    /// <summary>
    /// Gets the ETH balance for an address.
    /// </summary>
    /// <param name="address">Ethereum address to check.</param>
    /// <returns>Balance in ETH as a decimal.</returns>
    public async Task<decimal> GetBalanceAsync(string address)
    {
        try
        {
            var balance = await _web3.Eth.GetBalance.SendRequestAsync(address);
            return Web3.Convert.FromWei(balance.Value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get balance for {address}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if an address has sufficient balance for gas.
    /// </summary>
    /// <param name="address">Ethereum address to check.</param>
    /// <param name="minimumBalanceEth">Minimum balance required in ETH (default 0.01 ETH).</param>
    /// <returns>True if balance is sufficient, false otherwise.</returns>
    public async Task<bool> HasSufficientBalanceAsync(string address, decimal minimumBalanceEth = 0.01m)
    {
        try
        {
            var balance = await GetBalanceAsync(address);
            return balance >= minimumBalanceEth;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current network name (mainnet, sepolia, etc.).
    /// </summary>
    public async Task<string> GetNetworkNameAsync()
    {
        try
        {
            var chainId = await _web3.Eth.ChainId.SendRequestAsync();
            var chainIdInt = (int)chainId.Value;
            
            return chainIdInt switch
            {
                1 => "Ethereum Mainnet",
                11155111 => "Sepolia Testnet",
                5 => "Goerli Testnet",
                _ => $"Unknown Network (Chain ID: {chainId.Value})"
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get network name: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates that the RPC endpoint is accessible.
    /// </summary>
    public async Task<bool> ValidateConnectionAsync()
    {
        try
        {
            await _web3.Eth.ChainId.SendRequestAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
