namespace Diten.ProcurementService.Domain.Entities;

/// <summary>
/// Clause library entity (MOD-0144 §4). Standart clause'ların SoR'u; sözleşmeler (Contract.Clauses[].ClauseId) buradan
/// referanslar. Tenant-owned; her sorgu TenantId + LegalEntityId + IsDeleted=false ile filtrelenir. (Category+Title)
/// tenant+LE bazında unique → 409 DUPLICATE_CLAUSE. Clause IMMUTABLE kabul edilir (ASSUMPTION-0144-03): düzeltme =
/// yeni clause/versiyon; update/delete YOK (permission seti bununla tutarlı). Idempotent create (Idempotency-Key).
/// </summary>
public sealed class Clause : EntityBase
{
    /// <summary>Public code — server-assigned; unique/tenant+LE (ör. CL-...).</summary>
    public string ClauseId { get; set; } = string.Empty;

    /// <summary>Zorunlu; (Category+Title) tenant+LE bazında unique.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Trim, zorunlu; (Category+Title) tenant+LE bazında unique → 409 DUPLICATE_CLAUSE.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Clause gövdesi — zorunlu.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Create idempotency anahtarı (Idempotency-Key header). Aynı key ile replay → yan etki yok.</summary>
    public string? IdempotencyKey { get; set; }
}
