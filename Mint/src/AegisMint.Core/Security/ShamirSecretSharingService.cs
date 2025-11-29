using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using AegisMint.Core.Models;

namespace AegisMint.Core.Security;

/// <summary>
/// Shamir Secret Sharing over GF(256) with per-byte polynomials.
/// </summary>
public class ShamirSecretSharingService
{
    private const int FieldSize = 256;
    private const byte Generator = 0x03;

    private readonly byte[] _exp = new byte[FieldSize * 2];
    private readonly byte[] _log = new byte[FieldSize];

    public ShamirSecretSharingService()
    {
        BuildFieldTables();
    }

    public IReadOnlyCollection<ShamirShare> Split(ReadOnlySpan<byte> secret, int threshold, int shareCount)
    {
        ValidateParameters(secret.Length, threshold, shareCount);

        var shares = new List<ShamirShare>(shareCount);
        var random = RandomNumberGenerator.Create();

        var coefficients = new byte[threshold][];
        coefficients[0] = secret.ToArray(); // constant term is the secret bytes

        for (var degree = 1; degree < threshold; degree++)
        {
            var coeff = new byte[secret.Length];
            random.GetBytes(coeff);
            coefficients[degree] = coeff;
        }

        for (byte shareId = 1; shareId <= shareCount; shareId++)
        {
            var yBytes = new byte[secret.Length];
            for (int i = 0; i < secret.Length; i++)
            {
                var value = coefficients[0][i];
                for (int degree = 1; degree < threshold; degree++)
                {
                    var term = GfMul(coefficients[degree][i], GfPow(shareId, (byte)degree));
                    value = (byte)(value ^ term);
                }
                yBytes[i] = value;
            }

            shares.Add(new ShamirShare(shareId, Convert.ToBase64String(yBytes)));
        }

        return shares;
    }

    public byte[] Combine(IReadOnlyCollection<ShamirShare> shares, int threshold)
    {
        if (shares is null || shares.Count < threshold)
        {
            throw new InvalidOperationException($"At least {threshold} shares are required.");
        }

        var ordered = shares.Take(threshold).ToArray();
        var shareLength = Convert.FromBase64String(ordered[0].Value).Length;
        var secret = new byte[shareLength];

        for (int byteIndex = 0; byteIndex < shareLength; byteIndex++)
        {
            byte value = 0;

            for (int j = 0; j < threshold; j++)
            {
                var shareJ = ordered[j];
                var yJ = Convert.FromBase64String(shareJ.Value)[byteIndex];
                var xJ = shareJ.Id;

                byte numerator = 1;
                byte denominator = 1;

                for (int m = 0; m < threshold; m++)
                {
                    if (m == j) continue;
                    var xM = ordered[m].Id;
                    numerator = GfMul(numerator, xM);
                    denominator = GfMul(denominator, (byte)(xJ ^ xM)); // subtraction == XOR in GF(256)
                }

                var lagrange = GfMul(numerator, GfInverse(denominator));
                value = (byte)(value ^ GfMul(yJ, lagrange));
            }

            secret[byteIndex] = value;
        }

        return secret;
    }

    private static void ValidateParameters(int secretLength, int threshold, int shareCount)
    {
        if (secretLength <= 0)
        {
            throw new ArgumentException("Secret must not be empty.", nameof(secretLength));
        }

        if (threshold < 2)
        {
            throw new ArgumentException("Threshold must be at least 2.", nameof(threshold));
        }

        if (shareCount < threshold)
        {
            throw new ArgumentException("Share count must be >= threshold.", nameof(shareCount));
        }

        if (shareCount > 255)
        {
            throw new ArgumentException("Share count cannot exceed 255 (GF(256) limit).", nameof(shareCount));
        }
    }

    private void BuildFieldTables()
    {
        byte value = 1;
        for (int i = 0; i < FieldSize - 1; i++)
        {
            _exp[i] = value;
            _log[value] = (byte)i;
            value = GfMulNoTables(value, Generator);
        }

        for (int i = FieldSize - 1; i < _exp.Length; i++)
        {
            _exp[i] = _exp[i - (FieldSize - 1)];
        }
    }

    private byte GfMul(byte a, byte b)
    {
        if (a == 0 || b == 0) return 0;
        var logSum = _log[a] + _log[b];
        return _exp[logSum];
    }

    private byte GfPow(byte a, byte power)
    {
        if (power == 0) return 1;
        if (a == 0) return 0;
        var logVal = _log[a] * power;
        return _exp[logVal];
    }

    private byte GfInverse(byte a)
    {
        if (a == 0) throw new DivideByZeroException("Cannot invert zero in GF(256).");
        var logVal = 255 - _log[a];
        return _exp[logVal];
    }

    private static byte GfMulNoTables(byte a, byte b)
    {
        byte result = 0;
        byte tempA = a;
        byte tempB = b;

        for (int i = 0; i < 8; i++)
        {
            if ((tempB & 1) != 0)
            {
                result ^= tempA;
            }

            var carry = (tempA & 0x80) != 0;
            tempA <<= 1;
            if (carry)
            {
                tempA ^= 0x1b; // AES polynomial (x^8 + x^4 + x^3 + x + 1)
            }
            tempB >>= 1;
        }

        return result;
    }
}
