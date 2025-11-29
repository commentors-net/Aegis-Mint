using System;

namespace AegisMint.Service.Governance;

/// <summary>
/// Tracks unlock windows provided by the governance layer.
/// </summary>
public class GovernanceState
{
    private readonly object _gate = new();
    private DateTimeOffset _unlockedUntil = DateTimeOffset.MinValue;

    public bool IsUnlocked
    {
        get
        {
            lock (_gate)
            {
                return DateTimeOffset.UtcNow <= _unlockedUntil;
            }
        }
    }

    public DateTimeOffset Unlock(TimeSpan duration)
    {
        lock (_gate)
        {
            _unlockedUntil = DateTimeOffset.UtcNow.Add(duration);
            return _unlockedUntil;
        }
    }

    public void Lock()
    {
        lock (_gate)
        {
            _unlockedUntil = DateTimeOffset.MinValue;
        }
    }

    public DateTimeOffset? ExpiresAt
    {
        get
        {
            lock (_gate)
            {
                return _unlockedUntil == DateTimeOffset.MinValue ? null : _unlockedUntil;
            }
        }
    }
}
