using Diten.CrmService.Application.Features.Campaign;

namespace Diten.CrmService.Api.Models.CRM;

/// <summary>
/// MOD-0165 FU04 request bodies. Note what is NOT here: <c>TenantId</c> — it is server-resolved from the JWT claim and
/// can never be supplied by a caller. On update, <c>CampaignCode</c> is absent (immutable; rename goes through
/// <c>CampaignName</c>), and on a target update <c>TargetType</c>/<c>TargetId</c> are absent (immutable) along with
/// <c>ConsentEvaluation</c> — a caller may never hand-craft a consent verdict. There is no delete body: closing a
/// record is the archive endpoint.
/// </summary>
public sealed record CreateCampaignRequest(
    // FU10 - optional: an empty code is generated server-side (CMP-{YYYY}-{sequence}) at WRITE time, so an abandoned
    // create screen never burns a sequence number. Still author-editable.
    string? CampaignCode,
    string CampaignName,
    string CampaignType,
    DateTimeOffset StartDate,
    string? CampaignStatus = null,
    string? ObjectiveType = null,
    string? BusinessUnitId = null,
    string? DefaultConsentChannel = null,
    string? DefaultConsentPurpose = null,
    DateTimeOffset? EndDate = null,
    string? Description = null,
    // FU08 - optional pin to a planning period. Omitted means "not cycle-bound".
    Guid? CyclePeriodId = null,
    // FU09 - the campaign's discriminated address. An omitted ScopeType is DERIVED from the references, so a caller
    // written against FU04/FU08 keeps meaning exactly what it always meant.
    string? ScopeType = null,
    string? CountryScope = null,
    Guid? LegalEntityId = null,
    // FU10 - how the campaign is targeted, and (in segment mode) which segments.
    string? TargetingMode = null,
    List<Guid>? TargetedSegmentIds = null);

public sealed record UpdateCampaignRequest(
    string CampaignName,
    string CampaignType,
    DateTimeOffset StartDate,
    string? CampaignStatus = null,
    string? ObjectiveType = null,
    string? BusinessUnitId = null,
    string? DefaultConsentChannel = null,
    string? DefaultConsentPurpose = null,
    DateTimeOffset? EndDate = null,
    string? Description = null,
    // FU08 - full replace like every other field: null is an explicit UNBIND, which is always allowed.
    Guid? CyclePeriodId = null,
    // FU09 - full replace. The scope is editable; changing it re-validates the bound cycle period.
    string? ScopeType = null,
    string? CountryScope = null,
    Guid? LegalEntityId = null,
    // FU10 - full replace. Switching the mode never clears the other mode's stored data.
    string? TargetingMode = null,
    List<Guid>? TargetedSegmentIds = null);

public sealed record CreateCampaignTargetRequest(
    string TargetType,
    Guid TargetId,
    string? TargetSource = null,
    string? SelectionReason = null,
    DateTimeOffset? EffectiveFrom = null,
    string? TargetDisplayName = null,
    string? TargetStatus = null,
    string? SourceReferenceType = null,
    Guid? SourceReferenceId = null,
    int? Priority = null,
    string? PriorityLevel = null,
    DateTimeOffset? EffectiveTo = null,
    string? ExclusionReason = null,
    string? Notes = null,
    List<string>? ReasonCodes = null,
    List<CampaignExternalReferenceInput>? ExternalReferences = null);

public sealed record UpdateCampaignTargetRequest(
    string? TargetSource = null,
    string? SelectionReason = null,
    DateTimeOffset? EffectiveFrom = null,
    string? TargetDisplayName = null,
    string? TargetStatus = null,
    string? SourceReferenceType = null,
    Guid? SourceReferenceId = null,
    int? Priority = null,
    string? PriorityLevel = null,
    DateTimeOffset? EffectiveTo = null,
    string? ExclusionReason = null,
    string? Notes = null,
    List<string>? ReasonCodes = null,
    List<CampaignExternalReferenceInput>? ExternalReferences = null);

/// <summary>
/// Static snapshot body. <c>ApplyConsentFilter</c> defaults to <b>true</b> on purpose: an omitted flag must not
/// silently produce an unfiltered targeting run. With the filter on, <c>ConsentChannel</c> and <c>ConsentPurpose</c>
/// are mandatory unless the campaign carries defaults.
/// </summary>
public sealed record CreateCampaignTargetSnapshotRequest(
    string SourceType,
    List<CampaignSnapshotTargetItem> TargetItems,
    string SelectionReason,
    bool ApplyConsentFilter = true,
    string? SourceReferenceType = null,
    Guid? SourceReferenceId = null,
    string? ConsentChannel = null,
    string? ConsentPurpose = null,
    DateTimeOffset? EffectiveAt = null,
    DateTimeOffset? EffectiveTo = null,
    List<string>? ReasonCodes = null);
