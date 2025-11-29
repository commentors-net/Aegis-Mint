using System;
using System.Security.Cryptography;

namespace AegisMint.Core.Security;

public class AesGcmEnvelope
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;

    public static AesGcmEnvelope Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, ReadOnlySpan<byte>.Empty);

        return new AesGcmEnvelope
        {
            Nonce = Convert.ToBase64String(nonce),
            Ciphertext = Convert.ToBase64String(ciphertext),
            Tag = Convert.ToBase64String(tag)
        };
    }

    public byte[] Decrypt(ReadOnlySpan<byte> key)
    {
        var nonce = Convert.FromBase64String(Nonce);
        var ciphertext = Convert.FromBase64String(Ciphertext);
        var tag = Convert.FromBase64String(Tag);

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, ReadOnlySpan<byte>.Empty);
        return plaintext;
    }
}
