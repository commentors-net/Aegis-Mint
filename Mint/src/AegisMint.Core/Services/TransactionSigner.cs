using System;
using System.Numerics;
using Nethereum.Web3.Accounts;
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace AegisMint.Core.Services;

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

            // Ensure data has 0x prefix
            if (!string.IsNullOrEmpty(data) && !data.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                data = "0x" + data;
            }

            // Convert data to byte array first to avoid parsing issues
            byte[] dataBytes = string.IsNullOrEmpty(data) || data == "0x" 
                ? Array.Empty<byte>() 
                : data.HexToByteArray();

            // Use LegacyTransactionSigner with byte array for data
            var signer = new LegacyTransactionSigner();
            
            // For contract deployment, 'to' should be empty string
            var toAddress = string.IsNullOrWhiteSpace(to) ? string.Empty : to;

            // Convert private key to byte array
            var privateKeyBytes = _account.PrivateKey.HexToByteArray();

            // Sign the transaction - pass data as byte array by converting back to hex
            // This avoids the internal parsing issue
            var signedTx = signer.SignTransaction(
                privateKeyBytes,
                chainId,
                toAddress,
                value,
                nonce,
                gasPrice,
                gasLimit,
                dataBytes.ToHex(true)); // Convert bytes back to hex with 0x prefix
            
            Logger.Debug($"Transaction signed successfully");
            // Ensure 0x prefix for downstream RPC
            return signedTx.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? signedTx
                : "0x" + signedTx;
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
