using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Rules;

namespace Diten.InventoryService.Domain.Traceability.Repositories;

/// <summary>
/// WP-0174-P4 — the typed-lot (W-UI-02) migration store (pack §4 / §8 "Migration re-key", CT-D6). The migration is the
/// TENANT's (one record per tenant, lots tenant-wide — CT-D3): groups and links span every company of the tenant, and the
/// commit re-keys every company's projections. Reads of links are the caller's company's (stock reads stay company-scoped).
/// </summary>
public interface IManualLotMigrationRepository
{
    /// <summary>The tenant's migration record (also the typed-lot gate document), or null.</summary>
    Task<ManualLotMigration?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The typed-lot groups of the tenant as the ledger holds them now: every distinct SKU + typed text over the ledger
    /// transactions that carry a lot text which is not a registered lot id and that no <see cref="MovementLotLink"/> covers,
    /// with their transaction count, first posting and on-hand (Σ balance OnHand, every company, base unit).
    /// </summary>
    Task<IReadOnlyList<TypedLotGroupStock>> ListTypedGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>The registered lots of these SKUs (tenant-wide) — the matcher's candidates.</summary>
    Task<IReadOnlyList<RegisteredLotRef>> LotsOfSkusAsync(IReadOnlyCollection<(Guid SkuId, SkuLevel SkuLevel)> skus, CancellationToken cancellationToken = default);

    /// <summary>
    /// The commit, ONE transaction: gate + "not completed" check, groups re-read and compared (stale), new Draft lots,
    /// history rows, one link per linked transaction, the ledger projection re-key, serial lots (CT K4-5) and the
    /// completed record with <c>ManualLotEntryClosed = true</c>. No ledger transaction document is written.
    /// </summary>
    /// <exception cref="MigrationAlreadyCompletedException">The tenant's typing is already closed.</exception>
    /// <exception cref="MigrationStaleException">The groups changed since they were listed.</exception>
    /// <exception cref="LotUniqueViolationException">A new lot collides with an existing one.</exception>
    Task<ManualLotMigration> CommitAsync(ManualLotMigrationCommit commit, CancellationToken cancellationToken = default);

    /// <summary>Links of the caller's company, by transaction and / or lot (at most <paramref name="limit"/>).</summary>
    Task<IReadOnlyList<MovementLotLink>> LinksAsync(string? transactionId, Guid? lotId, int limit, CancellationToken cancellationToken = default);
}

/// <summary>One typed group with its stock (base unit, Σ over the tenant's companies).</summary>
public sealed record TypedLotGroupStock(TypedLotGroup Group, decimal OnHand, string? BaseUomId);

/// <summary>Where a group goes: its target lot (existing or created by this commit) and whether that lot holds its stock (CT-D1).</summary>
public sealed record ManualLotMigrationLink(TypedLotGroup Group, Guid LotId);

/// <summary>
/// Everything the commit writes, decided before the transaction. <see cref="ExpectedGroupIds"/> is the group set the
/// decisions were made on; <see cref="ExcludedItemIds"/> the items whose typed texts are not migrated (not lot-controlled,
/// K4-4). A group of an item in neither <see cref="Links"/> nor <see cref="ExcludedItemIds"/> found at commit time = stale.
/// </summary>
public sealed record ManualLotMigrationCommit(
    string IdempotencyKey,
    string Fingerprint,
    string Reason,
    Guid UserId,
    DateTimeOffset Now,
    IReadOnlySet<string> ExpectedGroupIds,
    IReadOnlySet<Guid> ExcludedItemIds,
    IReadOnlyList<Lot> NewLots,
    IReadOnlyList<ManualLotMigrationLink> Links,
    IReadOnlyList<LotChangeEntry> History);

public sealed class MigrationAlreadyCompletedException() : Exception("The typed-lot migration of this tenant is already completed.");

public sealed class MigrationStaleException(string detail) : Exception(detail);
