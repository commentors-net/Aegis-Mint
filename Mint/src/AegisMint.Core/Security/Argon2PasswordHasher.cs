using System;
using System.Security.Cryptography;
using System.Text;
using Isopoh.Cryptography.Argon2;

namespace AegisMint.Core.Security;

public class Argon2PasswordHasher
{
    private const int DefaultSaltSize = 16;
    private const int DefaultMemoryCostKb = 1024 * 64; // 64 MB
    private const int DefaultIterations = 4;
    private const int DefaultParallelism = 4;

    public string HashPassword(string password, byte[]? salt = null)
    {
        salt ??= RandomNumberGenerator.GetBytes(DefaultSaltSize);
        var config = BuildConfig(password, salt);
        return Argon2.Hash(config);
    }

    public bool Verify(string password, string encodedHash)
    {
        // Argon2.Verify will parse salt/parameters from encoded hash.
        return Argon2.Verify(encodedHash, password);
    }

    private static Argon2Config BuildConfig(string password, byte[] salt)
    {
        return new Argon2Config
        {
            Type = Argon2Type.DataIndependentAddressing,
            Version = Argon2Version.Nineteen,
            TimeCost = DefaultIterations,
            MemoryCost = DefaultMemoryCostKb,
            Lanes = DefaultParallelism,
            Threads = DefaultParallelism,
            Salt = salt,
            Password = Encoding.UTF8.GetBytes(password)
        };
    }
}
