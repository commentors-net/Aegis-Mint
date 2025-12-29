using System;
using System.Security.Cryptography;
using System.Text;

namespace AegisMint.Core.Security;

/// <summary>
/// Provides HMAC-SHA256 signature generation and verification for API authentication.
/// </summary>
public static class HmacSignature
{
    /// <summary>
    /// Generates a random secret key for HMAC signing (32 bytes, base64 encoded).
    /// </summary>
    public static string GenerateSecretKey()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Computes HMAC-SHA256 signature for the given message using the secret key.
    /// </summary>
    /// <param name="message">The message to sign</param>
    /// <param name="secretKey">Base64-encoded secret key</param>
    /// <returns>Base64-encoded signature</returns>
    public static string ComputeSignature(string message, string secretKey)
    {
        if (string.IsNullOrEmpty(message))
            throw new ArgumentNullException(nameof(message));
        if (string.IsNullOrEmpty(secretKey))
            throw new ArgumentNullException(nameof(secretKey));

        var keyBytes = Convert.FromBase64String(secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Verifies that the provided signature matches the expected signature for the message.
    /// </summary>
    /// <param name="message">The original message</param>
    /// <param name="signature">The signature to verify</param>
    /// <param name="secretKey">Base64-encoded secret key</param>
    /// <returns>True if signature is valid, false otherwise</returns>
    public static bool VerifySignature(string message, string signature, string secretKey)
    {
        try
        {
            var expectedSignature = ComputeSignature(message, secretKey);
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(expectedSignature),
                Convert.FromBase64String(signature)
            );
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a signature payload for API requests.
    /// Format: {desktopAppId}:{timestamp}:{requestBody}
    /// </summary>
    /// <param name="desktopAppId">Desktop application ID</param>
    /// <param name="timestamp">Unix timestamp in seconds</param>
    /// <param name="requestBody">JSON request body (empty string if no body)</param>
    /// <returns>Message string to sign</returns>
    public static string CreateSignatureMessage(string desktopAppId, long timestamp, string requestBody = "")
    {
        return $"{desktopAppId}:{timestamp}:{requestBody}";
    }

    /// <summary>
    /// Gets current Unix timestamp in seconds.
    /// </summary>
    public static long GetUnixTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
