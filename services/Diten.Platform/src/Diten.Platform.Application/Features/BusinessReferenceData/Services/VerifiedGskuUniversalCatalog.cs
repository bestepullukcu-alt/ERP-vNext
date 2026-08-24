using Diten.Platform.Application.Features.BusinessReferenceData.Models;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

/// <summary>
/// Platform-owned universal GSKU reference contract. These values are deployment-versioned,
/// immutable and identical for every tenant; they are not tenant-stewarded BRD records.
/// </summary>
public static class VerifiedGskuUniversalCatalog
{
    public const string CatalogVersion = "GSKU-UNIVERSAL-V1";
    public const int CatalogVersionNumber = 1;
    public const string ResolutionMode = "LATEST";
    public const string PackApplicabilitySetCode = "pack-applicability";
    public const string UomSetCode = "uom";
    public const string ScalarQuantityApplies = "SCALAR_QUANTITY_APPLIES";

    public static readonly Guid PackApplicabilityCatalogVersionId =
        Guid.Parse("a527ad40-5cd9-4f76-9e18-3c531fb9a001");

    public static readonly Guid UomCatalogVersionId =
        Guid.Parse("a527ad40-5cd9-4f76-9e18-3c531fb9a002");

    public static IReadOnlyList<BusinessReferenceDataVerifiedUom> Uoms { get; } =
        Array.AsReadOnly<BusinessReferenceDataVerifiedUom>(
    [
        new("C62", "One", 10, 0),
        new("GRM", "Gram", 20, 3),
        new("KGM", "Kilogram", 30, 3),
        new("MLT", "Millilitre", 40, 3),
        new("LTR", "Litre", 50, 3)
    ]);

    public static bool IsSupported(string setCode, string valueCode) => setCode switch
    {
        PackApplicabilitySetCode => string.Equals(
            valueCode,
            ScalarQuantityApplies,
            StringComparison.Ordinal),
        UomSetCode => Uoms.Any(x => string.Equals(x.Code, valueCode, StringComparison.Ordinal)),
        _ => false
    };

    public static Guid GetVersionId(string setCode) => setCode switch
    {
        PackApplicabilitySetCode => PackApplicabilityCatalogVersionId,
        UomSetCode => UomCatalogVersionId,
        _ => throw new ArgumentOutOfRangeException(nameof(setCode), setCode, "Unsupported universal catalog set.")
    };
}
