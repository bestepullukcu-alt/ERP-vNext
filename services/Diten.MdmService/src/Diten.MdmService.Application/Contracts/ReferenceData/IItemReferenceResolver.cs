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

/// <summary>
/// MOD-0290-FU04 S2 FIX1 item 1 — the same lists for a caller with NO user request (the approval worker): MDM's trusted
/// service identity for the given tenant (the GSKU workflow resolver's pattern: tenant + service token, one refresh on a
/// 401). A refusal of that identity (401 / 403, or an identity Auth will not issue) is a CONFIGURATION error, answered with
/// its own status and code — never "the provider is down, wait".
/// </summary>
public interface IWorkflowItemReferenceResolver
{
    Task<ItemReferenceResolveResult> ResolveLatestAsync(
        Guid tenantId,
        IReadOnlyList<ItemReferenceRequest> requests,
        CancellationToken cancellationToken = default);
}

/// <summary>The workflow resolver bound to one tenant — the shape the item rules read (<see cref="IItemReferenceResolver"/>).</summary>
public sealed class TenantBoundItemReferenceResolver(IWorkflowItemReferenceResolver resolver, Guid tenantId)
    : IItemReferenceResolver
{
    public Task<ItemReferenceResolveResult> ResolveLatestAsync(IReadOnlyList<ItemReferenceRequest> requests,
        CancellationToken cancellationToken = default) =>
        resolver.ResolveLatestAsync(tenantId, requests, cancellationToken);
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
    // S2 FIX1 item 1: the provider's refusals of MDM's own credentials — configuration, not an outage.
    public const string Unauthenticated = "REFERENCE_UNAUTHENTICATED";
    public const string Forbidden = "REFERENCE_FORBIDDEN";
    public const string ConfigurationInvalid = "REFERENCE_PROVIDER_CONFIGURATION_INVALID";
    public const string ServiceIdentityRefused = "REFERENCE_SERVICE_IDENTITY_REFUSED";

    /// <summary>A refusal of MDM's own credentials or configuration — a person must fix it; waiting never ends it.</summary>
    public bool IsConfigurationRefusal => !IsSuccessful
        && (StatusCode is 401 or 403
            || FailureCode is Unauthenticated or Forbidden or ConfigurationInvalid or ServiceIdentityRefused);

    public static ItemReferenceResolveResult Success(IReadOnlyList<ItemReferenceSelection> selections) =>
        new(true, 200, null, selections, []);

    public static ItemReferenceResolveResult Missing(IReadOnlyList<ItemReferenceRequest> notFound) =>
        new(false, 404, ValueNotFound, [], notFound);

    public static ItemReferenceResolveResult Fail(int statusCode, string failureCode) =>
        new(false, statusCode, failureCode, [], []);
}
