using System;
using System.Numerics;
using Nethereum.Web3.Accounts;
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;

namespace AegisMint.Mint.Services;

/// <summary>
/// Service for signing Ethereum transactions using Nethereum's Account class.
/// </summary>
public class TransactionSigner
{
    private readonly Account _account;

    public TransactionSigner(string privateKeyHex)
    {
        // Remove 0x prefix if present
        if (privateKeyHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            privateKeyHex = privateKeyHex.Substring(2);
        }

        _account = new Account(privateKeyHex);
        Logger.Debug($"Transaction signer initialized for address: {_account.Address}");
    }

    public string GetAddress()
    {
        return _account.Address;
    }

    public Account GetAccount()
    {
        return _account;
    }

    /// <summary>
    /// Signs a raw transaction and returns the signed transaction hex string.
    /// </summary>
    public string SignTransaction(
        BigInteger nonce,
        BigInteger gasPrice,
        BigInteger gasLimit,
        string to,
        BigInteger value,
        string data,
        BigInteger chainId)
    {
        try
        {
            Logger.Debug($"Signing transaction - Nonce: {nonce}, GasPrice: {gasPrice}, GasLimit: {gasLimit}, ChainId: {chainId}");

            // Use LegacyTransactionSigner for traditional transactions
            var signer = new LegacyTransactionSigner();
            
            // For contract deployment, 'to' is empty
            var toAddress = string.IsNullOrEmpty(to) ? null : to;

            // Sign the transaction with EIP-155 (chainId)
            var signedTx = signer.SignTransaction(
                _account.PrivateKey,
                chainId,
                toAddress,
                value,
                nonce,
                gasPrice,
                gasLimit,
                data);
            
            Logger.Debug($"Transaction signed successfully");
            return signedTx;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to sign transaction", ex);
            throw;
        }
    }

    /// <summary>
    /// Signs a contract deployment transaction.
    /// </summary>
    public string SignDeploymentTransaction(
        BigInteger nonce,
        BigInteger gasPrice,
        BigInteger gasLimit,
        string bytecodeWithConstructor,
        BigInteger chainId)
    {
        // For deployment, 'to' address is empty
        return SignTransaction(
            nonce,
            gasPrice,
            gasLimit,
            string.Empty,
            BigInteger.Zero,
            bytecodeWithConstructor,
            chainId);
    }

    /// <summary>
    /// Signs a contract method call transaction.
    /// </summary>
    public string SignContractCallTransaction(
        BigInteger nonce,
        BigInteger gasPrice,
        BigInteger gasLimit,
        string contractAddress,
        string encodedFunctionData,
        BigInteger chainId,
        BigInteger value = default)
    {
        return SignTransaction(
            nonce,
            gasPrice,
            gasLimit,
            contractAddress,
            value,
            encodedFunctionData,
            chainId);
    }
}
