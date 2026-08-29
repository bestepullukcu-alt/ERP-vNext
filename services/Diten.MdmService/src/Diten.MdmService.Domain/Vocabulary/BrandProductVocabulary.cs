namespace Diten.MdmService.Domain.Vocabulary;

// MOD-0290-FU02 — in-domain controlled vocabulary (pack §4.3, divergence D2).
//
// FU01 §10 wanted these to come from MOD-0048 reference sets, but FU01 F8 (publishing brand-status /
// product-status / product-dosage-form / product-uom) is still OPEN and MOD-0048 publish is explicitly
// out of scope for FU02. Without an in-domain vocabulary the feature could not run at all, so v1 ships
// these constants and publishes them through the capability contract — exactly the pattern MOD-0164-FU02
// used in the same chain. The UI must read the vocabulary from the contract, never hardcode it, so the
// MOD-0048 reconciliation (F4) can swap the source without touching the frontend.
//
// `discontinued` is deliberately ABSENT from ProductStatuses: FU01 §11 locked the lifecycle set to four
// values and extending it needs an FU01 amendment (follow-up F5).
public static class BrandProductVocabulary
{
    public const string StatusDraft = "draft";
    public const string StatusActive = "active";
    public const string StatusInactive = "inactive";
    public const string StatusArchived = "archived";

    public static readonly IReadOnlyList<string> BrandStatuses =
        [StatusDraft, StatusActive, StatusInactive, StatusArchived];

    public static readonly IReadOnlyList<string> ProductStatuses =
        [StatusDraft, StatusActive, StatusInactive, StatusArchived];

    public static readonly IReadOnlyList<string> ProductTypes =
        ["medicine", "medical-device", "service", "training-material", "other"];

    public static readonly IReadOnlyList<string> DosageForms =
        ["tablet", "capsule", "syrup", "injection", "ointment", "cream", "drops", "inhaler", "patch", "other"];

    public static readonly IReadOnlyList<string> UnitsOfMeasure =
        ["mg", "g", "ml", "l", "iu", "piece", "box", "vial", "ampoule", "other"];

    public static bool IsBrandStatus(string? value) => Contains(BrandStatuses, value);

    public static bool IsProductStatus(string? value) => Contains(ProductStatuses, value);

    public static bool IsProductType(string? value) => Contains(ProductTypes, value);

    public static bool IsDosageForm(string? value) => Contains(DosageForms, value);

    public static bool IsUnitOfMeasure(string? value) => Contains(UnitsOfMeasure, value);

    /// <summary><c>archived</c> is reachable only through the archive endpoint, never through a write payload.</summary>
    public static bool IsArchivedStatus(string? value)
        => string.Equals(value?.Trim(), StatusArchived, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(IReadOnlyList<string> allowed, string? value)
        => !string.IsNullOrWhiteSpace(value)
           && allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
}
