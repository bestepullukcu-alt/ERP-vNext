using Diten.ProcurementService.Application.Common;

namespace Diten.ProcurementService.Api.Tests.InvoiceMatch;

/// <summary>
/// Test double for the TOLERANCE POLICY (MOD-0143 ASSUMPTION-P2P-01) seam. Configurable qty/price/amount thresholds
/// let tests drive BOTH sides of the policy-driven decision — the vacuity control (K3): a zero-tolerance profile
/// turns any variance into an Exception, while a wide-tolerance profile turns the SAME variance into
/// MatchedWithinTolerance. This proves runThreeWayMatch reads its thresholds from the seam and bakes NO tolerance
/// number of its own. The production default (ZeroToleranceMatchPolicy) is the zero-threshold case.
/// </summary>
public sealed class FakeMatchTolerancePolicy : IMatchTolerancePolicy
{
    private readonly decimal _qty;
    private readonly decimal _price;
    private readonly decimal _amount;

    public FakeMatchTolerancePolicy(decimal qty = 0m, decimal price = 0m, decimal amount = 0m)
    {
        _qty = qty;
        _price = price;
        _amount = amount;
    }

    /// <summary>Records the last resolved profileId so tests can assert the seam (not the handler) supplied it.</summary>
    public string? LastRequestedProfileId { get; private set; }

    public Task<MatchToleranceProfile> ResolveAsync(string? toleranceProfileId, CancellationToken cancellationToken = default)
    {
        LastRequestedProfileId = toleranceProfileId;
        var profileId = string.IsNullOrWhiteSpace(toleranceProfileId) ? "DEFAULT" : toleranceProfileId!.Trim();
        return Task.FromResult(new MatchToleranceProfile(profileId, _qty, _price, _amount));
    }
}
