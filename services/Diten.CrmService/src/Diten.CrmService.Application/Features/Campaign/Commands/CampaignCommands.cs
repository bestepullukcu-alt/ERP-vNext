using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Campaign.Commands;

/// <summary>
/// MOD-0165 FU04 campaign write surface. <c>TenantId</c> is NEVER accepted from the payload (server-resolved from the
/// JWT claim). There is deliberately NO delete command — closing a campaign is <see cref="ArchiveCampaignCommand"/>
/// (soft lifecycle), so campaign and targeting history stay readable.
/// </summary>
public sealed record CreateCampaignCommand(
    // FU10 - optional: an empty code is generated server-side as CMP-{YYYY}-{sequence}. Left author-editable so a
    // team with its own numbering can still supply one.
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
    // FU08 — optional pin to a planning period. Appended last on purpose: every existing caller keeps compiling and
    // an omitted value means "not cycle-bound", which is what every campaign written before FU08 already was.
    Guid? CyclePeriodId = null,
    // FU09 - the campaign's discriminated address. Appended last so every existing caller keeps compiling; an omitted
    // ScopeType is DERIVED from the references (business unit -> business-unit, none -> tenant), which is exactly what
    // a pre-FU09 caller already meant.
    string? ScopeType = null,
    string? CountryScope = null,
    Guid? LegalEntityId = null,
    // FU10 - how the campaign is targeted, and (in segment mode) who it targets. An omitted mode keeps the row's
    // effective mode, which for a pre-FU10 campaign is manual.
    string? TargetingMode = null,
    IReadOnlyList<Guid>? TargetedSegmentIds = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields of a campaign. <c>CampaignCode</c> is immutable (rename goes through
/// <c>CampaignName</c>). An archived campaign cannot be updated.</summary>
public sealed record UpdateCampaignCommand(
    Guid CampaignId,
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
    // FU08 — the requested binding. null means "not bound"; on update that is an explicit UNBIND, which is always
    // allowed. This is a full replace like every other field on this command.
    Guid? CyclePeriodId = null,
    // FU09 - full replace like every other field on this command. The scope is editable, and changing it re-validates
    // the bound cycle period rather than silently unbinding it.
    string? ScopeType = null,
    string? CountryScope = null,
    Guid? LegalEntityId = null,
    // FU10 - full replace, like every other field on this command. Switching the mode never clears the other mode's
    // stored data; it only changes which one is validated and used.
    string? TargetingMode = null,
    IReadOnlyList<Guid>? TargetedSegmentIds = null) : IRequest<Response<bool>>;

/// <summary>Archives a campaign (ArchivedAt/By stamped, status → archived). Still readable; accepts no target
/// mutation afterwards. Existing targets are deliberately NOT cascaded — a silent cascade is forbidden.</summary>
public sealed record ArchiveCampaignCommand(Guid CampaignId) : IRequest<Response<bool>>;

/// <summary>
/// Manually authors one campaign target. A second ACTIVE target for the same
/// (CampaignId, TargetType, TargetId) is a 409 — the manual path is strict, because a human adding a duplicate by hand
/// is a mistake, not an idempotent retry (the snapshot path reconciles instead; see the snapshot command).
/// </summary>
public sealed record CreateCampaignTargetCommand(
    Guid CampaignId,
    string TargetType,
    Guid TargetId,
    // MOD-0165 FU11 - these three became OPTIONAL. The manual screen no longer asks for them because the server already
    // knows the answers: the source IS manual, the moment IS now, and who selected it is in the actor context. A caller
    // that still sends them is honoured unchanged, so nothing that worked before stops working.
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
    IReadOnlyList<string>? ReasonCodes = null,
    IReadOnlyList<CampaignExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields of a target. <c>TargetType</c>/<c>TargetId</c> and <c>CampaignId</c> are
/// IMMUTABLE — a different target is a different record. Consent provenance is NOT settable here: it is only ever
/// written by the snapshot from a live MOD-0164 evaluation, so a caller can never hand-craft a consent verdict.</summary>
public sealed record UpdateCampaignTargetCommand(
    Guid CampaignId,
    Guid CampaignTargetId,
    // MOD-0165 FU11 - optional, as on create. Omitted on update means KEEP what the target already says: an edit that
    // did not mention the reason must not overwrite the reason, and re-stamping EffectiveFrom would silently move when
    // the target started counting.
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
    IReadOnlyList<string>? ReasonCodes = null,
    IReadOnlyList<CampaignExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<bool>>;

public sealed record ArchiveCampaignTargetCommand(Guid CampaignId, Guid CampaignTargetId) : IRequest<Response<bool>>;

/// <summary>
/// Produces a STATIC target snapshot from the caller-supplied <see cref="TargetItems"/>.
/// <para>
/// It resolves NO membership: a segment-sourced snapshot stores the segment id as provenance and takes the items as
/// given (MOD-0167 boundary). It is <b>additive</b> — it never deletes or archives an earlier target — and
/// <b>idempotent per source</b>: re-running produces reconciles, not duplicates.
/// </para>
/// <para>
/// When <see cref="ApplyConsentFilter"/> is true, a channel and purpose are MANDATORY (from the request or the
/// campaign defaults); otherwise the request is rejected rather than evaluated against a guessed question. When it is
/// explicitly false, targets are still produced but every row carries <c>consent_filter_not_applied</c>.
/// </para>
/// </summary>
public sealed record CreateCampaignTargetSnapshotCommand(
    Guid CampaignId,
    string SourceType,
    IReadOnlyList<CampaignSnapshotTargetItem> TargetItems,
    string SelectionReason,
    bool ApplyConsentFilter = true,
    string? SourceReferenceType = null,
    Guid? SourceReferenceId = null,
    string? ConsentChannel = null,
    string? ConsentPurpose = null,
    DateTimeOffset? EffectiveAt = null,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? ReasonCodes = null) : IRequest<Response<CampaignTargetSnapshotResultDto>>;
