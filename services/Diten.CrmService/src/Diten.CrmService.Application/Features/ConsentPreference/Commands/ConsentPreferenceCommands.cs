using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ConsentPreference.Commands;

/// <summary>
/// MOD-0164 FU02 consent write surface. <c>TenantId</c> is NEVER accepted from the payload (server-resolved from the
/// JWT claim). There is deliberately NO delete command — closing a record is
/// <see cref="ArchiveConsentRecordCommand"/> (soft lifecycle), so consent history including withdrawals stays
/// readable forever.
/// </summary>
public sealed record CreateConsentRecordCommand(
    string SubjectType,
    Guid SubjectId,
    string Channel,
    string Purpose,
    string LegalBasis,
    string ConsentStatus,
    DateTimeOffset EffectiveFrom,
    string Source,
    string? ScopeType = null,
    Guid? ScopeId = null,
    DateTimeOffset? EffectiveTo = null,
    ConsentEvidenceRefInput? EvidenceRef = null,
    string? WithdrawalReason = null,
    string? Notes = null,
    IReadOnlyList<ConsentExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<Guid>>;

/// <summary>
/// Full replace of the mutable fields of a consent record. The question dimensions — SubjectType/SubjectId, Channel,
/// Purpose, ScopeType/ScopeId — are IMMUTABLE: a different subject/channel/purpose/scope is a different record, never
/// an edit of this one, so a permission can never be silently repurposed. A status transition (e.g. granted →
/// withdrawn) IS allowed here and is audit stamped; it never deletes or rewrites history.
/// </summary>
public sealed record UpdateConsentRecordCommand(
    Guid ConsentId,
    string LegalBasis,
    string ConsentStatus,
    DateTimeOffset EffectiveFrom,
    string Source,
    DateTimeOffset? EffectiveTo = null,
    ConsentEvidenceRefInput? EvidenceRef = null,
    string? WithdrawalReason = null,
    string? Notes = null,
    IReadOnlyList<ConsentExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<bool>>;

/// <summary>Archives a consent record (ArchivedAt/By stamped). Excluded from evaluation; still readable as history.</summary>
public sealed record ArchiveConsentRecordCommand(Guid ConsentId) : IRequest<Response<bool>>;

/// <summary>MOD-0164 FU02 preference write surface. Same rules: no TenantId in the payload, no delete command.</summary>
public sealed record CreatePreferenceRecordCommand(
    string SubjectType,
    Guid SubjectId,
    string Channel,
    string PreferenceType,
    string PreferenceValue,
    int Priority,
    DateTimeOffset EffectiveFrom,
    string Source,
    DateTimeOffset? EffectiveTo = null,
    string? Notes = null,
    IReadOnlyList<ConsentExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields of a preference record. SubjectType/SubjectId, Channel and
/// PreferenceType are IMMUTABLE — a different restriction is a different record.</summary>
public sealed record UpdatePreferenceRecordCommand(
    Guid PreferenceId,
    string PreferenceValue,
    int Priority,
    DateTimeOffset EffectiveFrom,
    string Source,
    DateTimeOffset? EffectiveTo = null,
    string? Notes = null,
    IReadOnlyList<ConsentExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<bool>>;

public sealed record ArchivePreferenceRecordCommand(Guid PreferenceId) : IRequest<Response<bool>>;
