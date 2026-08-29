using Diten.CrmService.Application.Features.Campaign.Read;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;
using CampaignEntity = Diten.CrmService.Domain.Entities.Campaign;

namespace Diten.CrmService.Application.Features.Campaign;

/// <summary>Aggregate ↔ DTO projection for MOD-0165 FU04. Reads never echo TenantId (server-resolved), and the consent
/// projection carries the decision only — never consent record content.</summary>
public static class CampaignMapper
{
    /// <summary>
    /// FU10 — one targeted segment as a campaign surface may DISPLAY it. When the segment could not be read the
    /// projection stays empty and <c>IsResolvable</c> is false: a detail page still opens, showing the pinned id
    /// rather than a label nobody can vouch for.
    /// </summary>
    public static CampaignTargetedSegmentDto ToDto(
        CampaignTargetedSegment link, IReadOnlyDictionary<Guid, CampaignSegmentRef>? segments)
    {
        if (segments is not null && segments.TryGetValue(link.SegmentId, out var s))
        {
            return new CampaignTargetedSegmentDto(
                link.SegmentId, link.LinkedAt, true,
                s.SegmentCode, s.SegmentName, s.SubjectType, s.SegmentStatus, s.Superseded, s.SegmentVersion);
        }

        return new CampaignTargetedSegmentDto(
            link.SegmentId, link.LinkedAt, false, null, null, null, null, false, null);
    }

    /// <summary>
    /// FU08 — the read seam's period snapshot narrowed to what a campaign surface may DISPLAY. Scope fields are
    /// deliberately dropped: FU08 does not match scope, so surfacing it here would suggest a guarantee that does not
    /// exist.
    /// </summary>
    public static CampaignCyclePeriodDto ToCyclePeriodDto(CyclePeriodSnapshot period) => new(
        period.CyclePeriodId,
        period.CycleCode,
        period.CycleName,
        period.Year,
        period.SequenceInYear,
        period.StartDate,
        period.EndDate,
        period.CycleStatus);

    /// <summary>
    /// FU08 — the campaign as-is, with no cycle projection. Used where the bound period is not being displayed; the
    /// binding ID is still echoed, so a caller always knows whether a campaign is bound.
    /// </summary>
    public static CampaignDto ToDto(CampaignEntity campaign) => ToDto(campaign, null, null);

    /// <summary>
    /// FU08 — the campaign plus an optional READ-TIME projection of its bound period. The projection is a display
    /// convenience and is never written back onto the campaign.
    /// </summary>
    public static CampaignDto ToDto(
        CampaignEntity campaign,
        CampaignCyclePeriodDto? cyclePeriod,
        IReadOnlyDictionary<Guid, CampaignSegmentRef>? segments) => new(
        campaign.Id,
        campaign.CampaignCode,
        campaign.CampaignName,
        campaign.CampaignType,
        campaign.CampaignStatus,
        campaign.ObjectiveType,
        campaign.BusinessUnitId,
        campaign.EffectiveScopeType(),
        campaign.ScopeRef(),
        campaign.CountryScope,
        campaign.LegalEntityId,
        campaign.DefaultConsentChannel,
        campaign.DefaultConsentPurpose,
        campaign.StartDate,
        campaign.EndDate,
        campaign.CyclePeriodId,
        cyclePeriod,
        campaign.EffectiveTargetingMode(),
        campaign.TargetedSegments.Select(s => s.SegmentId).ToList(),
        campaign.TargetedSegments.Select(s => ToDto(s, segments)).ToList(),
        campaign.Description,
        campaign.CreatedAt,
        campaign.CreatedBy,
        campaign.UpdatedAt,
        campaign.UpdatedBy,
        campaign.ArchivedAt,
        campaign.ArchivedBy,
        campaign.IsArchived());

    public static CampaignTargetDto ToDto(CampaignTarget target) => new(
        target.Id,
        target.CampaignId,
        target.TargetType,
        target.TargetId,
        target.TargetDisplayName,
        target.TargetStatus,
        target.TargetSource,
        target.SourceReferenceType,
        target.SourceReferenceId,
        target.SnapshotBatchId,
        target.SelectionReason,
        target.ReasonCodes.ToList(),
        target.Priority,
        target.DerivedPriorityLevel(),
        ToDto(target.ConsentEvaluation),
        target.EffectiveFrom,
        target.EffectiveTo,
        target.ExclusionReason,
        target.Notes,
        target.ExternalReferences.Select(ToDto).ToList(),
        target.CreatedAt,
        target.CreatedBy,
        target.UpdatedAt,
        target.UpdatedBy,
        target.ArchivedAt,
        target.ArchivedBy,
        target.IsArchived());

    public static CampaignTargetConsentEvaluationDto? ToDto(CampaignTargetConsentEvaluation? evaluation)
        => evaluation is null
            ? null
            : new CampaignTargetConsentEvaluationDto(
                evaluation.Decision,
                evaluation.EligibilityStatus,
                evaluation.ReasonCodes.ToList(),
                evaluation.EvaluatedAt,
                evaluation.MatchedConsentId,
                evaluation.MatchedPreferenceIds.ToList(),
                evaluation.EvaluatorVersion,
                evaluation.SelectionReason,
                evaluation.Channel,
                evaluation.Purpose,
                evaluation.FilterApplied);

    public static CampaignExternalReferenceDto ToDto(CampaignExternalReference reference) => new(
        reference.SourceSystem,
        reference.ExternalId,
        reference.ExternalCode,
        reference.ExternalName,
        reference.ImportedAt,
        reference.IsPrimary);

    /// <summary>Inbound external-reference lines → stored value objects. <c>ImportedAt</c> supplied by the caller is
    /// preserved (legacy history is never rewritten) and stamped with "now" only when omitted.</summary>
    public static List<CampaignExternalReference> ToEntities(
        IReadOnlyList<CampaignExternalReferenceInput>? inputs, DateTimeOffset now)
        => inputs is null
            ? new List<CampaignExternalReference>()
            : inputs.Select(i => new CampaignExternalReference
            {
                SourceSystem = i.SourceSystem.Trim(),
                ExternalId = i.ExternalId.Trim(),
                ExternalCode = string.IsNullOrWhiteSpace(i.ExternalCode) ? null : i.ExternalCode.Trim(),
                ExternalName = string.IsNullOrWhiteSpace(i.ExternalName) ? null : i.ExternalName.Trim(),
                ImportedAt = i.ImportedAt ?? now,
                IsPrimary = i.IsPrimary
            }).ToList();
}
