using Diten.CrmService.Application.Features.Campaign.Rules;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Campaign;

/// <summary>
/// MOD-0165 FU08 — the single place that decides whether a campaign may carry a cycle-period binding.
///
/// <para><b>Why it is not part of <c>CampaignWrite.Validate</c>.</b> That method is pure and synchronous: it takes
/// values, returns an error, touches nothing. Proving a binding needs an asynchronous READ, and pouring I/O into a
/// pure validator would make every caller async for a rule most campaigns never hit. So the pure rules stay pure and
/// the rule that reads lives here — in one class, called from both Create and Update, because a rule written twice is
/// two rules.</para>
///
/// <para><b>Read-only, in-process, no self-call.</b> It holds an <see cref="ICyclePeriodReader"/> and nothing else: no
/// repository, no <c>HttpClient</c>. It never creates, updates or closes a period. A consumer inside CrmService must
/// not go out through the Gateway to reach its own service.</para>
///
/// <para><b>Fail-closed.</b> Every refusal happens BEFORE the caller writes anything, so a rejected binding can never
/// be half-applied. A period that cannot be found — including one belonging to another tenant, which the reader
/// resolves to <c>null</c> — is refused rather than accepted on trust.</para>
/// </summary>
public sealed class CampaignCycleBindingGuard
{
    private readonly ICyclePeriodReader _periods;

    public CampaignCycleBindingGuard(ICyclePeriodReader periods)
    {
        _periods = periods;
    }

    /// <summary>
    /// Decides the binding for one campaign write.
    ///
    /// <para><b>The two checks fire on different triggers, and that asymmetry is the whole design (D-RECHECK):</b></para>
    /// <list type="bullet">
    /// <item><description><b>ACTIVE</b> is checked only when the binding CHANGES to a non-null period
    /// (<paramref name="requestedCyclePeriodId"/> differs from <paramref name="currentCyclePeriodId"/>). A period that
    /// closes after the fact keeps its bindings — checking active on every write would make every campaign bound to a
    /// period uneditable the day that period closed, which is exactly what "closing a period changes no campaign"
    /// forbids.</description></item>
    /// <item><description><b>Containment</b> is checked on EVERY write while the campaign ends up bound, changed
    /// binding or not. Otherwise an author could bind inside the window and then quietly drag the dates
    /// out of it.</description></item>
    /// <item><description><b>Scope applicability (FU09)</b> is checked on EVERY write while bound, for the same
    /// reason and one more: the campaign's scope is EDITABLE, so a rule that only fired when the BINDING changed
    /// could be walked around by moving the campaign instead of the period.</description></item>
    /// </list>
    ///
    /// <para>Unbinding (<paramref name="requestedCyclePeriodId"/> = <c>null</c>) is always allowed and skips both
    /// checks: with no period there is no window to be inside of.</para>
    /// </summary>
    /// <param name="requestedCyclePeriodId">The binding the caller wants. <c>null</c> means unbound.</param>
    /// <param name="currentCyclePeriodId">The binding already stored. <c>null</c> on create.</param>
    /// <param name="campaignStart">The campaign's own start — never derived from the period.</param>
    /// <param name="campaignEnd">The campaign's own end. <c>null</c> means open-ended.</param>
    public async Task<CampaignCycleBindingVerdict> EvaluateAsync(
        Guid? requestedCyclePeriodId,
        Guid? currentCyclePeriodId,
        DateTimeOffset campaignStart,
        DateTimeOffset? campaignEnd,
        string campaignScopeType,
        string? campaignScopeRef,
        CancellationToken cancellationToken)
    {
        if (requestedCyclePeriodId is not { } cyclePeriodId || cyclePeriodId == Guid.Empty)
        {
            // Unbound (or unbinding): no period, no window, no rule.
            return CampaignCycleBindingVerdict.Allowed();
        }

        var period = await _periods.GetByIdAsync(cyclePeriodId, cancellationToken);
        if (period is null)
        {
            return CampaignCycleBindingVerdict.Refused(
                $"CyclePeriod '{cyclePeriodId}' was not found in this tenant. The campaign was not saved.",
                CampaignReasonCodes.CampaignCyclePeriodNotFound);
        }

        var bindingChanged = requestedCyclePeriodId != currentCyclePeriodId;
        if (bindingChanged && !string.Equals(period.CycleStatus, CyclePeriodStatuses.Active, StringComparison.Ordinal))
        {
            return CampaignCycleBindingVerdict.Refused(
                $"CyclePeriod '{period.CycleCode}' is {period.CycleStatus}; only an active period can be bound to a " +
                "campaign. A period that closes after a campaign was bound to it keeps that binding.",
                CampaignReasonCodes.CampaignCyclePeriodNotActive);
        }

        // D-OPENEND: an open-ended campaign has no last day, so it can never be contained in a period that has one.
        // Implying the period's end instead would invent a campaign date the author never wrote.
        if (campaignEnd is not { } end)
        {
            return CampaignCycleBindingVerdict.Refused(
                "A campaign bound to a cycle period requires an EndDate: an open-ended campaign cannot be contained " +
                $"in the window of period '{period.CycleCode}'. Set an end date, or remove the cycle period.",
                CampaignReasonCodes.CampaignOutsideCycleWindow);
        }

        // FU09 - the period must be APPLICABLE to the campaign's address: its own scope, or the tenant-wide fallback.
        // Checked on EVERY write while bound, not only when the binding changes, because the campaign's scope is
        // editable: checking at bind time alone would let an author bind inside the rule and then move the campaign
        // out of it. It is checked BEFORE containment so the more fundamental mismatch is the one reported.
        if (!CampaignCycleApplicability.IsApplicable(
                campaignScopeType, campaignScopeRef, period.ScopeType, period.ScopeRef))
        {
            return CampaignCycleBindingVerdict.Refused(
                $"Cycle period '{period.CycleCode}' is scoped to "
                + $"{CampaignScopeRules.Describe(period.ScopeType, period.ScopeRef)}, which does not apply to a "
                + $"campaign scoped to {CampaignScopeRules.Describe(campaignScopeType, campaignScopeRef)}. "
                + $"Applicable scopes are: {CampaignCycleApplicability.DescribeApplicable(campaignScopeType, campaignScopeRef)}. "
                + "Remove the cycle period, or change the campaign scope.",
                CampaignReasonCodes.CampaignCyclePeriodScopeMismatch);
        }

        if (!IsWithin(campaignStart, end, period.StartDate, period.EndDate))
        {
            return CampaignCycleBindingVerdict.Refused(
                $"The campaign window {Day(campaignStart):yyyy-MM-dd}..{Day(end):yyyy-MM-dd} is not inside the window " +
                $"of period '{period.CycleCode}' ({Day(period.StartDate):yyyy-MM-dd}..{Day(period.EndDate):yyyy-MM-dd}). " +
                "While bound, a campaign must stay within its period (both ends inclusive).",
                CampaignReasonCodes.CampaignOutsideCycleWindow);
        }

        return CampaignCycleBindingVerdict.Allowed();
    }

    /// <summary>
    /// Containment on the canonical UTC DAY, both ends inclusive.
    ///
    /// <para>Reducing to <c>.Date</c> is load-bearing, not tidiness. A period stores its bounds at UTC midnight, while
    /// a campaign carries a real instant: a campaign ending at 18:00Z on the period's last day is INSIDE the period,
    /// but an instant-level comparison calls it outside and rejects a perfectly valid campaign. Comparing UTC days
    /// also makes the answer independent of the offset the caller happened to send.</para>
    /// </summary>
    public static bool IsWithin(
        DateTimeOffset campaignStart,
        DateTimeOffset campaignEnd,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd)
        => Day(periodStart) <= Day(campaignStart) && Day(campaignEnd) <= Day(periodEnd);

    private static DateTime Day(DateTimeOffset value) => value.UtcDateTime.Date;
}

/// <summary>
/// The guard's answer. A refusal always carries BOTH a human message and one of the three FU08 reason codes, so a
/// caller can tell "no such period" from "period not active" from "outside the window" without parsing prose.
/// </summary>
public sealed record CampaignCycleBindingVerdict(bool IsAllowed, string? Error, string? ReasonCode)
{
    public static CampaignCycleBindingVerdict Allowed() => new(true, null, null);

    public static CampaignCycleBindingVerdict Refused(string error, string reasonCode) => new(false, error, reasonCode);
}
