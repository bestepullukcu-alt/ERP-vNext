namespace Diten.InventoryService.Application.Features.Recall.Drills;

/// <summary>A lot a drill can run on (id + number, the dr-v1 <c>lotId</c> / <c>lotNumber</c>).</summary>
public sealed record RecallDrillLot(string LotId, string LotNumber);

/// <summary>
/// WP-0177-R5 (pack §8.10) — the READ-ONLY candidate set of a drill plan, bound to the caller's Tenant + LegalEntity:
/// <list type="bullet">
/// <item><see cref="FinishedGoodLotsAsync"/> — finished-good lots (first inbound <c>GOODS_RECEIPT_PROD</c>) with an outbound movement
/// (<c>GOODS_ISSUE_SALES</c>) posted since <c>since</c> (the last 365 days);</item>
/// <item><see cref="RawMaterialLotsAsync"/> — raw-material lots (received <c>GOODS_RECEIPT_PO</c>) that have genealogy children;</item>
/// <item><see cref="LotAsync"/> — the lot a user picks (Manual), null = no such lot of the tenant.</item>
/// </list>
/// The lists come back in ONE order (lot id, ordinal) so a seeded random pick is reproducible.
/// </summary>
public interface IRecallDrillCandidates
{
    Task<IReadOnlyList<RecallDrillLot>> FinishedGoodLotsAsync(DateTimeOffset since, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecallDrillLot>> RawMaterialLotsAsync(CancellationToken cancellationToken = default);

    Task<RecallDrillLot?> LotAsync(Guid lotId, CancellationToken cancellationToken = default);
}

/// <summary>The randomness of the Auto pick (injectable: the tests run it with a fixed seed). The pick is audited (index + candidate count).</summary>
public interface IRecallDrillRandom
{
    /// <summary>0 ≤ result &lt; <paramref name="count"/>.</summary>
    int Next(int count);
}

/// <summary>Production randomness (<see cref="Random.Shared"/>).</summary>
public sealed class SharedRecallDrillRandom : IRecallDrillRandom
{
    public int Next(int count) => Random.Shared.Next(count);
}

/// <summary>A seeded randomness (tests: the same seed gives the same pick).</summary>
public sealed class SeededRecallDrillRandom(int seed) : IRecallDrillRandom
{
    private readonly Random _random = new(seed);
    private readonly object _gate = new();

    public int Next(int count)
    {
        lock (_gate) return _random.Next(count);
    }
}
