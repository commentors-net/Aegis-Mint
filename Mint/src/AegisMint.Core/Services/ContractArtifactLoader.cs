using System;
using System.IO;
using System.Linq;

namespace AegisMint.Core.Services;

public sealed class ContractArtifactLoader
{
    private readonly string _basePath;

    public ContractArtifactLoader(string? basePath = null)
    {
        _basePath = basePath ?? Path.Combine(AppContext.BaseDirectory, "Resources");
    }

    public ContractArtifacts LoadTokenImplementation() => Load("TokenImplementationV2");

    public ContractArtifacts Load(string contractName)
    {
        var abiPath = Path.Combine(_basePath, $"{contractName}.abi");
        var binPath = Path.Combine(_basePath, $"{contractName}.bin");

        var abi = ReadFileOrNull(abiPath);
        var bytecode = NormalizeHex(ReadFileOrNull(binPath));

        return new ContractArtifacts(contractName, abi, bytecode, abiPath, binPath);
    }

    private static string? ReadFileOrNull(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Strips whitespace, newlines, and an optional 0x prefix so the bytecode is pure hex.
    /// </summary>
    public static string NormalizeHex(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        // Keep only hex digits to avoid signer errors when converting to bytes.
        return new string(trimmed.Where(Uri.IsHexDigit).ToArray());
    }
}

public sealed record ContractArtifacts(string Name, string? Abi, string? Bytecode, string AbiPath, string BinPath)
{
    public bool HasAbi => !string.IsNullOrWhiteSpace(Abi);
    public bool HasBytecode => !string.IsNullOrWhiteSpace(Bytecode);
    public bool IsComplete => HasAbi && HasBytecode;
}
