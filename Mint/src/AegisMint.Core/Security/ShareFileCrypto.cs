using System;
using System.Security.Cryptography;
using System.Text;

namespace AegisMint.Core.Security;

public static class ShareFileCrypto
{
    public const string FileExtension = ".aegisshare";
    public const string EncryptionKeyId = "aegis-share-v1";

    private const byte PayloadVersion = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    // Shared app key for AegisMint share files (Base64-encoded 32-byte key).
    private static readonly byte[] ShareKey = Convert.FromBase64String("juQYpzarNHVJhRPS4nUlP6Gwqzy3s+wUfWdPBCsPTTM=");

    public static string EncryptShareJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Share JSON is empty.", nameof(json));
        }

        var plaintext = Encoding.UTF8.GetBytes(json);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(ShareKey, tagSizeInBytes: TagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, ReadOnlySpan<byte>.Empty);
        }

        var payload = new byte[1 + NonceSize + TagSize + ciphertext.Length];
        payload[0] = PayloadVersion;
        Buffer.BlockCopy(nonce, 0, payload, 1, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, 1 + NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, 1 + NonceSize + TagSize, ciphertext.Length);

        return Convert.ToBase64String(payload);
    }

    public static string DecryptSharePayload(string base64Payload)
    {
        if (string.IsNullOrWhiteSpace(base64Payload))
        {
            throw new ArgumentException("Share payload is empty.", nameof(base64Payload));
        }

        var payload = Convert.FromBase64String(base64Payload);
        if (payload.Length < 1 + NonceSize + TagSize)
        {
            throw new InvalidOperationException("Share payload is invalid or corrupted.");
        }

        var version = payload[0];
        if (version != PayloadVersion)
        {
            throw new InvalidOperationException($"Unsupported share payload version: {version}.");
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertext = new byte[payload.Length - 1 - NonceSize - TagSize];

        Buffer.BlockCopy(payload, 1, nonce, 0, NonceSize);
        Buffer.BlockCopy(payload, 1 + NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(payload, 1 + NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(ShareKey, tagSizeInBytes: TagSize))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext, ReadOnlySpan<byte>.Empty);
        }

        return Encoding.UTF8.GetString(plaintext);
    }
}
