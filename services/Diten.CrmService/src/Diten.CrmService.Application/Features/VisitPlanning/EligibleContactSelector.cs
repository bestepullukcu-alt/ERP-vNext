using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.RouteOptimization;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using ContactAvailabilityEntity = Diten.CrmService.Domain.Entities.ContactAvailability;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// MOD-0155 FU05 — the read-only selection assembly over the shipped seams (§3 / §4.1 ③). It answers, for a set of
/// manually-picked doctors, three questions the engine needs before it can route:
/// <list type="bullet">
/// <item><b>Segment FILTER</b> — MOD-0167 <see cref="ISegmentMembershipReader"/> narrows the eligible universe;
/// unknown is never a member (D-SEGMENT-FILTER). A non-member is dropped from the eligible set, so it is never offered.</item>
/// <item><b>Consent gate</b> — MOD-0164 <see cref="IConsentPreferenceEvaluator"/> (channel = visit); a blocked doctor is
/// <b>excluded-not-dropped</b> with a reason (FilterApplied honoured, AC-SELECT-2).</item>
/// <item><b>Availability</b> — MOD-0150 <see cref="IContactAvailabilityRepository"/> per-contact windows, mapped to the
/// FU03 HARD <see cref="AvailabilityWindow"/> constraint (D-AVAILABILITY).</item>
/// </list>
/// It writes nothing and it is not an engine — it reads seams and reports. Segment membership NARROWS; the pick stays a
/// human's (the selection arrives already made).
/// </summary>
public sealed class EligibleContactSelector
{
    private readonly ITenantContext _tenant;
    private readonly ISegmentMembershipReader _segments;
    private readonly IConsentPreferenceEvaluator _consent;
    private readonly IContactAvailabilityRepository _availabilities;

    public EligibleContactSelector(
        ITenantContext tenant,
        ISegmentMembershipReader segments,
        IConsentPreferenceEvaluator consent,
        IContactAvailabilityRepository availabilities)
    {
        _tenant = tenant;
        _segments = segments;
        _consent = consent;
        _availabilities = availabilities;
    }

    /// <summary>
    /// Assess each selected doctor against segment (filter), consent (gate) and availability (hard windows). A doctor
    /// the segment does not admit is NOT returned (segment filters the universe); every admitted doctor is returned with
    /// its consent verdict and its availability windows, whether or not consent blocks it — a blocked doctor is
    /// excluded-not-dropped (it appears with <see cref="EligibleContactAssessment.ConsentBlocked"/> true and a reason,
    /// so the planner sees WHY).
    /// </summary>
    public async Task<IReadOnlyList<EligibleContactAssessment>> AssessAsync(
        IReadOnlyList<PlanningSessionSelectedContact> selected,
        Guid? segmentId,
        string visitPurpose,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        var results = new List<EligibleContactAssessment>();
        if (_tenant.TenantId is not { } tenantId || selected is null || selected.Count == 0)
        {
            return results;
        }

        var purpose = PlannedVisitValidation.ToConsentPurpose(visitPurpose);

        foreach (var pick in selected)
        {
            if (pick.ContactId == Guid.Empty)
            {
                continue;
            }

            // Segment FILTER — unknown is never a member. A non-member is not offered (dropped from the eligible set).
            if (segmentId is { } sid && sid != Guid.Empty)
            {
                var verdict = await _segments.IsMemberAsync(
                    sid, ConsentSubjectType.Contact, pick.ContactId, effectiveAt, cancellationToken);
                if (!verdict.IsMember)
                {
                    continue;
                }
            }

            // Consent GATE — excluded-not-dropped. The evaluator never throws into us (controlled unknown).
            var consent = await _consent.EvaluateAsync(
                new ConsentEvaluationRequest(
                    ConsentSubjectType.Contact, pick.ContactId, ConsentChannel.Visit, purpose, effectiveAt),
                cancellationToken);
            var consentBlocked = string.Equals(
                consent.EligibilityStatus, ConsentEligibilityStatus.Blocked, StringComparison.Ordinal);

            var windows = await ResolveWindowsAsync(tenantId, pick, cancellationToken);

            results.Add(new EligibleContactAssessment(
                pick.ContactId,
                pick.AccountId,
                pick.AccountContactLinkId,
                SegmentEligible: true,
                ConsentStatus: consent.EligibilityStatus,
                ConsentBlocked: consentBlocked,
                ConsentReason: consent.SelectionReason,
                AvailabilityWindows: windows));
        }

        return results;
    }

    private async Task<IReadOnlyList<AvailabilityWindow>> ResolveWindowsAsync(
        Guid tenantId, PlanningSessionSelectedContact pick, CancellationToken cancellationToken)
    {
        IReadOnlyList<ContactAvailabilityEntity> rows;
        if (pick.AccountContactLinkId is { } linkId && linkId != Guid.Empty)
        {
            rows = await _availabilities.ListByLinkAsync(tenantId, linkId, cancellationToken);
        }
        else
        {
            rows = await _availabilities.ListByContactAsync(tenantId, pick.ContactId, cancellationToken);
        }

        return rows
            .Where(a => !a.IsDeleted
                        && string.Equals(a.Status, AvailabilityLifecycle.Active, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(a.Weekday)
                        && !string.IsNullOrWhiteSpace(a.StartTime)
                        && !string.IsNullOrWhiteSpace(a.EndTime))
            .Select(a => new AvailabilityWindow(a.Weekday.Trim().ToLowerInvariant(), a.StartTime, a.EndTime))
            .ToList();
    }
}

/// <summary>One admitted doctor with its consent verdict + hard availability windows. Consent-blocked is
/// excluded-not-dropped, so a blocked doctor still appears here (with a reason) — the planner decides.</summary>
public sealed record EligibleContactAssessment(
    Guid ContactId,
    Guid? AccountId,
    Guid? AccountContactLinkId,
    bool SegmentEligible,
    string ConsentStatus,
    bool ConsentBlocked,
    string ConsentReason,
    IReadOnlyList<AvailabilityWindow> AvailabilityWindows);
