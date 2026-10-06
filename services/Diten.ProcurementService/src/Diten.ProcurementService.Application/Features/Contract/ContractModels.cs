using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.Contract;

// ── Contract version sabiti (CONTRACTING owned contract; contracting.openapi.yaml) ──
public static class ContractingContract
{
    public const string Version = "v1";

    /// <summary>
    /// ASSUMPTION-0144-04: currency boş bırakılırsa server LE base currency ile doldurulur. LE-base-currency seam
    /// (DEC-INV-18) bu dilimde bağlı değil; permissive-seam deseniyle (bkz. IProductReferenceValidator) server default
    /// kullanılır. Gerçek LE base currency servisi bağlanınca yalnız bu doldurma noktası değişir; kullanıcı uydurması
    /// DEĞİL — boş currency için deterministik sunucu varsayılanı.
    /// </summary>
    public const string DefaultCurrency = "USD";
}

// ══ READ DTOs (contracting.openapi.yaml şekilleriyle uyumlu) ═══════════════════════

/// <summary>Contract.clauses[] öğesi — contract ClauseRef {clauseId, deviation?, deviationText?}.</summary>
public sealed record ClauseRefDto(
    string ClauseId,
    bool? Deviation,
    string? DeviationText);

/// <summary>Contract read DTO — contract Contract {contractId, supplierId, rfxId?, title, status, effectiveFrom,
/// effectiveTo?, currency?, clauses[], workflowInstanceId?, contractVersion}. (Id/Version house-style zarf alanı;
/// concurrency için.)</summary>
public sealed record ContractDto(
    Guid Id,
    string ContractId,
    string SupplierId,
    string? RfxId,
    string Title,
    ContractStatus Status,
    string EffectiveFrom,
    string? EffectiveTo,
    string? Currency,
    IReadOnlyList<ClauseRefDto> Clauses,
    string? WorkflowInstanceId,
    IReadOnlyList<string> EvidenceRefs,
    int Version,
    string ContractVersion);

/// <summary>Sayfalı liste sonucu (contract listContracts {items, nextCursor, contractVersion}).</summary>
public sealed record ContractListResultDto(
    IReadOnlyList<ContractDto> Items,
    string? NextCursor,
    string ContractVersion);

// ══ REQUEST / INPUT records (controller body binding — contract ContractUpsert/ClauseRef) ══

/// <summary>contract ClauseRef request satırı.</summary>
public sealed record ClauseRefInput(
    string ClauseId,
    bool? Deviation,
    string? DeviationText);

/// <summary>contract ContractUpsert body. TenantId/LegalEntityId server-resolved (payload'da YOK); Idempotency-Key header.</summary>
public sealed record ContractUpsertBody(
    string SupplierId,
    string? RfxId,
    string Title,
    string EffectiveFrom,
    string? EffectiveTo,
    string? Currency,
    List<ClauseRefInput>? Clauses,
    List<string>? EvidenceRefs);
