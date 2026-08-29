using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Vocabulary;

namespace Diten.MdmService.Application.Features.Product;

// MOD-0290-FU02 — every Product DTO/ViewModel lives in this single file (Golden Reference convention).
// TenantId is absent from every request shape: it is resolved server-side and never accepted from a caller.

/// <summary>
/// Shared write payload for Create + Update. `ProductCode` is honoured on create only (FU01 §4: the code is
/// stable). `ProductStatus` cannot be set to `archived` here. `BrandId` is optional (FU01 §4.1).
/// </summary>
public sealed record ProductWriteRequest(
    string ProductCode,
    string ProductName,
    string ProductStatus,
    Guid? BrandId,
    string? ProductType,
    string? DosageForm,
    string? Strength,
    string? PackSize,
    string? UnitOfMeasure,
    string? ATCCode,
    Guid? TherapeuticAreaId,
    IReadOnlyList<Guid>? IndicationRefs,
    string? Description,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<BrandProductExternalReferenceDto>? ExternalReferences);

public sealed record ProductDetailDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string ProductStatus,
    Guid? BrandId,
    string? ProductType,
    string? DosageForm,
    string? Strength,
    string? PackSize,
    string? UnitOfMeasure,
    string? ATCCode,
    Guid? TherapeuticAreaId,
    IReadOnlyList<Guid> IndicationRefs,
    string? Description,
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
    DateTimeOffset? UpdatedAt);

public sealed record ProductListResultDto(IReadOnlyList<ProductDetailDto> Items, int TotalCount);

/// <summary>
/// Payload-only business rules shared by Create and Update so the two write paths cannot drift. Rules that need
/// the repository (code uniqueness, brand link, archive state) stay in the handlers.
/// </summary>
public static class ProductWriteRules
{
    public static (string ReasonCode, string Message, int StatusCode)? Validate(ProductWriteRequest r)
    {
        if (BrandProductVocabulary.IsArchivedStatus(r.ProductStatus))
        {
            return (BrandProductReasonCodes.ArchivedStatusNotAssignable,
                "Product status 'archived' is set by the archive endpoint, not by a write request.", 400);
        }

        // `discontinued` deliberately fails here until FU01 §11 is amended (follow-up F5).
        if (!BrandProductVocabulary.IsProductStatus(r.ProductStatus))
        {
            return (BrandProductReasonCodes.InvalidProductStatus, $"Unknown product status '{r.ProductStatus}'.", 400);
        }

        if (!string.IsNullOrWhiteSpace(r.ProductType) && !BrandProductVocabulary.IsProductType(r.ProductType))
        {
            return (BrandProductReasonCodes.InvalidProductType, $"Unknown product type '{r.ProductType}'.", 400);
        }

        if (!string.IsNullOrWhiteSpace(r.DosageForm) && !BrandProductVocabulary.IsDosageForm(r.DosageForm))
        {
            return (BrandProductReasonCodes.InvalidDosageForm, $"Unknown dosage form '{r.DosageForm}'.", 400);
        }

        if (!string.IsNullOrWhiteSpace(r.UnitOfMeasure) && !BrandProductVocabulary.IsUnitOfMeasure(r.UnitOfMeasure))
        {
            return (BrandProductReasonCodes.InvalidUnitOfMeasure, $"Unknown unit of measure '{r.UnitOfMeasure}'.", 400);
        }

        if (!BrandProductEffectiveWindow.IsValid(r.EffectiveFrom, r.EffectiveTo))
        {
            return (BrandProductReasonCodes.InvalidEffectiveWindow, "EffectiveTo cannot be earlier than EffectiveFrom.", 400);
        }

        var indications = r.IndicationRefs ?? [];
        if (indications.Where(x => x != Guid.Empty).Distinct().Count() != indications.Count(x => x != Guid.Empty))
        {
            return (BrandProductReasonCodes.IndicationRefDuplicate, "IndicationRefs must not contain duplicates.", 400);
        }

        if (BrandProductExternalReferences.Validate(r.ExternalReferences) is { } externalReferenceFailure)
        {
            return (externalReferenceFailure,
                "External references must be unique per (SourceSystem, ExternalId) with at most one primary per source system.",
                409);
        }

        return null;
    }
}

public static class ProductMappings
{
    public static void Apply(Domain.Entities.Product entity, ProductWriteRequest request)
    {
        entity.ProductName = request.ProductName.Trim();
        entity.ProductStatus = request.ProductStatus.Trim().ToLowerInvariant();
        entity.BrandId = Normalize(request.BrandId);
        entity.ProductType = Lower(request.ProductType);
        entity.DosageForm = Lower(request.DosageForm);
        entity.Strength = BrandProductExternalReferences.Clean(request.Strength);
        entity.PackSize = BrandProductExternalReferences.Clean(request.PackSize);
        entity.UnitOfMeasure = Lower(request.UnitOfMeasure);

        // Stored verbatim (upper-cased) as an EXTERNAL TAXONOMY POINTER. No ATC master is consulted or created.
        entity.ATCCode = BrandProductExternalReferences.Clean(request.ATCCode)?.ToUpperInvariant();

        entity.TherapeuticAreaId = Normalize(request.TherapeuticAreaId);
        entity.IndicationRefs = (request.IndicationRefs ?? []).Where(x => x != Guid.Empty).Distinct().ToList();
        entity.Description = BrandProductExternalReferences.Clean(request.Description);
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.ExternalReferences = BrandProductExternalReferences.ToEntities(request.ExternalReferences);
    }

    public static ProductDetailDto ToDetailDto(Domain.Entities.Product entity)
        => new(
            entity.Id,
            entity.ProductCode,
            entity.ProductName,
            entity.ProductStatus,
            entity.BrandId,
            entity.ProductType,
            entity.DosageForm,
            entity.Strength,
            entity.PackSize,
            entity.UnitOfMeasure,
            entity.ATCCode,
            entity.TherapeuticAreaId,
            entity.IndicationRefs,
            entity.Description,
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

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static Guid? Normalize(Guid? value) => value == Guid.Empty ? null : value;

    private static string? Lower(string? value)
        => BrandProductExternalReferences.Clean(value)?.ToLowerInvariant();
}
