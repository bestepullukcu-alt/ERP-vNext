using Diten.MdmService.Domain.Entities;
using Diten.Shared.Core;

namespace Diten.MdmService.Application.Features.BrandProductContract;

// MOD-0290-FU02 — pieces shared by the Brand and Product features so the two write paths cannot drift:
// the external-reference contract (FU01 §12), the reason-code dictionary, and the failure builders.

/// <summary>External reference payload/DTO — identical shape on request and response (FU01 §12).</summary>
public sealed record BrandProductExternalReferenceDto(
    string SourceSystem,
    string ExternalId,
    string? ExternalCode,
    string? ExternalName,
    DateTimeOffset? ImportedAt,
    bool IsPrimary);

/// <summary>
/// Machine-readable failure codes. `Response&lt;T&gt;` (Diten.Shared.Core) carries only a string error list and
/// is shared with MOD-0220, so widening it is out of this pack's repo scope. Reason codes are therefore emitted
/// as the leading token of the error string — `"brand_code_duplicate: ..."` — which keeps the existing envelope
/// intact while staying greppable for the UI and the smoke script.
/// </summary>
public static class BrandProductReasonCodes
{
    public const string BrandNotFound = "brand_not_found";
    public const string ProductNotFound = "product_not_found";
    public const string BrandCodeDuplicate = "brand_code_duplicate";
    public const string ProductCodeDuplicate = "product_code_duplicate";
    public const string BrandArchived = "brand_archived";
    public const string RecordArchived = "record_archived";
    public const string CodeImmutable = "code_immutable";
    public const string InvalidBrandStatus = "invalid_brand_status";
    public const string InvalidProductStatus = "invalid_product_status";
    public const string InvalidProductType = "invalid_product_type";
    public const string InvalidDosageForm = "invalid_dosage_form";
    public const string InvalidUnitOfMeasure = "invalid_unit_of_measure";
    public const string InvalidAtcCode = "invalid_atc_code";
    public const string ArchivedStatusNotAssignable = "archived_status_not_assignable";
    public const string InvalidEffectiveWindow = "invalid_effective_window";
    public const string ExternalReferencePrimaryConflict = "external_reference_primary_conflict";
    public const string ExternalReferenceDuplicate = "external_reference_duplicate";
    public const string IndicationRefDuplicate = "indication_ref_duplicate";

    // WP-MDM-BRAND-ACCEPT-01 FIX1 — item 2: an edit made on an older read (409) and an edit that does not say which read
    // it was made on (400); item 10: an external reference another record already carries (409); item 6: a list sort key
    // or paging value the server does not accept (400).
    public const string VersionConflict = "version_conflict";
    public const string ExpectedVersionRequired = "expected_version_required";
    public const string ExternalReferenceInUse = "external_reference_in_use";
    public const string ListQueryInvalid = "list_query_invalid";

    public static readonly IReadOnlyList<string> All =
    [
        BrandNotFound, ProductNotFound, BrandCodeDuplicate, ProductCodeDuplicate, BrandArchived, RecordArchived,
        CodeImmutable, InvalidBrandStatus, InvalidProductStatus, InvalidProductType, InvalidDosageForm,
        InvalidUnitOfMeasure, InvalidAtcCode, ArchivedStatusNotAssignable, InvalidEffectiveWindow,
        ExternalReferencePrimaryConflict, ExternalReferenceDuplicate, IndicationRefDuplicate,
        VersionConflict, ExpectedVersionRequired, ExternalReferenceInUse, ListQueryInvalid
    ];
}

public static class BrandProductFailures
{
    public static Response<T> Fail<T>(string reasonCode, string message, int statusCode)
        => Response<T>.Fail($"{reasonCode}: {message}", statusCode);
}

/// <summary>
/// External-reference normalisation + guards. Silent merge is forbidden (FU01 §12): duplicates and a second
/// primary for the same source system surface as explicit conflicts instead of being quietly collapsed.
/// </summary>
public static class BrandProductExternalReferences
{
    /// <summary>Returns a reason code when the collection is invalid, otherwise <c>null</c>.</summary>
    public static string? Validate(IReadOnlyList<BrandProductExternalReferenceDto>? references)
    {
        if (references is null || references.Count == 0)
        {
            return null;
        }

        var seenPrimary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in references)
        {
            var source = reference.SourceSystem?.Trim() ?? string.Empty;
            var externalId = reference.ExternalId?.Trim() ?? string.Empty;

            if (!seenPairs.Add($"{source}{externalId}"))
            {
                return BrandProductReasonCodes.ExternalReferenceDuplicate;
            }

            if (reference.IsPrimary && !seenPrimary.Add(source))
            {
                return BrandProductReasonCodes.ExternalReferencePrimaryConflict;
            }
        }

        return null;
    }

    public static List<BrandProductExternalReference> ToEntities(IReadOnlyList<BrandProductExternalReferenceDto>? references)
        => (references ?? [])
            .Select(x => new BrandProductExternalReference
            {
                SourceSystem = x.SourceSystem.Trim(),
                ExternalId = x.ExternalId.Trim(),
                ExternalCode = Clean(x.ExternalCode),
                ExternalName = Clean(x.ExternalName),
                ImportedAt = x.ImportedAt ?? DateTimeOffset.UtcNow,
                IsPrimary = x.IsPrimary
            })
            .ToList();

    public static IReadOnlyList<BrandProductExternalReferenceDto> ToDtos(IEnumerable<BrandProductExternalReference> references)
        => references
            .Select(x => new BrandProductExternalReferenceDto(
                x.SourceSystem, x.ExternalId, x.ExternalCode, x.ExternalName, x.ImportedAt, x.IsPrimary))
            .ToList();

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// WP-MDM-BRAND-ACCEPT-01 FIX1 (item 10) — an edit never re-stamps where a reference came from: a reference the record
    /// already carried (same source system and external id) keeps its stored <c>ImportedAt</c>, whatever the request says;
    /// only a reference new to the record takes the request's value (or now).
    /// </summary>
    public static void PreserveImportedAt(
        IEnumerable<BrandProductExternalReference> incoming, IEnumerable<BrandProductExternalReference> stored)
    {
        var known = stored
            .GroupBy(x => PairKey(x.SourceSystem, x.ExternalId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().ImportedAt, StringComparer.OrdinalIgnoreCase);
        foreach (var reference in incoming)
        {
            if (known.TryGetValue(PairKey(reference.SourceSystem, reference.ExternalId), out var importedAt))
            {
                reference.ImportedAt = importedAt;
            }
        }
    }

    /// <summary>
    /// FIX1 (item 10) — the first reference of the request that ANOTHER record already carries (the message names that
    /// record; the caller adds <see cref="BrandProductReasonCodes.ExternalReferenceInUse"/>), asked through
    /// <paramref name="ownerOf"/> (which answers the other record's code, or null). Null when every reference is free.
    /// Answers in request order, so the same request always names the same conflict.
    /// </summary>
    public static async Task<(string Message, int StatusCode)?> FirstInUseAsync(
        IEnumerable<BrandProductExternalReference> references, Func<string, string, Task<string?>> ownerOf)
    {
        foreach (var reference in references)
        {
            if (await ownerOf(reference.SourceSystem, reference.ExternalId) is { } owner)
            {
                return ($"The external reference {reference.SourceSystem}/{reference.ExternalId} is already used by {owner}.", 409);
            }
        }

        return null;
    }

    private static string PairKey(string sourceSystem, string externalId) => $"{sourceSystem.Trim()}\u001F{externalId.Trim()}";
}

/// <summary>
/// Effective-window guard. `EffectiveFrom`/`EffectiveTo` are stored as BSON arrays (DateTimeOffset), which is
/// why they are compared on <c>.Date</c> here and why they are never indexed or sorted together — two parallel
/// arrays in one index/sort raise "cannot sort with keys that are parallel arrays".
/// </summary>
public static class BrandProductEffectiveWindow
{
    public static bool IsValid(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
        => effectiveTo is null || effectiveTo.Value.Date >= effectiveFrom.Date;
}
