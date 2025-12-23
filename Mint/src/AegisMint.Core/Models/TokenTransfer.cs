using System;

namespace AegisMint.Core.Models;

/// <summary>
/// Represents a token transfer operation stored in the database.
/// </summary>
public record TokenTransfer
{
    public long Id { get; init; }
    public string Network { get; init; } = string.Empty;
    public string ContractAddress { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string ToAddress { get; init; } = string.Empty;
    public string Amount { get; init; } = string.Empty;
    public string? Memo { get; init; }
    public string? TransactionHash { get; init; }
    public string Status { get; init; } = "pending"; // pending, success, failed
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
}

/// <summary>
/// Represents a freeze/unfreeze operation on an address.
/// </summary>
public record FreezeOperation
{
    public long Id { get; init; }
    public string Network { get; init; } = string.Empty;
    public string ContractAddress { get; init; } = string.Empty;
    public string TargetAddress { get; init; } = string.Empty;
    public bool IsFrozen { get; init; } // true = freeze, false = unfreeze
    public string? Reason { get; init; }
    public string? TransactionHash { get; init; }
    public string Status { get; init; } = "pending"; // pending, success, failed
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
}

/// <summary>
/// Represents a token retrieval operation (pulling tokens from an address back to treasury).
/// </summary>
public record TokenRetrieval
{
    public long Id { get; init; }
    public string Network { get; init; } = string.Empty;
    public string ContractAddress { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string ToAddress { get; init; } = string.Empty;
    public string Amount { get; init; } = string.Empty; // Empty means full balance
    public string? Reason { get; init; }
    public string? TransactionHash { get; init; }
    public string Status { get; init; } = "pending"; // pending, success, failed
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
}

/// <summary>
/// Represents a pause/unpause operation on the token contract.
/// </summary>
public record PauseOperation
{
    public long Id { get; init; }
    public string Network { get; init; } = string.Empty;
    public string ContractAddress { get; init; } = string.Empty;
    public bool IsPaused { get; init; } // true = pause, false = unpause
    public string? TransactionHash { get; init; }
    public string Status { get; init; } = "pending"; // pending, success, failed
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
}

/// <summary>
/// Result of a token control operation.
/// </summary>
public record OperationResult
{
    public bool Success { get; init; }
    public string? TransactionHash { get; init; }
    public string? ErrorMessage { get; init; }
}

