using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using AegisMint.Core.Models;

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

    // Token Transfer Operations

    public long SaveTokenTransfer(Models.TokenTransfer transfer)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO token_transfers (
                network, contract_address, from_address, to_address, 
                amount, memo, transaction_hash, status, error_message, 
                created_at_utc, completed_at_utc
            )
            VALUES (
                $network, $contract_address, $from_address, $to_address,
                $amount, $memo, $transaction_hash, $status, $error_message,
                $created_at_utc, $completed_at_utc
            );
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$network", transfer.Network);
        command.Parameters.AddWithValue("$contract_address", transfer.ContractAddress);
        command.Parameters.AddWithValue("$from_address", transfer.FromAddress);
        command.Parameters.AddWithValue("$to_address", transfer.ToAddress);
        command.Parameters.AddWithValue("$amount", transfer.Amount);
        command.Parameters.AddWithValue("$memo", (object?)transfer.Memo ?? DBNull.Value);
        command.Parameters.AddWithValue("$transaction_hash", (object?)transfer.TransactionHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", transfer.Status);
        command.Parameters.AddWithValue("$error_message", (object?)transfer.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$created_at_utc", transfer.CreatedAtUtc.ToString("o"));
        command.Parameters.AddWithValue("$completed_at_utc", 
            transfer.CompletedAtUtc.HasValue ? transfer.CompletedAtUtc.Value.ToString("o") : DBNull.Value);

        var result = command.ExecuteScalar();
        return result != null ? Convert.ToInt64(result) : 0;
    }

    public void UpdateTokenTransferStatus(long id, string status, string? txHash = null, string? errorMessage = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE token_transfers
            SET status = $status,
                transaction_hash = COALESCE($transaction_hash, transaction_hash),
                error_message = $error_message,
                completed_at_utc = $completed_at_utc
            WHERE id = $id;
            """;

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$transaction_hash", (object?)txHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$error_message", (object?)errorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$completed_at_utc", DateTimeOffset.UtcNow.ToString("o"));

        command.ExecuteNonQuery();
    }

    public Models.TokenTransfer? GetTokenTransfer(long id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, network, contract_address, from_address, to_address, 
                   amount, memo, transaction_hash, status, error_message, 
                   created_at_utc, completed_at_utc
            FROM token_transfers
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new Models.TokenTransfer
        {
            Id = reader.GetInt64(0),
            Network = reader.GetString(1),
            ContractAddress = reader.GetString(2),
            FromAddress = reader.GetString(3),
            ToAddress = reader.GetString(4),
            Amount = reader.GetString(5),
            Memo = reader.IsDBNull(6) ? null : reader.GetString(6),
            TransactionHash = reader.IsDBNull(7) ? null : reader.GetString(7),
            Status = reader.GetString(8),
            ErrorMessage = reader.IsDBNull(9) ? null : reader.GetString(9),
            CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(10)),
            CompletedAtUtc = reader.IsDBNull(11) ? null : DateTimeOffset.Parse(reader.GetString(11))
        };
    }

    // Freeze Operations

    public long SaveFreezeOperation(Models.FreezeOperation operation)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO freeze_operations (
                network, contract_address, target_address, is_frozen, 
                reason, transaction_hash, status, error_message, 
                created_at_utc, completed_at_utc
            )
            VALUES (
                $network, $contract_address, $target_address, $is_frozen,
                $reason, $transaction_hash, $status, $error_message,
                $created_at_utc, $completed_at_utc
            );
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$network", operation.Network);
        command.Parameters.AddWithValue("$contract_address", operation.ContractAddress);
        command.Parameters.AddWithValue("$target_address", operation.TargetAddress);
        command.Parameters.AddWithValue("$is_frozen", operation.IsFrozen ? 1 : 0);
        command.Parameters.AddWithValue("$reason", (object?)operation.Reason ?? DBNull.Value);
        command.Parameters.AddWithValue("$transaction_hash", (object?)operation.TransactionHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", operation.Status);
        command.Parameters.AddWithValue("$error_message", (object?)operation.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$created_at_utc", operation.CreatedAtUtc.ToString("o"));
        command.Parameters.AddWithValue("$completed_at_utc", 
            operation.CompletedAtUtc.HasValue ? operation.CompletedAtUtc.Value.ToString("o") : DBNull.Value);

        var result = command.ExecuteScalar();
        return result != null ? Convert.ToInt64(result) : 0;
    }

    public void UpdateFreezeOperationStatus(long id, string status, string? txHash = null, string? errorMessage = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE freeze_operations
            SET status = $status,
                transaction_hash = COALESCE($transaction_hash, transaction_hash),
                error_message = $error_message,
                completed_at_utc = $completed_at_utc
            WHERE id = $id;
            """;

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$transaction_hash", (object?)txHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$error_message", (object?)errorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$completed_at_utc", DateTimeOffset.UtcNow.ToString("o"));

        command.ExecuteNonQuery();
    }

    public IReadOnlyList<FreezeOperation> GetFreezeOperations(string network, int limit = 100)
    {
        var list = new List<FreezeOperation>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, network, contract_address, target_address, is_frozen, reason,
                   transaction_hash, status, error_message, created_at_utc, completed_at_utc
            FROM freeze_operations
            WHERE network = $network
            ORDER BY datetime(created_at_utc) DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$network", network);
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new FreezeOperation
            {
                Id = reader.GetInt64(0),
                Network = reader.GetString(1),
                ContractAddress = reader.GetString(2),
                TargetAddress = reader.GetString(3),
                IsFrozen = reader.GetInt64(4) == 1,
                Reason = reader.IsDBNull(5) ? null : reader.GetString(5),
                TransactionHash = reader.IsDBNull(6) ? null : reader.GetString(6),
                Status = reader.GetString(7),
                ErrorMessage = reader.IsDBNull(8) ? null : reader.GetString(8),
                CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(9)),
                CompletedAtUtc = reader.IsDBNull(10) ? null : DateTimeOffset.Parse(reader.GetString(10))
            });
        }

        return list;
    }

    // Token Retrieval Operations

    public long SaveTokenRetrieval(Models.TokenRetrieval retrieval)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO token_retrievals (
                network, contract_address, from_address, to_address, 
                amount, reason, transaction_hash, status, error_message, 
                created_at_utc, completed_at_utc
            )
            VALUES (
                $network, $contract_address, $from_address, $to_address,
                $amount, $reason, $transaction_hash, $status, $error_message,
                $created_at_utc, $completed_at_utc
            );
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$network", retrieval.Network);
        command.Parameters.AddWithValue("$contract_address", retrieval.ContractAddress);
        command.Parameters.AddWithValue("$from_address", retrieval.FromAddress);
        command.Parameters.AddWithValue("$to_address", retrieval.ToAddress);
        command.Parameters.AddWithValue("$amount", retrieval.Amount);
        command.Parameters.AddWithValue("$reason", (object?)retrieval.Reason ?? DBNull.Value);
        command.Parameters.AddWithValue("$transaction_hash", (object?)retrieval.TransactionHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", retrieval.Status);
        command.Parameters.AddWithValue("$error_message", (object?)retrieval.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$created_at_utc", retrieval.CreatedAtUtc.ToString("o"));
        command.Parameters.AddWithValue("$completed_at_utc", 
            retrieval.CompletedAtUtc.HasValue ? retrieval.CompletedAtUtc.Value.ToString("o") : DBNull.Value);

        var result = command.ExecuteScalar();
        return result != null ? Convert.ToInt64(result) : 0;
    }

    public void UpdateTokenRetrievalStatus(long id, string status, string? wipeTxHash = null, string? reclaimTxHash = null, string? errorMessage = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE token_retrievals
            SET status = $status,
                wipe_transaction_hash = COALESCE($wipe_transaction_hash, wipe_transaction_hash),
                reclaim_transaction_hash = COALESCE($reclaim_transaction_hash, reclaim_transaction_hash),
                transaction_hash = COALESCE($wipe_transaction_hash, wipe_transaction_hash),
                error_message = $error_message,
                completed_at_utc = $completed_at_utc
            WHERE id = $id;
            """;

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$wipe_transaction_hash", (object?)wipeTxHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$reclaim_transaction_hash", (object?)reclaimTxHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$error_message", (object?)errorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$completed_at_utc", DateTimeOffset.UtcNow.ToString("o"));

        command.ExecuteNonQuery();
    }

    // Pause Operations

    public long SavePauseOperation(Models.PauseOperation operation)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pause_operations (
                network, contract_address, is_paused, 
                transaction_hash, status, error_message, 
                created_at_utc, completed_at_utc
            )
            VALUES (
                $network, $contract_address, $is_paused,
                $transaction_hash, $status, $error_message,
                $created_at_utc, $completed_at_utc
            );
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$network", operation.Network);
        command.Parameters.AddWithValue("$contract_address", operation.ContractAddress);
        command.Parameters.AddWithValue("$is_paused", operation.IsPaused ? 1 : 0);
        command.Parameters.AddWithValue("$transaction_hash", (object?)operation.TransactionHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", operation.Status);
        command.Parameters.AddWithValue("$error_message", (object?)operation.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$created_at_utc", operation.CreatedAtUtc.ToString("o"));
        command.Parameters.AddWithValue("$completed_at_utc", 
            operation.CompletedAtUtc.HasValue ? operation.CompletedAtUtc.Value.ToString("o") : DBNull.Value);

        var result = command.ExecuteScalar();
        return result != null ? Convert.ToInt64(result) : 0;
    }

    public void UpdatePauseOperationStatus(long id, string status, string? txHash = null, string? errorMessage = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE pause_operations
            SET status = $status,
                transaction_hash = COALESCE($transaction_hash, transaction_hash),
                error_message = $error_message,
                completed_at_utc = $completed_at_utc
            WHERE id = $id;
            """;

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$transaction_hash", (object?)txHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$error_message", (object?)errorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$completed_at_utc", DateTimeOffset.UtcNow.ToString("o"));

        command.ExecuteNonQuery();
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

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS token_transfers (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                network TEXT NOT NULL,
                contract_address TEXT NOT NULL,
                from_address TEXT NOT NULL,
                to_address TEXT NOT NULL,
                amount TEXT NOT NULL,
                memo TEXT,
                transaction_hash TEXT,
                status TEXT NOT NULL DEFAULT 'pending',
                error_message TEXT,
                created_at_utc TEXT NOT NULL,
                completed_at_utc TEXT
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS freeze_operations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                network TEXT NOT NULL,
                contract_address TEXT NOT NULL,
                target_address TEXT NOT NULL,
                is_frozen INTEGER NOT NULL,
                reason TEXT,
                transaction_hash TEXT,
                status TEXT NOT NULL DEFAULT 'pending',
                error_message TEXT,
                created_at_utc TEXT NOT NULL,
                completed_at_utc TEXT
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS token_retrievals (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                network TEXT NOT NULL,
                contract_address TEXT NOT NULL,
                from_address TEXT NOT NULL,
                to_address TEXT NOT NULL,
                amount TEXT NOT NULL,
                reason TEXT,
                transaction_hash TEXT,
                wipe_transaction_hash TEXT,
                reclaim_transaction_hash TEXT,
                status TEXT NOT NULL DEFAULT 'pending',
                error_message TEXT,
                created_at_utc TEXT NOT NULL,
                completed_at_utc TEXT
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS pause_operations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                network TEXT NOT NULL,
                contract_address TEXT NOT NULL,
                is_paused INTEGER NOT NULL,
                transaction_hash TEXT,
                status TEXT NOT NULL DEFAULT 'pending',
                error_message TEXT,
                created_at_utc TEXT NOT NULL,
                completed_at_utc TEXT
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE INDEX IF NOT EXISTS idx_token_transfers_network ON token_transfers(network);
            """);

        ExecuteNonQuery(connection, """
            CREATE INDEX IF NOT EXISTS idx_freeze_operations_network ON freeze_operations(network);
            """);

        ExecuteNonQuery(connection, """
            CREATE INDEX IF NOT EXISTS idx_token_retrievals_network ON token_retrievals(network);
            """);

        ExecuteNonQuery(connection, """
            CREATE INDEX IF NOT EXISTS idx_pause_operations_network ON pause_operations(network);
            """);

        EnsureColumnExists(connection, "token_retrievals", "wipe_transaction_hash",
            "ALTER TABLE token_retrievals ADD COLUMN wipe_transaction_hash TEXT;");
        EnsureColumnExists(connection, "token_retrievals", "reclaim_transaction_hash",
            "ALTER TABLE token_retrievals ADD COLUMN reclaim_transaction_hash TEXT;");
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void EnsureColumnExists(SqliteConnection connection, string table, string column, string alterSql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        ExecuteNonQuery(connection, alterSql);
    }
}
