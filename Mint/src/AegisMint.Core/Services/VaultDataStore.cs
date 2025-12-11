using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace AegisMint.Core.Services;

/// <summary>
/// SQLite + SQLCipher backed store for vault secrets and deployment metadata (hex key only).
/// </summary>
internal sealed class VaultDataStore
{
    private readonly string _dbPath;
    private readonly string _keyPath;
    private readonly object _initLock = new();
    private bool _initialized;
    private string? _hexKey;

    public VaultDataStore()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AegisMint",
            "Data");

        Directory.CreateDirectory(baseDir);

        _dbPath = Path.Combine(baseDir, "vault.db");
        _keyPath = Path.Combine(baseDir, "vault.key");
    }

    public string DatabasePath => _dbPath;

    public void SaveEncryptedMnemonic(string name, string encryptedBase64)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO mnemonics (name, encrypted)
            VALUES ($name, $encrypted)
            ON CONFLICT(name) DO UPDATE SET encrypted = excluded.encrypted;
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$encrypted", encryptedBase64);
        command.ExecuteNonQuery();
    }

    public string? GetEncryptedMnemonic(string name)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT encrypted FROM mnemonics WHERE name = $name LIMIT 1;
            """;
        command.Parameters.AddWithValue("$name", name);
        return command.ExecuteScalar() as string;
    }

    public void RemoveMnemonic(string name)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM mnemonics WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name);
        command.ExecuteNonQuery();
    }

    public void SaveContractDeployment(string network, string address)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO contracts (network, address)
            VALUES ($network, $address)
            ON CONFLICT(network) DO UPDATE SET address = excluded.address;
            """;
        command.Parameters.AddWithValue("$network", network);
        command.Parameters.AddWithValue("$address", address);
        command.ExecuteNonQuery();
    }

    public string? GetContractDeployment(string network)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT address FROM contracts WHERE network = $network LIMIT 1;
            """;
        command.Parameters.AddWithValue("$network", network);
        return command.ExecuteScalar() as string;
    }

    public void SaveDeploymentSnapshot(DeploymentSnapshot snapshot)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO snapshots (
                network,
                contract_address,
                token_name,
                token_supply,
                token_decimals,
                gov_shares,
                gov_threshold,
                treasury_address,
                treasury_eth,
                treasury_tokens,
                created_at_utc
            )
            VALUES (
                $network,
                $contract_address,
                $token_name,
                $token_supply,
                $token_decimals,
                $gov_shares,
                $gov_threshold,
                $treasury_address,
                $treasury_eth,
                $treasury_tokens,
                $created_at_utc
            )
            ON CONFLICT(network) DO UPDATE SET
                contract_address = excluded.contract_address,
                token_name = excluded.token_name,
                token_supply = excluded.token_supply,
                token_decimals = excluded.token_decimals,
                gov_shares = excluded.gov_shares,
                gov_threshold = excluded.gov_threshold,
                treasury_address = excluded.treasury_address,
                treasury_eth = excluded.treasury_eth,
                treasury_tokens = excluded.treasury_tokens,
                created_at_utc = excluded.created_at_utc;
            """;

        command.Parameters.AddWithValue("$network", snapshot.Network);
        command.Parameters.AddWithValue("$contract_address", snapshot.ContractAddress);
        command.Parameters.AddWithValue("$token_name", snapshot.TokenName);
        command.Parameters.AddWithValue("$token_supply", snapshot.TokenSupply);
        command.Parameters.AddWithValue("$token_decimals", snapshot.TokenDecimals);
        command.Parameters.AddWithValue("$gov_shares", snapshot.GovShares);
        command.Parameters.AddWithValue("$gov_threshold", snapshot.GovThreshold);
        command.Parameters.AddWithValue("$treasury_address", snapshot.TreasuryAddress);
        command.Parameters.AddWithValue("$treasury_eth", snapshot.TreasuryEth);
        command.Parameters.AddWithValue("$treasury_tokens", snapshot.TreasuryTokens);
        command.Parameters.AddWithValue("$created_at_utc", snapshot.CreatedAtUtc.ToString("o"));

        command.ExecuteNonQuery();
    }

    public DeploymentSnapshot? GetDeploymentSnapshot(string network)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                network,
                contract_address,
                token_name,
                token_supply,
                token_decimals,
                gov_shares,
                gov_threshold,
                treasury_address,
                treasury_eth,
                treasury_tokens,
                created_at_utc
            FROM snapshots
            WHERE network = $network
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$network", network);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var createdAtRaw = reader.GetString(10);
        var createdAt = DateTimeOffset.Parse(createdAtRaw, null, System.Globalization.DateTimeStyles.RoundtripKind);

        return new DeploymentSnapshot(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9),
            createdAt);
    }

    public void SaveSetting(string key, string value)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings (key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    public string? GetSetting(string key)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT value FROM settings WHERE key = $key LIMIT 1;
            """;
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    public void ClearContracts()
    {
        using var connection = OpenConnection();
        ExecuteNonQuery(connection, "DELETE FROM contracts;");
    }

    public void ClearSnapshots()
    {
        using var connection = OpenConnection();
        ExecuteNonQuery(connection, "DELETE FROM snapshots;");
    }

    public void ClearSettings()
    {
        using var connection = OpenConnection();
        ExecuteNonQuery(connection, "DELETE FROM settings;");
    }

    public void ClearAll()
    {
        using var connection = OpenConnection();
        ExecuteNonQuery(connection, "DELETE FROM mnemonics;");
        ExecuteNonQuery(connection, "DELETE FROM contracts;");
        ExecuteNonQuery(connection, "DELETE FROM snapshots;");
        ExecuteNonQuery(connection, "DELETE FROM settings;");
    }

    private SqliteConnection OpenConnection()
    {
        EnsureInitialized();
        var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        ApplyKey(connection);
        ApplyCipherSettings(connection);
        return connection;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;

            Batteries_V2.Init();
            _hexKey = LoadOrCreateEncryptionKey();

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            ApplyKey(connection);
            ApplyCipherSettings(connection);
            EnsureTables(connection);

            _initialized = true;
        }
    }

    private string LoadOrCreateEncryptionKey()
    {
        if (File.Exists(_keyPath))
        {
            var protectedKey = File.ReadAllBytes(_keyPath);
            var keyBytes = ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.CurrentUser);
            return Convert.ToHexString(keyBytes).ToLowerInvariant();
        }

        var newKey = RandomNumberGenerator.GetBytes(32);
        var protectedBytes = ProtectedData.Protect(newKey, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_keyPath, protectedBytes);
        return Convert.ToHexString(newKey).ToLowerInvariant();
    }

    private void ApplyKey(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA key = \"x'{_hexKey}'\";";
        command.ExecuteNonQuery();
    }

    private static void ApplyCipherSettings(SqliteConnection connection)
    {
        ExecuteNonQuery(connection, "PRAGMA cipher_compatibility = 4;");
        ExecuteNonQuery(connection, "PRAGMA kdf_iter = 256000;");
    }

    private static void EnsureTables(SqliteConnection connection)
    {
        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS mnemonics (
                name TEXT PRIMARY KEY,
                encrypted TEXT NOT NULL
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS contracts (
                network TEXT PRIMARY KEY,
                address TEXT NOT NULL
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS snapshots (
                network TEXT PRIMARY KEY,
                contract_address TEXT NOT NULL,
                token_name TEXT,
                token_supply TEXT,
                token_decimals INTEGER,
                gov_shares INTEGER,
                gov_threshold INTEGER,
                treasury_address TEXT,
                treasury_eth TEXT,
                treasury_tokens TEXT,
                created_at_utc TEXT NOT NULL
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """);
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
