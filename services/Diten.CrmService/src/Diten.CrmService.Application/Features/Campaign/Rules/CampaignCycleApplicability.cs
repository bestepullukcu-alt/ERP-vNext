using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Campaign.Rules;

/// <summary>
/// MOD-0165 FU09 — which cycle periods a campaign at a given address may bind to, as a PURE function: no repository,
/// no clock, no I/O.
///
/// <para><b>Precedence with fallback, mirroring the cycle-period resolver.</b> The resolver walks
/// business-unit → legal-entity → country → tenant and SKIPS a level the caller did not name. A campaign's scope is
/// discriminated: it names exactly ONE level. Applying the resolver's own rule to that fact gives the applicable set
/// directly — the campaign's own address, plus the tenant-wide fallback that is named at every level:</para>
///
/// <code>
/// campaign scope            applicable period scopes
/// ---------------------     ------------------------------------
/// tenant                    tenant
/// country:TR                country:TR   + tenant
/// legal-entity:X            legal-entity:X + tenant
/// business-unit:alpha       business-unit:alpha + tenant
/// </code>
///
/// <para><b>The axis does not cross, and that is a decision rather than an omission.</b> A business-unit-scoped
/// campaign does NOT see country periods. It never named a country, so there is no datum on the campaign that could
/// answer "does TR cover alpha?". The two ways to invent one were both rejected: the period's
/// <c>BusinessUnitCountryContext</c> is documentation the cycle-period module explicitly excludes from identity, and
/// resolving the unit's country through Territory at pick time would put a cross-module dependency and a new outage
/// mode inside a read. Widening this is a follow-up, not a silent behaviour.</para>
///
/// <para><b>tenant is the only fallback, and it is symmetric.</b> A tenant-wide period is applicable to every
/// campaign, which is what keeps a tenant that has not adopted scoping working exactly as it did. Conversely a
/// tenant-scoped campaign sees ONLY tenant periods: a campaign that claims the whole tenant must not be governed by
/// one business unit's calendar.</para>
/// </summary>
public static class CampaignCycleApplicability
{
    /// <summary>
    /// The (scopeType, scopeRef) addresses a campaign at this scope may bind to, MOST SPECIFIC FIRST — the campaign's
    /// own address, then the tenant fallback. For a tenant-scoped campaign the two coincide and one entry is returned.
    /// <para>Order matters to callers that present the list: the specific address is offered before the fallback, the
    /// same way the resolver prefers it.</para>
    /// </summary>
    public static IReadOnlyList<(string ScopeType, string? ScopeRef)> ApplicableScopes(
        string campaignScopeType, string? campaignScopeRef)
    {
        var scopeType = CampaignScopeTypes.Normalize(campaignScopeType);
        if (!CampaignScopeTypes.IsKnown(scopeType) || scopeType == CampaignScopeTypes.Tenant)
        {
            return new[] { (CampaignScopeTypes.Tenant, (string?)null) };
        }

        return new[] { (scopeType, CampaignScopeRules.Trim(campaignScopeRef)), (CampaignScopeTypes.Tenant, (string?)null) };
    }

    /// <summary>
    /// Is a period at <paramref name="periodScopeType"/>/<paramref name="periodScopeRef"/> applicable to a campaign at
    /// <paramref name="campaignScopeType"/>/<paramref name="campaignScopeRef"/>?
    /// <para>Both sides are compared on the SAME normalisation the addresses are built with, so "TR" and "tr", or
    /// "alpha" and " alpha ", are one address rather than two.</para>
    /// </summary>
    public static bool IsApplicable(
        string campaignScopeType,
        string? campaignScopeRef,
        string periodScopeType,
        string? periodScopeRef)
    {
        var periodType = CampaignScopeTypes.Normalize(periodScopeType);
        var periodRef = CampaignScopeRules.Trim(periodScopeRef);

        foreach (var (scopeType, scopeRef) in ApplicableScopes(campaignScopeType, campaignScopeRef))
        {
            if (string.Equals(periodType, scopeType, StringComparison.Ordinal)
                && CampaignScopeRules.SameScopeRef(periodRef, scopeRef))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A human-readable list of the addresses a refusal message can name.</summary>
    public static string DescribeApplicable(string campaignScopeType, string? campaignScopeRef)
        => string.Join(
            " or ",
            ApplicableScopes(campaignScopeType, campaignScopeRef)
                .Select(s => CampaignScopeRules.Describe(s.ScopeType, s.ScopeRef)));
}
