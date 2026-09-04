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
        p.Version, p.CreatedAt, p.UpdatedAt);

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
        p.Version, p.CreatedAt, p.CreatedBy, p.UpdatedAt, p.UpdatedBy);

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
