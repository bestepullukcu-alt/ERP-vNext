using System.Collections.Concurrent;
using Diten.MdmService.Application.Contracts.Inventory;
using Diten.MdmService.Application.Contracts.ReferenceData;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S1: the three universal item lists as MOD-0048-FU01 §4 plans them (ITEM-UNIVERSAL-Vn), answering like
/// the verified-reference provider: exact LATEST selections with ConditionKind on storage conditions; an unknown code
/// is REFERENCE_VALUE_NOT_FOUND; an outage is an outage.
/// </summary>
public sealed class FakeItemReferences : IItemReferenceResolver
{
    public static readonly Guid VersionId = Guid.Parse("6f1d3c55-0e1b-4c38-9a77-1b1f2a9e0c01");
    public static readonly DateTimeOffset ResolvedAt = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, Dictionary<string, string?>> Sets = new()
    {
        // WP-PLT-ITEM-LISTS-02 (the FIX1 M5 pattern): the units are Platform's, from the shared contract fixture.
        [ItemReferenceSets.ItemUom] = ItemListContract.ItemUoms().ToDictionary(code => code, _ => (string?)null),
        // FIX1: the storage conditions (with their kind) and material types too.
        [ItemReferenceSets.StorageCondition] = ItemListContract.StorageConditions(),
        [ItemReferenceSets.MaterialType] = ItemListContract.MaterialTypes().ToDictionary(code => code, _ => (string?)null)
    };

    public ConcurrentQueue<IReadOnlyList<ItemReferenceRequest>> Calls { get; } = new();
    public bool Outage { get; set; }
    public bool Throw { get; set; }
    // FIX1 item 1: an answer whose storage conditions lack ConditionKind / carry no attributes at all.
    public bool OmitConditionKind { get; set; }
    public bool NullAttributes { get; set; }
    // FIX2 m1: a "not found" answer whose list holds a null entry.
    public bool NullNotFoundEntry { get; set; }
    public HashSet<string> Retired { get; } = new(StringComparer.Ordinal);
    // WP-PLT-ITEM-LISTS-01: the REAL Platform client a test puts in (the host's resolver is this fake).
    public IItemReferenceResolver? Override { get; set; }
    // S5b-FIX2 (2): an enumeration naming its first set twice; a set listed under its own catalog version.
    public bool RepeatEnumeratedSet { get; set; }
    public Dictionary<string, string> EnumeratedVersions { get; } = new(StringComparer.Ordinal);

    public void Reset()
    {
        RepeatEnumeratedSet = false;
        EnumeratedVersions.Clear();
        Outage = false;
        Throw = false;
        OmitConditionKind = false;
        NullAttributes = false;
        NullNotFoundEntry = false;
        Override = null;
        Retired.Clear();
    }

    public Task<ItemReferenceResolveResult> ResolveLatestAsync(IReadOnlyList<ItemReferenceRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (Override is { } real) return real.ResolveLatestAsync(requests, cancellationToken);
        Calls.Enqueue(requests);
        if (Throw) throw new HttpRequestException("item list provider down (test)");
        if (Outage) return Task.FromResult(ItemReferenceResolveResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"));
        var missing = requests.Where(request =>
            !Sets.TryGetValue(request.SetCode, out var set) || !set.ContainsKey(request.ValueCode)).ToList();
        if (NullNotFoundEntry) return Task.FromResult(ItemReferenceResolveResult.Missing([null!]));
        if (missing.Count > 0) return Task.FromResult(ItemReferenceResolveResult.Missing(missing));
        return Task.FromResult(ItemReferenceResolveResult.Success(requests.Select(request =>
        {
            var kind = Sets[request.SetCode][request.ValueCode];
            var retired = Retired.Contains(request.ValueCode);
            return new ItemReferenceSelection(request.SetCode, request.ValueCode, VersionId, 1, "LATEST", ResolvedAt,
                retired, !retired,
                NullAttributes ? null! : kind is null || OmitConditionKind
                    ? new Dictionary<string, string?>()
                    : new Dictionary<string, string?> { [ItemReferenceSets.ConditionKindAttribute] = kind });
        }).ToList()));
    }

    // S5b (BL-600): the lists to choose from, in their order, with the attributes the forms read; an outage is an outage.
    public ConcurrentQueue<IReadOnlyList<string>> EnumerationCalls { get; } = new();

    public Task<ItemReferenceEnumerationResult> EnumerateLatestAsync(IReadOnlyList<string> setCodes,
        CancellationToken cancellationToken = default)
    {
        if (Override is { } real) return real.EnumerateLatestAsync(setCodes, cancellationToken);
        EnumerationCalls.Enqueue(setCodes);
        if (Throw) throw new HttpRequestException("item list provider down (test)");
        if (Outage) return Task.FromResult(ItemReferenceEnumerationResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"));
        var asked = RepeatEnumeratedSet ? [.. setCodes, setCodes[0]] : setCodes;
        return Task.FromResult(ItemReferenceEnumerationResult.Success(asked.Select(set => new ItemReferenceEnumeratedSet(
            set, EnumeratedVersions.GetValueOrDefault(set, "ITEM-UNIVERSAL-V1"), VersionId, 1,
            Sets[set].Select((pair, index) => new ItemReferenceEnumeratedValue(pair.Key, pair.Key, (index + 1) * 10,
                set == ItemReferenceSets.ItemUom
                    ? new Dictionary<string, string?> { ["DimensionCode"] = "COUNT", ["MaximumDecimalPrecision"] = "0" }
                    : pair.Value is null
                        ? new Dictionary<string, string?>()
                        : new Dictionary<string, string?> { [ItemReferenceSets.ConditionKindAttribute] = pair.Value }))
                .ToList())).ToList()));
    }
}

/// <summary>
/// WP-PLT-ITEM-LISTS-02: the shared Platform ↔ MDM contract fixture (Platform's tests produce it from the real handler).
/// The fakes read their item-uom codes here, so a unit Platform adds or drops reaches them without a second list.
/// </summary>
internal static class ItemListContract
{
    public const string RelativeFixturePath =
        "services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/Fixtures/item-universal-resolve.contract.json";

    // FIX1: read in a static initializer — a moved or missing file must say WHICH file, not fail the host with a bare
    // TypeInitializationException.
    public static string FixturePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "services")))
            directory = directory.Parent;
        var path = directory is null ? null : Path.Combine(directory.FullName, RelativeFixturePath);
        return path is not null && File.Exists(path)
            ? path
            : throw new InvalidOperationException(
                $"The Platform ↔ MDM item-list contract fixture was not found: {RelativeFixturePath} (searched upward from "
                + $"{AppContext.BaseDirectory}). MDM's item fakes read their lists from it; if it moved, move this path too.");
    }

    private static System.Text.Json.Nodes.JsonNode Fixture() =>
        System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(FixturePath()))!;

    public static IReadOnlyList<string> ItemUoms() =>
        Fixture()["item_uoms"]!.AsArray().Select(code => (string)code!).ToList();

    public static IReadOnlyList<string> MaterialTypes() =>
        Fixture()["material_types"]!.AsArray().Select(code => (string)code!).ToList();

    public static Dictionary<string, string?> StorageConditions() =>
        Fixture()["storage_conditions"]!.AsObject().ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
}

/// <summary>MOD-0290-FU04 §8.3: the inventory team's tenant-wide "non-zero stock?" answer, as a test sets it.</summary>
public sealed class FakeStockAvailability : IInventoryStockAvailabilityClient
{
    public InventoryStockCheck Answer { get; set; } = InventoryStockCheck.Zero();
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;
    public bool Throw { get; set; }
    public ConcurrentQueue<(Guid SkuId, string SkuLevel)> Calls { get; } = new();

    public void Reset()
    {
        Answer = InventoryStockCheck.Zero();
        Delay = TimeSpan.Zero;
        Throw = false;
    }

    public async Task<InventoryStockCheck> CheckNonZeroStockAsync(Guid skuId, string skuLevel,
        CancellationToken cancellationToken = default)
    {
        Calls.Enqueue((skuId, skuLevel));
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay, cancellationToken);
        if (Throw) throw new HttpRequestException("inventory down (test)");
        return Answer;
    }
}
