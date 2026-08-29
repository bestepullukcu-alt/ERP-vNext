using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Campaign;

/// <summary>
/// MOD-0165 FU04 structural validation. Every rule returns an error string (400 message) or null. The campaign/target
/// vocabulary is validated in-domain against the <c>Campaign*</c> constants — it is structural, not tenant vocabulary,
/// so it never fails open on an unpublished MOD-0048 set.
/// <para>
/// Reference fields (Brand / Product / Subject / Topic / ConceptChainTemplate / EngagementJourney / KnowledgePath /
/// KnowledgeContent) are validated at <b>format level only</b>: MOD-0290 and MOD-0162 have no runtime yet, so there is
/// no master to resolve them against. FU04 therefore rejects a structurally impossible reference (empty GUID) and
/// stores everything else as provenance — it never invents, copies or silently drops a reference.
/// </para>
/// </summary>
public static class CampaignValidation
{
    public static string? ValidateCampaignCode(string? campaignCode)
        => string.IsNullOrWhiteSpace(campaignCode) ? "CampaignCode is required." : null;

    public static string? ValidateCampaignName(string? campaignName)
        => string.IsNullOrWhiteSpace(campaignName) ? "CampaignName is required." : null;

    public static string? ValidateCampaignType(string? campaignType)
        => CampaignTypes.IsValid(campaignType)
            ? null
            : $"CampaignType is required and must be one of: {string.Join(", ", CampaignTypes.All)}.";

    /// <summary>Status is optional on write (defaults to draft). When supplied it must be a lifecycle value.</summary>
    public static string? ValidateCampaignStatus(string? campaignStatus)
        => string.IsNullOrWhiteSpace(campaignStatus) || CampaignStatuses.IsValid(campaignStatus)
            ? null
            : $"CampaignStatus must be one of: {string.Join(", ", CampaignStatuses.All)}. " +
              "A campaign is never hard-deleted; closing it is the archive endpoint.";

    public static string? ValidateObjectiveType(string? objectiveType)
        => string.IsNullOrWhiteSpace(objectiveType) || CampaignObjectiveTypes.IsValid(objectiveType)
            ? null
            : $"ObjectiveType must be one of: {string.Join(", ", CampaignObjectiveTypes.All)}.";

    public static string? ValidateStartDate(DateTimeOffset startDate)
        => startDate == default ? "StartDate is required." : null;

    public static string? ValidateCampaignPeriod(DateTimeOffset startDate, DateTimeOffset? endDate)
        => endDate is { } end && end < startDate
            ? "EndDate cannot be earlier than StartDate."
            : null;

    /// <summary>The optional campaign-level consent defaults must be known MOD-0164 vocabulary when supplied — a typo
    /// must not become a silently unusable default.</summary>
    public static string? ValidateConsentDefaults(string? channel, string? purpose)
    {
        if (!string.IsNullOrWhiteSpace(channel) && !ConsentChannel.IsValid(channel))
        {
            return $"DefaultConsentChannel must be one of: {string.Join(", ", ConsentChannel.All)}.";
        }

        return !string.IsNullOrWhiteSpace(purpose) && !ConsentPurpose.IsValid(purpose)
            ? $"DefaultConsentPurpose must be one of: {string.Join(", ", ConsentPurpose.All)}."
            : null;
    }

    /// <summary>
    /// Format-level reference validation. An explicitly supplied but empty GUID is a caller error, not a "no reference"
    /// signal — accepting it would store an unusable link that later looks like real provenance.
    /// </summary>
    public static string? ValidateOptionalReference(Guid? value, string fieldName)
        => value is { } id && id == Guid.Empty
            ? $"{fieldName} must be a non-empty identifier when supplied (omit the field instead)."
            : null;

    /// <summary>
    /// MOD-0165 FU08 — FORMAT level only, and deliberately nothing more. Whether the period exists, is active and
    /// contains the campaign window needs a READ, so it lives in <see cref="CampaignCycleBindingGuard"/>; this method
    /// stays pure so the shared synchronous validator can stay pure.
    /// </summary>
    public static string? ValidateCyclePeriodReference(Guid? cyclePeriodId)
        => ValidateOptionalReference(cyclePeriodId, "CyclePeriodId");

    // ---------------- CampaignTarget ----------------

    public static string? ValidateTargetType(string? targetType)
        => CampaignTargetTypes.IsValid(targetType)
            ? null
            : $"TargetType is required and must be one of: {string.Join(", ", CampaignTargetTypes.All)}. " +
              "'campaign-target' is deliberately not a campaign target type (self-referential loop).";

    public static string? ValidateTargetId(Guid targetId)
        => targetId == Guid.Empty ? "TargetId is required and cannot be empty." : null;

    public static string? ValidateTargetSource(string? targetSource)
        => CampaignTargetSources.IsValid(targetSource)
            ? null
            : $"TargetSource is required and must be one of: {string.Join(", ", CampaignTargetSources.All)}.";

    public static string? ValidateTargetStatus(string? targetStatus)
        => string.IsNullOrWhiteSpace(targetStatus) || CampaignTargetStatuses.IsValid(targetStatus)
            ? null
            : $"TargetStatus must be one of: {string.Join(", ", CampaignTargetStatuses.All)}. " +
              "A target is never hard-deleted; closing it is the archive endpoint.";

    /// <summary>A target must always say why it exists — a silent selection is forbidden (pack §7).</summary>
    public static string? ValidateSelectionReason(string? selectionReason)
        => string.IsNullOrWhiteSpace(selectionReason)
            ? "SelectionReason is required: a campaign target may never be selected without a stated reason."
            : null;

    public static string? ValidateEffectiveRange(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
        => effectiveTo is { } to && to < effectiveFrom
            ? "EffectiveTo cannot be earlier than EffectiveFrom."
            : null;

    /// <summary>
    /// DEPRECATED input (MOD-0165 FU11) - kept because the SNAPSHOT path may still be handed an integer by an existing
    /// caller. Removing it would reject requests that were valid yesterday for a reason the caller cannot see.
    /// Manual authoring no longer sends it; see <see cref="ValidatePriorityLevel"/>.
    /// </summary>
    public static string? ValidatePriority(int? priority)
        => priority is { } value && value < 1
            ? "Priority must be a positive number when supplied (smaller wins)."
            : null;

    /// <summary>MOD-0165 FU11 - the priority BAND. Blank is allowed (no band stated); an unknown band is refused rather
    /// than rounded to a neighbour, because a target quietly demoted to <c>low</c> gets worked on last for a reason
    /// nobody chose.</summary>
    public static string? ValidatePriorityLevel(string? priorityLevel)
        => string.IsNullOrWhiteSpace(priorityLevel) || CampaignTargetPriorityLevels.IsValid(priorityLevel)
            ? null
            : $"PriorityLevel must be one of: {string.Join(", ", CampaignTargetPriorityLevels.All)} " +
              $"({CampaignReasonCodes.CampaignTargetPriorityLevelUnknown}).";

    /// <summary>
    /// MOD-0165 FU11 - the statuses a human may set directly. <c>excluded</c> is refused because it is the OUTCOME of a
    /// consent evaluation, which also supplies the reason an excluded target is required to carry; an author setting it
    /// by hand would produce a row that cannot satisfy its own rule. <c>archived</c> has its own endpoint.
    /// <para>The snapshot path does NOT come through here and still writes <c>excluded</c> exactly as before.</para>
    /// </summary>
    public static string? ValidateAuthorableTargetStatus(string? targetStatus)
        => string.IsNullOrWhiteSpace(targetStatus) || CampaignTargetStatuses.IsAuthorable(targetStatus)
            ? null
            : $"TargetStatus must be one of: {string.Join(", ", CampaignTargetStatuses.Authorable)} " +
              $"({CampaignReasonCodes.CampaignTargetStatusNotAuthorable}). 'excluded' is written by the consent " +
              "evaluation together with its reason, and 'archived' is set by the archive endpoint.";

    /// <summary>An excluded target must carry a reason — silently dropping a target is forbidden (pack §16).</summary>
    public static string? ValidateExclusion(string? targetStatus, string? exclusionReason)
        => CampaignTargetStatuses.Normalize(targetStatus) == CampaignTargetStatuses.Excluded
           && string.IsNullOrWhiteSpace(exclusionReason)
            ? "An excluded target requires an ExclusionReason."
            : null;

    /// <summary>A source reference type must be a known target-source-shaped value when supplied, and a bare
    /// SourceReferenceId with no type is meaningless provenance.</summary>
    public static string? ValidateSourceReference(string? sourceReferenceType, Guid? sourceReferenceId)
    {
        var hasId = sourceReferenceId is { } id && id != Guid.Empty;
        if (string.IsNullOrWhiteSpace(sourceReferenceType))
        {
            return hasId ? "SourceReferenceId requires a SourceReferenceType." : null;
        }

        return CampaignTargetTypes.IsValid(sourceReferenceType) || CampaignTargetSources.IsValid(sourceReferenceType)
            ? null
            : "SourceReferenceType must be a known target type or target source " +
              $"({string.Join(", ", CampaignTargetTypes.All)} | {string.Join(", ", CampaignTargetSources.All)}).";
    }

    // ---------------- Snapshot ----------------

    public static string? ValidateSnapshotSourceType(string? sourceType)
        => CampaignSnapshotSourceTypes.IsValid(sourceType)
            ? null
            : $"SourceType is required and must be one of: {string.Join(", ", CampaignSnapshotSourceTypes.All)}.";

    /// <summary>An empty snapshot is a caller error, not an instruction to clear the campaign — a snapshot never
    /// removes targets, so an empty list can only ever be a mistake.</summary>
    public static string? ValidateSnapshotItems(IReadOnlyList<CampaignSnapshotTargetItem>? items)
        => items is null || items.Count == 0
            ? "TargetItems is required and must contain at least one item. " +
              "An empty snapshot is rejected: a snapshot is additive and never removes existing targets."
            : null;

    /// <summary>
    /// External references: SourceSystem + ExternalId mandatory per line, at most one primary, and a duplicate
    /// (SourceSystem, ExternalId) inside the same payload is a conflict — silent merge is forbidden. Returns
    /// (error, isConflict); the caller maps a conflict to 409 and everything else to 400.
    /// </summary>
    public static (string? Error, bool IsConflict) ValidateExternalReferences(
        IReadOnlyList<CampaignExternalReferenceInput>? references)
    {
        if (references is null || references.Count == 0)
        {
            return (null, false);
        }

        foreach (var reference in references)
        {
            if (string.IsNullOrWhiteSpace(reference.SourceSystem))
            {
                return ("ExternalReferences[].SourceSystem is required.", false);
            }

            if (string.IsNullOrWhiteSpace(reference.ExternalId))
            {
                return ("ExternalReferences[].ExternalId is required.", false);
            }
        }

        if (references.Count(r => r.IsPrimary) > 1)
        {
            return ("At most one external reference may be marked IsPrimary.", false);
        }

        var duplicate = references
            .GroupBy(r => (r.SourceSystem.Trim().ToLowerInvariant(), r.ExternalId.Trim()))
            .FirstOrDefault(g => g.Count() > 1);

        return duplicate is null
            ? (null, false)
            : ($"Duplicate external mapping '{duplicate.Key.Item1}/{duplicate.Key.Item2}' in the payload. " +
               "Silent merge is not performed; resolve the conflict explicitly.", true);
    }
}
