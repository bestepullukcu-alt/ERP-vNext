using Diten.MdmService.Application.Features.Brand;
using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Application.Features.Product;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Vocabulary;

namespace Diten.MdmService.Application.Tests.BrandProduct;

internal static class BrandProductTestData
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static BrandWriteRequest BrandRequest(
        string code = "BR-001",
        string name = "Almiba",
        string status = BrandProductVocabulary.StatusActive,
        DateTimeOffset? effectiveFrom = null,
        DateTimeOffset? effectiveTo = null,
        IReadOnlyList<BrandProductExternalReferenceDto>? externalReferences = null)
        => new(
            BrandCode: code,
            BrandName: name,
            BrandStatus: status,
            Description: "Test brand",
            OwnerCompanyId: null,
            BusinessUnitId: null,
            TherapeuticAreaId: null,
            EffectiveFrom: effectiveFrom ?? From,
            EffectiveTo: effectiveTo,
            ExternalReferences: externalReferences);

    public static ProductWriteRequest ProductRequest(
        string code = "PR-001",
        string name = "Almiba 10mg",
        string status = BrandProductVocabulary.StatusActive,
        Guid? brandId = null,
        string? productType = "medicine",
        string? dosageForm = null,
        string? unitOfMeasure = null,
        string? atcCode = "C09AA",
        IReadOnlyList<Guid>? indicationRefs = null,
        DateTimeOffset? effectiveFrom = null,
        DateTimeOffset? effectiveTo = null,
        IReadOnlyList<BrandProductExternalReferenceDto>? externalReferences = null)
        => new(
            ProductCode: code,
            ProductName: name,
            ProductStatus: status,
            BrandId: brandId,
            ProductType: productType,
            DosageForm: dosageForm,
            Strength: "10 mg",
            PackSize: "28",
            UnitOfMeasure: unitOfMeasure,
            ATCCode: atcCode,
            TherapeuticAreaId: null,
            IndicationRefs: indicationRefs,
            Description: "Test product",
            EffectiveFrom: effectiveFrom ?? From,
            EffectiveTo: effectiveTo,
            ExternalReferences: externalReferences);

    public static Domain.Entities.Brand Brand(
        Guid tenantId,
        string code = "BR-EXISTING",
        string name = "Existing brand",
        bool isArchived = false,
        Guid? id = null)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            BrandCode = code,
            BrandName = name,
            BrandStatus = isArchived ? BrandProductVocabulary.StatusArchived : BrandProductVocabulary.StatusActive,
            IsArchived = isArchived,
            EffectiveFrom = From
        };

    public static Domain.Entities.Product Product(
        Guid tenantId,
        string code = "PR-EXISTING",
        string name = "Existing product",
        bool isArchived = false,
        Guid? brandId = null,
        Guid? id = null)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            ProductCode = code,
            ProductName = name,
            ProductStatus = isArchived ? BrandProductVocabulary.StatusArchived : BrandProductVocabulary.StatusActive,
            IsArchived = isArchived,
            BrandId = brandId,
            EffectiveFrom = From
        };

    /// <summary>Reason codes are emitted as the leading token of the error string (see BrandProductFailures).</summary>
    public static bool HasReasonCode(IReadOnlyList<string> errors, string reasonCode)
        => errors.Any(x => x.StartsWith($"{reasonCode}:", StringComparison.Ordinal));
}
