namespace AegisMint.Core.Configuration;

/// <summary>
/// Configuration surface for the Mint service and supporting tools.
/// </summary>
public class MintOptions
{
    /// <summary>
    /// Directory used for on-disk secrets and metadata (relative paths resolved from the service base directory).
    /// </summary>
    public string DataDirectory { get; set; } = "data";

    /// <summary>
    /// Number of user-distributed shares (M).
    /// </summary>
    public int ShareCount { get; set; } = 5;

    /// <summary>
    /// Threshold of shares required to reconstruct (N).
    /// </summary>
    public int RecoveryThreshold { get; set; } = 3;

    /// <summary>
    /// Approval threshold for governance unlock (N-of-M governors).
    /// </summary>
    public int GovernanceQuorum { get; set; } = 2;

    /// <summary>
    /// Unlock window in minutes (default 15 as per requirements).
    /// </summary>
    public int UnlockWindowMinutes { get; set; } = 15;

    /// <summary>
    /// Optional externally supplied device identifier; if empty a GUID will be generated on first run.
    /// </summary>
    public string? DeviceId { get; set; }
}
