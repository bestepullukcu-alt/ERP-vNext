using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.ConsentPreference;

/// <summary>Aggregate ↔ DTO projection for MOD-0164 FU02. Reads never echo TenantId (server-resolved), and the
/// evidence pointer is projected as a reference only — no file content or render URL is produced.</summary>
public static class ConsentPreferenceMapper
{
    public static ConsentRecordDto ToDto(ConsentRecord record) => new(
        record.Id,
        record.SubjectType,
        record.SubjectId,
        record.ScopeType,
        record.ScopeId,
        record.Channel,
        record.Purpose,
        record.LegalBasis,
        record.ConsentStatus,
        record.EffectiveFrom,
        record.EffectiveTo,
        record.Source,
        record.EvidenceRef is null
            ? null
            : new ConsentEvidenceRefDto(
                record.EvidenceRef.RefType, record.EvidenceRef.RefId,
                record.EvidenceRef.SourceModule, record.EvidenceRef.RefCode),
        record.WithdrawalReason,
        record.Notes,
        record.ExternalReferences.Select(ToDto).ToList(),
        record.CreatedAt,
        record.CreatedBy,
        record.UpdatedAt,
        record.UpdatedBy,
        record.ArchivedAt,
        record.ArchivedBy,
        record.IsArchived());

    public static PreferenceRecordDto ToDto(PreferenceRecord record) => new(
        record.Id,
        record.SubjectType,
        record.SubjectId,
        record.Channel,
        record.PreferenceType,
        record.PreferenceValue,
        record.Priority,
        record.EffectiveFrom,
        record.EffectiveTo,
        record.Source,
        record.Notes,
        record.ExternalReferences.Select(ToDto).ToList(),
        record.CreatedAt,
        record.CreatedBy,
        record.UpdatedAt,
        record.UpdatedBy,
        record.ArchivedAt,
        record.ArchivedBy,
        record.IsArchived());

    public static ConsentExternalReferenceDto ToDto(ConsentExternalReference reference) => new(
        reference.SourceSystem,
        reference.ExternalId,
        reference.ExternalCode,
        reference.ExternalName,
        reference.ImportedAt,
        reference.IsPrimary);

    /// <summary>Inbound external-reference lines → stored value objects. <c>ImportedAt</c> is preserved when supplied
    /// (legacy history is never rewritten) and stamped with "now" only when the caller omitted it.</summary>
    public static List<ConsentExternalReference> ToEntities(
        IReadOnlyList<ConsentExternalReferenceInput>? inputs, DateTimeOffset now)
        => inputs is null
            ? new List<ConsentExternalReference>()
            : inputs.Select(i => new ConsentExternalReference
            {
                SourceSystem = i.SourceSystem.Trim(),
                ExternalId = i.ExternalId.Trim(),
                ExternalCode = string.IsNullOrWhiteSpace(i.ExternalCode) ? null : i.ExternalCode.Trim(),
                ExternalName = string.IsNullOrWhiteSpace(i.ExternalName) ? null : i.ExternalName.Trim(),
                ImportedAt = i.ImportedAt ?? now,
                IsPrimary = i.IsPrimary
            }).ToList();

    public static ConsentEvidenceRef? ToEntity(ConsentEvidenceRefInput? input)
        => input is null
            ? null
            : new ConsentEvidenceRef
            {
                RefType = ConsentEvidenceRefType.Normalize(input.RefType),
                RefId = input.RefId,
                SourceModule = ConsentEvidenceSourceModule.Normalize(input.SourceModule),
                RefCode = string.IsNullOrWhiteSpace(input.RefCode) ? null : input.RefCode.Trim()
            };
}
