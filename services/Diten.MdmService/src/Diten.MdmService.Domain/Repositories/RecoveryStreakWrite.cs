namespace Diten.MdmService.Domain.Repositories;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX4 item 3: how a write changes an operation's retry streak — in the store, atomically
/// ($inc), never from the snapshot read before the claim (two actors would overwrite each other's count).
/// </summary>
public enum RecoveryStreakWrite
{
    /// <summary>The attempt did not end in a retry: the streak is over.</summary>
    Reset = 0,
    /// <summary>A worker's attempt ended in a retry: one more in a row.</summary>
    Increment = 1,
    /// <summary>An interactive attempt ended in a retry: the worker's streak is left as it is (FIX4 item 2).</summary>
    Keep = 2
}
