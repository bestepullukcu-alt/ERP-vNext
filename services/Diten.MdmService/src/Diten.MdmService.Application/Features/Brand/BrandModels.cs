using Diten.MdmService.Application.Features.BrandProductContract;

namespace Diten.MdmService.Application.Features.Brand;

// MOD-0290-FU02 — every Brand DTO/ViewModel lives in this single file (Golden Reference convention).
//
// TenantId is ABSENT from every request shape by design: it is resolved server-side from the tenant context /
// JWT claim and can never be supplied by a caller.

/// <summary>
/// Shared write payload for Create + Update. `BrandCode` is honoured on create only — on update the stored
/// code wins, because FU01 §3 makes the code stable (renames go through BrandName).
/// `BrandStatus` cannot be set to `archived` here; archiving is a separate endpoint.
/// </summary>
public sealed record BrandWriteRequest(
    string BrandCode,
    string BrandName,
    string BrandStatus,
    string? Description,
    Guid? OwnerCompanyId,
    Guid? BusinessUnitId,
    Guid? TherapeuticAreaId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<BrandProductExternalReferenceDto>? ExternalReferences,
    // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 2) — the version the edit was made on. Required on update (a stale one is 409
    // version_conflict and nothing is written); ignored on create.
    int? ExpectedVersion = null);

public sealed record BrandDetailDto(
    Guid BrandId,
    string BrandCode,
    string BrandName,
    string BrandStatus,
    string? Description,
    Guid? OwnerCompanyId,
    Guid? BusinessUnitId,
    Guid? TherapeuticAreaId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<BrandProductExternalReferenceDto> ExternalReferences,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    string? CreatedBy,
    string? UpdatedBy,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    // FIX1 (item 3) — the brand's products that are NOT archived; read on the detail only (null in a list row).
    int? LiveProductCount = null);

/// <summary>
/// The brand list. <c>TotalCount</c> counts every row that matches the filters. FIX1 (item 6) — a PAGED read (one that
/// sends <c>length</c>) returns only that page in <c>Items</c>, says which page it is, and carries the
/// <c>ArchivedWithLiveProducts</c> indicator (FIX1 item 3: archived brands that still have products in use); a read
/// without <c>length</c> returns every matching row exactly as before, and those three stay null.
/// </summary>
public sealed record BrandListResultDto(
    IReadOnlyList<BrandDetailDto> Items,
    int TotalCount,
    int? Start = null,
    int? Length = null,
    int? ArchivedWithLiveProducts = null);

/// <summary>Brand-scoped product row for the Brand detail Products tab. Read-only projection; no mutation here.</summary>
public sealed record BrandProductRowDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string ProductStatus,
    string? ProductType,
    bool IsArchived,
    DateTimeOffset? UpdatedAt);

public static class BrandMappings
{
    /// <summary>Applies a write request onto an entity. Never touches BrandCode, archive state or audit identity.</summary>
    public static void Apply(Domain.Entities.Brand entity, BrandWriteRequest request)
    {
        entity.BrandName = request.BrandName.Trim();
        entity.BrandStatus = request.BrandStatus.Trim().ToLowerInvariant();
        entity.Description = BrandProductExternalReferences.Clean(request.Description);
        entity.OwnerCompanyId = Normalize(request.OwnerCompanyId);
        entity.BusinessUnitId = Normalize(request.BusinessUnitId);
        entity.TherapeuticAreaId = Normalize(request.TherapeuticAreaId);
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.ExternalReferences = BrandProductExternalReferences.ToEntities(request.ExternalReferences);
    }

    public static BrandDetailDto ToDetailDto(Domain.Entities.Brand entity)
        => new(
            entity.Id,
            entity.BrandCode,
            entity.BrandName,
            entity.BrandStatus,
            entity.Description,
            entity.OwnerCompanyId,
            entity.BusinessUnitId,
            entity.TherapeuticAreaId,
            entity.EffectiveFrom,
            entity.EffectiveTo,
            BrandProductExternalReferences.ToDtos(entity.ExternalReferences),
            entity.IsArchived,
            entity.ArchivedAt,
            entity.ArchivedBy,
            entity.CreatedBy,
            entity.UpdatedBy,
            entity.Version,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static BrandProductRowDto ToBrandProductRow(Domain.Entities.Product product)
        => new(
            product.Id,
            product.ProductCode,
            product.ProductName,
            product.ProductStatus,
            product.ProductType,
            product.IsArchived,
            product.UpdatedAt);

    /// <summary>Canonical brand code form: trimmed + upper-cased, so casing can never create a duplicate.</summary>
    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static Guid? Normalize(Guid? value) => value == Guid.Empty ? null : value;
}
