using Diten.AuthService.Application.Features.Users.Services;

namespace Diten.AuthService.Application.Tests.Testing;

/// <summary>
/// WP-USERS-ROLES-SMALL-02 — a fixed answer of the last-steward rule, for handler tests whose subject is something else
/// (sessions, versions, audit). The rule itself is tested on its own (<c>RecoveryCapabilityGuardTests</c>) and through
/// each handler with the REAL rule over fakes (<c>LastStewardPathsTests</c>); this double never stands in for those.
/// </summary>
internal sealed class RecoveryGuardForTests(bool refuse) : IRecoveryCapabilityGuard
{
    public List<RecoveryCapabilityChange> Asked { get; } = [];

    /// <summary>The rule lets every change through.</summary>
    public static RecoveryGuardForTests Permits() => new(false);

    /// <summary>The rule refuses every change.</summary>
    public static RecoveryGuardForTests Refuses() => new(true);

    public Task<bool> WouldLeaveNoActiveHolderAsync(Guid tenantId, RecoveryCapabilityChange change, CancellationToken ct)
    {
        Asked.Add(change);
        return Task.FromResult(refuse);
    }
}
