using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace AegisMint.Core.Services;

public static class EvmRevertReason
{
    // Error(string)
    private static readonly byte[] ErrorSelector = { 0x08, 0xC3, 0x79, 0xA0 };
    // Panic(uint256)
    private static readonly byte[] PanicSelector = { 0x4E, 0x48, 0x7B, 0x71 };

    public static string? TryDecodeFromJsonRpcErrorData(JsonElement? errorData, string? errorMessageFallback = null)
    {
        if (errorData.HasValue)
        {
            var decoded = TryDecodeFromJsonElement(errorData.Value);
            if (!string.IsNullOrWhiteSpace(decoded))
            {
                return decoded;
            }
        }

        return TryDecodeFromErrorMessage(errorMessageFallback);
    }

    private static string? TryDecodeFromJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return TryDecodeFromHex(element.GetString());

            case JsonValueKind.Object:
                // Common shapes:
                // - { "data": "0x..." }
                // - { "data": { ... } }
                // - { "result": "0x..." }
                if (element.TryGetProperty("data", out var dataElement))
                {
                    return TryDecodeFromJsonElement(dataElement);
                }

                if (element.TryGetProperty("result", out var resultElement))
                {
                    return TryDecodeFromJsonElement(resultElement);
                }

                if (element.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
                {
                    return TryDecodeFromErrorMessage(messageElement.GetString());
                }

                return null;

            case JsonValueKind.Array:
                // Some providers return an array of errors; try each.
                foreach (var child in element.EnumerateArray())
                {
                    var decoded = TryDecodeFromJsonElement(child);
                    if (!string.IsNullOrWhiteSpace(decoded))
                    {
                        return decoded;
                    }
                }
                return null;

            default:
                return null;
        }
    }

    private static string? TryDecodeFromErrorMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        // e.g. "execution reverted: Ownable: caller is not the owner"
        const string marker = "execution reverted:";
        var idx = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return message[(idx + marker.Length)..].Trim();
        }

        return null;
    }

    public static string? TryDecodeFromHex(string? dataHex)
    {
        if (string.IsNullOrWhiteSpace(dataHex))
        {
            return null;
        }

        dataHex = dataHex.Trim();
        if (dataHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            dataHex = dataHex[2..];
        }

        if (dataHex.Length < 8 || (dataHex.Length % 2 != 0))
        {
            return null;
        }

        if (!TryHexToBytes(dataHex, out var bytes) || bytes.Length < 4)
        {
            return null;
        }

        if (StartsWith(bytes, ErrorSelector))
        {
            return DecodeErrorString(bytes);
        }

        if (StartsWith(bytes, PanicSelector))
        {
            return DecodePanic(bytes);
        }

        return null;
    }

    private static string? DecodeErrorString(byte[] bytes)
    {
        // ABI encoding:
        // 0x08c379a0 + offset(32) + length(32) + string bytes (padded)
        if (bytes.Length < 4 + 32 + 32)
        {
            return null;
        }

        try
        {
            var offset = (int)ReadUInt256(bytes, 4);
            var lenPos = 4 + offset;
            if (lenPos + 32 > bytes.Length)
            {
                return null;
            }

            var strLen = (int)ReadUInt256(bytes, lenPos);
            var strPos = lenPos + 32;
            if (strLen < 0 || strPos + strLen > bytes.Length)
            {
                return null;
            }

            return Encoding.UTF8.GetString(bytes, strPos, strLen);
        }
        catch
        {
            return null;
        }
    }

    private static string? DecodePanic(byte[] bytes)
    {
        // ABI encoding:
        // 0x4e487b71 + code(32)
        if (bytes.Length < 4 + 32)
        {
            return "Panic";
        }

        var code = ReadUInt256(bytes, 4);
        var codeHex = $"0x{code.ToString("x", CultureInfo.InvariantCulture)}";

        if (code > int.MaxValue)
        {
            return $"Panic {codeHex}";
        }

        var codeInt = (int)code;
        return codeInt switch
        {
            0x01 => $"Panic {codeHex}: assert(false)",
            0x11 => $"Panic {codeHex}: arithmetic overflow/underflow",
            0x12 => $"Panic {codeHex}: division by zero",
            0x21 => $"Panic {codeHex}: invalid enum conversion",
            0x22 => $"Panic {codeHex}: storage byte array out of bounds",
            0x31 => $"Panic {codeHex}: pop on empty array",
            0x32 => $"Panic {codeHex}: array index out of bounds",
            0x41 => $"Panic {codeHex}: memory allocation overflow",
            0x51 => $"Panic {codeHex}: zero-initialized function pointer",
            _ => $"Panic {codeHex}"
        };
    }

    private static bool StartsWith(byte[] bytes, byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (bytes[i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }

    private static BigInteger ReadUInt256(byte[] bytes, int offset)
    {
        // ABI uint256 is big-endian 32 bytes.
        var span = bytes.AsSpan(offset, 32);
        var tmp = new byte[33]; // extra 0 to keep positive in little-endian BigInteger ctor

        for (var i = 0; i < 32; i++)
        {
            tmp[i] = span[31 - i];
        }

        return new BigInteger(tmp);
    }

    private static bool TryHexToBytes(string hex, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();

        try
        {
            bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return true;
        }
        catch
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }
}
