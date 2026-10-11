namespace Diten.MdmService.Application.Contracts.ReferenceData;

// MOD-0290-FU04 S1: the three universal item lists of MOD-0048 (`item-uom`, `storage-condition`, `material-type`;
// MOD-0048-FU01 §4 "Universal Item Reference Lists for MOD-0290-FU04"). Exact LATEST resolution of every requested code
// in one call; a code the list does not hold is named (REFERENCE_VALUE_NOT_FOUND), an unreachable or untrusted provider
// is an outage, never a pass.
public interface IItemReferenceResolver
{
    Task<ItemReferenceResolveResult> ResolveLatestAsync(
        IReadOnlyList<ItemReferenceRequest> requests,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// MOD-0290-FU04 S5b (BL-600): every value of the asked sets, for the item / stock forms to offer as a choice — never
    /// the proof of a choice (the write resolves each chosen code exactly). A provider that cannot list answers an outage;
    /// one that does not know this call (a test fake) answers the same — never an empty list.
    /// </summary>
    Task<ItemReferenceEnumerationResult> EnumerateLatestAsync(
        IReadOnlyList<string> setCodes,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ItemReferenceEnumerationResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"));
}

public sealed record ItemReferenceEnumeratedValue(
    string ValueCode,
    string DisplayName,
    int SortOrder,
    IReadOnlyDictionary<string, string?> Attributes);

public sealed record ItemReferenceEnumeratedSet(
    string SetCode,
    string CatalogVersion,
    Guid CatalogVersionId,
    int CatalogVersionNumber,
    IReadOnlyList<ItemReferenceEnumeratedValue> Values);

public sealed record ItemReferenceEnumerationResult(
    bool IsSuccessful,
    int StatusCode,
    string? FailureCode,
    IReadOnlyList<ItemReferenceEnumeratedSet> Sets)
{
    public static ItemReferenceEnumerationResult Success(IReadOnlyList<ItemReferenceEnumeratedSet> sets) => new(true, 200, null, sets);

    public static ItemReferenceEnumerationResult Fail(int statusCode, string code) => new(false, statusCode, code, []);
}

public static class ItemReferenceSets
{
    public const string ItemUom = "item-uom";
    public const string StorageCondition = "storage-condition";
    public const string MaterialType = "material-type";

    // storage-condition attribute (FU01 §4): TEMPERATURE | PROTECTION — the O-2 rule reads it, never a code list.
    public const string ConditionKindAttribute = "ConditionKind";
    public const string TemperatureKind = "TEMPERATURE";
    public const string ProtectionKind = "PROTECTION";
}

public sealed record ItemReferenceRequest(string SetCode, string ValueCode);

public sealed record ItemReferenceSelection(
    string SetCode,
    string ValueCode,
    Guid CatalogVersionId,
    int CatalogVersionNumber,
    string ResolutionMode,
    DateTimeOffset ResolvedAtUtc,
    bool IsRetired,
    bool SelectableForNew,
    IReadOnlyDictionary<string, string?> Attributes);

public sealed record ItemReferenceResolveResult(
    bool IsSuccessful,
    int StatusCode,
    string? FailureCode,
    IReadOnlyList<ItemReferenceSelection> Selections,
    IReadOnlyList<ItemReferenceRequest> NotFound)
{
    public const string ValueNotFound = "REFERENCE_VALUE_NOT_FOUND";

    public static ItemReferenceResolveResult Success(IReadOnlyList<ItemReferenceSelection> selections) =>
        new(true, 200, null, selections, []);

    public static ItemReferenceResolveResult Missing(IReadOnlyList<ItemReferenceRequest> notFound) =>
        new(false, 404, ValueNotFound, [], notFound);

    public static ItemReferenceResolveResult Fail(int statusCode, string failureCode) =>
        new(false, statusCode, failureCode, [], []);
}
