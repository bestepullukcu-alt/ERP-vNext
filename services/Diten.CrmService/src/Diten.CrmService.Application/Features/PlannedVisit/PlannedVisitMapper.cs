using Diten.CrmService.Domain.Entities;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.PlannedVisit;

/// <summary>Entity → DTO projections. One place, so the grid, the detail and the read paths can never disagree about
/// what a plan is. PlannedDate is projected as an ISO "yyyy-MM-dd" string so a JSON client needs no DateOnly knowledge.</summary>
public static class PlannedVisitMapper
{
    private const string DateFormat = "yyyy-MM-dd";

    public static PlannedVisitListItemDto ToListItem(PlannedVisitEntity p) => new(
        p.Id, p.VisitCode, p.TargetType, p.TargetId, p.AccountId, p.ContactId, p.AccountContactLinkId,
        p.PlannedDate.ToString(DateFormat), p.PlannedStartTime, p.PlannedEndTime, p.PlannedDurationMinutes,
        p.Resource.ResourceId, p.Resource.ResourceType, p.Resource.DisplayName,
        p.VisitPurpose, p.VisitType, p.BusinessUnit, p.TerritoryNodeId, p.CampaignId,
        p.PlanStatus, p.Source,
        p.Consent?.EligibilityStatus, p.Frequency?.FrequencyStatus,
        p.Version, p.CreatedAt, p.UpdatedAt,
        ToContentItems(p.ContentItems));

    public static PlannedVisitDetailDto ToDetail(PlannedVisitEntity p) => new(
        p.Id, p.VisitCode, p.TargetType, p.TargetId, p.AccountId, p.ContactId, p.AccountContactLinkId,
        p.PlannedDate.ToString(DateFormat), p.PlannedStartTime, p.PlannedEndTime, p.PlannedDurationMinutes,
        new PlannedVisitResourceRefDto(p.Resource.ResourceId, p.Resource.ResourceType, p.Resource.DisplayName),
        p.PositionCode, p.PositionId,
        p.VisitPurpose, p.VisitType, p.Objective, p.Notes,
        p.BusinessUnit, p.TerritoryNodeId, p.TerritoryModelId, p.CampaignId,
        p.Content?.JourneyId, p.Content?.StageId,
        p.PlanStatus, p.Source, p.CancellationReason,
        p.IsDraft(), p.IsPlanned(), p.IsConfirmed(), p.IsCancelled(), p.IsArchived(),
        p.ArchivedAt, p.ArchivedBy,
        ToSlot(p.Slot),
        ToFrequency(p.Frequency),
        ToConsent(p.Consent),
        ToContent(p.Content),
        ToSelection(p.Selection),
        ToAvailability(p.Availability),
        p.Version, p.CreatedAt, p.CreatedBy, p.UpdatedAt, p.UpdatedBy,
        ToContentItems(p.ContentItems));

    /// <summary>WP-VP-2 (B-8) — a list row with its read-time names.</summary>
    public static PlannedVisitListItemDto ToListItem(PlannedVisitEntity p, VisitTargetNames names)
    {
        var (target, account, contact, inactive) = names.For(p.TargetType, p.TargetId, p.AccountId, p.ContactId);
        return ToListItem(p) with
        {
            TargetDisplayName = target, AccountDisplayName = account, ContactDisplayName = contact, TargetInactive = inactive
        };
    }

    /// <summary>WP-VP-2 (B-8) — the detail with its read-time names.</summary>
    public static PlannedVisitDetailDto ToDetail(PlannedVisitEntity p, VisitTargetNames names)
    {
        var (target, account, contact, inactive) = names.For(p.TargetType, p.TargetId, p.AccountId, p.ContactId);
        return ToDetail(p) with
        {
            TargetDisplayName = target, AccountDisplayName = account, ContactDisplayName = contact, TargetInactive = inactive
        };
    }

    /// <summary>WP-SB-3b — the frozen product list (never null on the wire: empty for an older plan).</summary>
    public static IReadOnlyList<PlannedVisitContentItemDto> ToContentItems(IEnumerable<PlannedVisitContentItem>? items)
        => (items ?? Enumerable.Empty<PlannedVisitContentItem>())
            .Select(i => new PlannedVisitContentItemDto(
                i.ProductId, i.ProductCode, i.Role, i.JourneyId, i.JourneyCode, i.StageId, i.StageIndex, i.StageCode,
                i.StageName, i.PathId, i.PathCode, i.PathVersion,
                i.Steps.Select(s => new PlannedVisitContentStepDto(s.StepId, s.ContentId, s.ContentCode, s.Title, s.Type, s.Minutes))
                    .ToList(),
                i.Claims.Select(c => new PlannedVisitContentClaimDto(c.ClaimId, c.ClaimCode)).ToList(),
                i.Warnings.ToList()))
            .ToList();

    private static PlannedVisitScheduleSlotDto ToSlot(PlannedVisitScheduleSlot s)
        => new(s.SequenceOrder, s.SlotStartTime, s.SlotEndTime, s.IsPacked);

    private static PlannedVisitFrequencyProvenanceDto? ToFrequency(PlannedVisitFrequencyProvenance? f)
        => f is null
            ? null
            : new(f.FrequencyStatus, f.SelectedFrequencyPolicyId, f.SelectedPolicyCode, f.SelectedPolicyName,
                f.FrequencyType, f.RequiredVisitCount, f.PeriodType, f.SelectionReason, f.ReasonCodes, f.ResolvedAt);

    private static PlannedVisitConsentProvenanceDto? ToConsent(PlannedVisitConsentProvenance? c)
        => c is null
            ? null
            : new(c.FilterApplied, c.EligibilityStatus, c.Decision, c.Channel, c.Purpose, c.MatchedConsentId,
                c.MatchedPreferenceIds, c.ReasonCodes, c.SelectionReason, c.EvaluatorVersion, c.EvaluatedAt);

    private static PlannedVisitContentRefDto? ToContent(PlannedVisitContentRef? c)
        => c is null
            ? null
            : new(c.JourneyId, c.StageId, c.StageIndex, c.StageCode, c.ContentSource, c.IsOverridden,
                c.StrategyTemplateId, c.JourneyDisplayName, c.StageDisplayName, c.ResolvedAt);

    private static PlannedVisitSelectionProvenanceDto? ToSelection(PlannedVisitSelectionProvenance? s)
        => s is null
            ? null
            : new(s.SegmentId, s.CampaignId, s.StrategyTemplateId, s.SelectionMode, s.DecidedAt, s.DecidedBy);

    private static PlannedVisitAvailabilitySnapshotDto? ToAvailability(PlannedVisitAvailabilitySnapshot? a)
        => a is null
            ? null
            : new(a.Weekday, a.AvailableStartTime, a.AvailableEndTime, a.AppointmentRequired,
                a.WithinAvailableWindow, a.ReasonCodes, a.CapturedAt);
}
