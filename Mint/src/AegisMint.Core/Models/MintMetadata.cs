using System;

namespace AegisMint.Core.Models;

public class MintMetadata
{
    public string DeviceId { get; set; } = string.Empty;
    public int ShareCount { get; set; }
    public int RecoveryThreshold { get; set; }
    public int GovernanceQuorum { get; set; }
    public int UnlockWindowMinutes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string ConfigurationVersion { get; set; } = "1.0.0";
}
