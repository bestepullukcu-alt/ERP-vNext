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

    // WP-MDM-BRAND-ACCEPT-01 FIX2 — item 2: a new link to a brand that is not active or not inside its validity window
    // (409); items 7, 18: the store could not write atomically, nothing was written (503).
    public const string BrandNotLinkable = "brand_not_linkable";
    public const string StoreUnavailable = "store_unavailable";

    // FIX2 (M5) — a field-shape rule failed (400); the field follows the code (see BrandProductFieldRefusal).
    public const string FieldInvalid = "field_invalid";

    // FIX3 (M-2) — the commit's result stayed unknown (503): the write may or may not have landed; repeat the same request.
    public const string WriteOutcomeUnknown = "write_outcome_unknown";

    public static readonly IReadOnlyList<string> All =
    [
        BrandNotFound, ProductNotFound, BrandCodeDuplicate, ProductCodeDuplicate, BrandArchived, RecordArchived,
        CodeImmutable, InvalidBrandStatus, InvalidProductStatus, InvalidProductType, InvalidDosageForm,
        InvalidUnitOfMeasure, InvalidAtcCode, ArchivedStatusNotAssignable, InvalidEffectiveWindow,
        ExternalReferencePrimaryConflict, ExternalReferenceDuplicate, IndicationRefDuplicate,
        VersionConflict, ExpectedVersionRequired, ExternalReferenceInUse, ListQueryInvalid,
        BrandNotLinkable, StoreUnavailable, FieldInvalid, WriteOutcomeUnknown
    ];
}

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX2 (M5) — a field-shape refusal: <c>field_invalid: {Field} — {detail}</c>. <c>{Field}</c> is the
/// request's property name (BrandCode, Description, SourceSystem, …), the token a screen localizes; the detail is English
/// for logs and API callers.
/// </summary>
public static class BrandProductFieldRefusal
{
    public static string Message(string field, string detail) => $"{BrandProductReasonCodes.FieldInvalid}: {field} — {detail}";

    public static string Required(string field) => Message(field, "is required.");

    public static string TooLong(string field, int maximum) => Message(field, $"must be at most {maximum} characters.");
}

public static class BrandProductFailures
{
    public static Response<T> Fail<T>(string reasonCode, string message, int statusCode)
        => Response<T>.Fail($"{reasonCode}: {message}", statusCode);

    /// <summary>
    /// FIX3 (M-2) — the one answer for a commit whose result stayed unknown: named, never a 500 (the LSKU FIX4 shape, 503 of
    /// its "202 or 503"): a 2xx would read as "saved" to every screen that checks the status, and this write is not
    /// confirmed. The client repeats the same request: a create then meets its own code, an edit its own version.
    /// </summary>
    public static Response<T> OutcomeUnknown<T>() => Fail<T>(
        BrandProductReasonCodes.WriteOutcomeUnknown,
        "The change was sent but its result could not be confirmed. Reload the record and repeat the same request if it is not there.", 503);

    /// <summary>FIX2 (items 7, 18) — the one answer for a write the store could not make atomically.</summary>
    public static Response<T> StoreUnavailable<T>() => Fail<T>(
        BrandProductReasonCodes.StoreUnavailable,
        "The master data store cannot write this change safely right now. Nothing was saved; try again later.", 503);
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

            // FIX3 (M-6) — the pair key keeps the two parts apart: ("SAP","1X") and ("SAP1","X") are different pairs.
            if (!seenPairs.Add(PairKey(source, externalId)))
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

    /// <summary>
    /// FIX2 (R4, item 5) — what an edit changes in the record's references: <c>Added</c> = pairs the record did not carry
    /// (only these are checked against other records and reserved), <c>Removed</c> = pairs it no longer carries (their
    /// reservation, if this record holds one, is released). Pairs compare trimmed and case-insensitively — the same
    /// rule as the in-use check — so re-casing a reference is neither an add nor a remove.
    /// </summary>
    public static (IReadOnlyList<BrandProductExternalReference> Added, IReadOnlyList<BrandProductExternalReference> Removed) Diff(
        IReadOnlyCollection<BrandProductExternalReference> incoming, IReadOnlyCollection<BrandProductExternalReference> stored)
    {
        var storedKeys = stored.Select(x => PairKey(x.SourceSystem, x.ExternalId)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var incomingKeys = incoming.Select(x => PairKey(x.SourceSystem, x.ExternalId)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (
            incoming.Where(x => !storedKeys.Contains(PairKey(x.SourceSystem, x.ExternalId))).ToList(),
            stored.Where(x => !incomingKeys.Contains(PairKey(x.SourceSystem, x.ExternalId))).ToList());
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
        => Domain.Vocabulary.BrandProductCalendar.IsValidWindow(effectiveFrom, effectiveTo);
}
