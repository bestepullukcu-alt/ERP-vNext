using Diten.MdmService.Domain.Vocabulary;

namespace Diten.MdmService.Application.Features.BrandProductContract;

// MOD-0290-FU02 — capability contract consumed by the UI (and by any future consumer that needs to know what
// this master supports). The UI gates every action on these flags and reads every dropdown from `Vocabulary`,
// so the MOD-0048 reconciliation (F4) can change the vocabulary source without touching the frontend.

/// <summary>
/// The eight capability flags this feature publishes (pack §16.1). Forbidden flags — campaign/knowledge/visit/
/// route/frequency/recommendation/digital-detailing/workflow/segmentation runtime, ATC local master,
/// therapeutic-area flat set, indication master, item/SKU, UoM mapping, import/export, hard delete,
/// multi-brand — are ABSENT from this type entirely, not published as `false`. A consumer must not be able to
/// discover them at all.
/// </summary>
public sealed record BrandProductFeaturesDto(
    bool SupportsBrandManagement,
    bool SupportsProductManagement,
    bool SupportsBrandProductReference,
    bool SupportsBrandProductHierarchy,
    bool SupportsExternalReferences,
    bool SupportsArchiveLifecycle,
    bool SupportsEffectiveDating,
    bool SupportsContractDrivenUi);

public sealed record BrandProductVocabularyDto(
    IReadOnlyList<string> BrandStatuses,
    IReadOnlyList<string> ProductStatuses,
    IReadOnlyList<string> ProductTypes,
    IReadOnlyList<string> DosageForms,
    IReadOnlyList<string> UnitsOfMeasure);

public sealed record BrandProductContractDto(
    bool IsReady,
    BrandProductFeaturesDto Features,
    BrandProductVocabularyDto Vocabulary,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

public static class BrandProductContractFactory
{
    public static BrandProductContractDto Create() => new(
        IsReady: true,
        Features: new BrandProductFeaturesDto(
            SupportsBrandManagement: true,
            SupportsProductManagement: true,
            SupportsBrandProductReference: true,
            SupportsBrandProductHierarchy: true,
            SupportsExternalReferences: true,
            SupportsArchiveLifecycle: true,
            SupportsEffectiveDating: true,
            SupportsContractDrivenUi: true),
        Vocabulary: new BrandProductVocabularyDto(
            BrandProductVocabulary.BrandStatuses,
            BrandProductVocabulary.ProductStatuses,
            BrandProductVocabulary.ProductTypes,
            BrandProductVocabulary.DosageForms,
            BrandProductVocabulary.UnitsOfMeasure),
        ReasonCodes: BrandProductReasonCodes.All,
        Permissions:
        [
            "mdm.brands.read", "mdm.brands.create", "mdm.brands.update", "mdm.brands.archive",
            "mdm.products.read", "mdm.products.create", "mdm.products.update", "mdm.products.archive"
        ],
        // Stated honestly so the UI can disable rather than fake, and so consumers do not assume more than
        // this slice delivers.
        Limitations:
        [
            "hard-delete-not-supported",
            "vocabulary-served-in-domain-pending-mod-0048-reconciliation",
            "product-status-discontinued-not-authorized",
            "reference-ids-are-format-level-only-no-master-resolution",
            "atc-code-is-external-taxonomy-pointer-no-local-master",
            "therapeutic-area-is-concept-reference-not-flat-reference-set",
            "multi-brand-product-not-supported",
            "product-family-hierarchy-not-supported",
            "item-sku-uom-identifier-management-out-of-scope",
            "import-export-engine-out-of-scope"
        ]);
}
