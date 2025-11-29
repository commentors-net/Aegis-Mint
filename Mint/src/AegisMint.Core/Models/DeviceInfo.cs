namespace AegisMint.Core.Models;

public record DeviceInfo(
    string DeviceId,
    int ShareCount,
    int RecoveryThreshold,
    int GovernanceQuorum,
    int UnlockWindowMinutes,
    string ConfigurationVersion);
