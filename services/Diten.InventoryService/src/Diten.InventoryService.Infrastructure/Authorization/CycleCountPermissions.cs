namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// MOD-0181 permission keys — namespace <c>cycle-count.*</c>, owned by MOD-0181 (pack §14, Q-16 = A; PKS-001 §4 one owner per namespace). All
/// Tenant scope. <see cref="Implemented"/> are the keys the controllers enforce so far — exactly what the manifest declares; the line, variance,
/// settings and evidence keys come with their slices (DL-2 / DL-3 / DL-5). There is no <c>delete</c> key: a single Draft delete is
/// <see cref="CountsCancel"/>, a bulk one <see cref="CountsBulkDelete"/> (pack §10 G-0181-01, r4 02/F-6).
/// </summary>
public static class CycleCountPermissions
{
    /// <summary>Dashboard, list, detail, timeline, lines (blind: no expected quantity).</summary>
    public const string CountsRead = "cycle-count.counts.read";

    /// <summary>Open a count.</summary>
    public const string CountsCreate = "cycle-count.counts.create";

    /// <summary>Edit a Draft, assign, release (the snapshot).</summary>
    public const string CountsUpdate = "cycle-count.counts.update";

    /// <summary>Soft delete Draft counts in bulk (Tier 2).</summary>
    public const string CountsBulkDelete = "cycle-count.counts.bulk-delete";

    /// <summary><c>POST …/cancel</c> — Draft: soft delete; later: cancel state (Tier 3 <c>cancel</c>).</summary>
    public const string CountsCancel = "cycle-count.counts.cancel";

    /// <summary>DL-2: quantity entry, found line, submit.</summary>
    public const string CountsRecord = "cycle-count.counts.record";

    /// <summary>DL-3: Variance Trace, the expected / variance view.</summary>
    public const string VariancesRead = "cycle-count.variances.read";

    /// <summary>The keys DL-1's controller enforces (pack §14 "Dilim" column) — exactly the manifest's.</summary>
    public static readonly IReadOnlyList<string> Dl1 = [CountsRead, CountsCreate, CountsUpdate, CountsBulkDelete, CountsCancel];

    /// <summary>Every key the controllers enforce so far.</summary>
    public static IReadOnlyList<string> Implemented => Dl1;
}
