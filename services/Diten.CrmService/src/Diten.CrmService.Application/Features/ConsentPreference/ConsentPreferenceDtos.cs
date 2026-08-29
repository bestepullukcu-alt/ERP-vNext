namespace Diten.CrmService.Application.Features.ConsentPreference;

/// <summary>MOD-0164 FU02 read model for a consent record (list + detail). TenantId is never echoed — it is
/// server-resolved from the JWT claim and never accepted from a payload.</summary>
public sealed record ConsentRecordDto(
    Guid ConsentId,
    string SubjectType,
    Guid SubjectId,
    string? ScopeType,
    Guid? ScopeId,
    string Channel,
    string Purpose,
    string LegalBasis,
    string ConsentStatus,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Source,
    ConsentEvidenceRefDto? EvidenceRef,
    string? WithdrawalReason,
    string? Notes,
    IReadOnlyList<ConsentExternalReferenceDto> ExternalReferences,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

/// <summary>MOD-0028/MOD-0029 evidence pointer as echoed back. A reference only — no file content, no render URL and
/// no copy is produced by MOD-0164.</summary>
public sealed record ConsentEvidenceRefDto(
    string RefType,
    Guid RefId,
    string SourceModule,
    string? RefCode);

/// <summary>External/legacy identity as echoed back (same contract as MOD-0290-FU01 / MOD-0165-FU02).</summary>
public sealed record ConsentExternalReferenceDto(
    string SourceSystem,
    string ExternalId,
    string? ExternalCode,
    string? ExternalName,
    DateTimeOffset? ImportedAt,
    bool IsPrimary);

public sealed record ConsentRecordListDto(
    IReadOnlyList<ConsentRecordDto> Items,
    int Total);

/// <summary>MOD-0164 FU02 read model for a preference record.</summary>
public sealed record PreferenceRecordDto(
    Guid PreferenceId,
    string SubjectType,
    Guid SubjectId,
    string Channel,
    string PreferenceType,
    string PreferenceValue,
    int Priority,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Source,
    string? Notes,
    IReadOnlyList<ConsentExternalReferenceDto> ExternalReferences,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record PreferenceRecordListDto(
    IReadOnlyList<PreferenceRecordDto> Items,
    int Total);

/// <summary>Inbound external-reference line shared by the consent and preference write commands.</summary>
public sealed record ConsentExternalReferenceInput(
    string SourceSystem,
    string ExternalId,
    string? ExternalCode = null,
    string? ExternalName = null,
    DateTimeOffset? ImportedAt = null,
    bool IsPrimary = false);

/// <summary>Inbound evidence pointer. Format-level validated only in FU02 (no document-master lookup).</summary>
public sealed record ConsentEvidenceRefInput(
    string RefType,
    Guid RefId,
    string SourceModule,
    string? RefCode = null);
