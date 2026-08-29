using Diten.CrmService.Application.Features.ConsentPreference;

namespace Diten.CrmService.Api.Models.CRM;

/// <summary>
/// MOD-0164 FU02 request bodies. Note what is NOT here: <c>TenantId</c> — it is server-resolved from the JWT claim and
/// can never be supplied by a caller. On update, the question dimensions are also absent
/// (<c>SubjectType</c>/<c>SubjectId</c>, <c>Channel</c>, <c>Purpose</c>, <c>ScopeType</c>/<c>ScopeId</c>) because they
/// are immutable — a different question is a different record. There is no delete body: closing a record is the archive
/// endpoint.
/// </summary>
public sealed record CreateConsentRecordRequest(
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
    List<ConsentExternalReferenceInput>? ExternalReferences = null);

public sealed record UpdateConsentRecordRequest(
    string LegalBasis,
    string ConsentStatus,
    DateTimeOffset EffectiveFrom,
    string Source,
    DateTimeOffset? EffectiveTo = null,
    ConsentEvidenceRefInput? EvidenceRef = null,
    string? WithdrawalReason = null,
    string? Notes = null,
    List<ConsentExternalReferenceInput>? ExternalReferences = null);

/// <summary>Preference write body. <c>PreferenceType</c> is immutable after create, so it is absent from the update
/// body — a different restriction is a new record.</summary>
public sealed record CreatePreferenceRecordRequest(
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
    List<ConsentExternalReferenceInput>? ExternalReferences = null);

public sealed record UpdatePreferenceRecordRequest(
    string PreferenceValue,
    int Priority,
    DateTimeOffset EffectiveFrom,
    string Source,
    DateTimeOffset? EffectiveTo = null,
    string? Notes = null,
    List<ConsentExternalReferenceInput>? ExternalReferences = null);
