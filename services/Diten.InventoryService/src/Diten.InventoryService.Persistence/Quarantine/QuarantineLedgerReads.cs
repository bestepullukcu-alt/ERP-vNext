using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Quarantine;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Persistence.Traceability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Quarantine;

/// <summary>
/// READ-ONLY ledger facts for MOD-0175 (pack ASSUMPTION-0175-05; CT-Q6 refusal details): the transactions of a 0175 document,
/// the SKU position's unreserved quantity and the SKU's active reservations — Tenant + Legal Entity scoped. This class only
/// calls <c>Find</c> on the ledger collections (architecture-tested); 0175 posts through the ledger's internal posting
/// service and never writes a ledger collection.
/// </summary>
public sealed class QuarantineLedgerReader : IQuarantineLedgerReader
{
    private readonly IMongoCollection<InventoryTransaction> _transactions;
    private readonly IMongoCollection<InventorySkuPosition> _positions;
    private readonly IMongoCollection<InventoryReservation> _reservations;
    private readonly IMongoCollection<InventoryBalance> _balances;
    private readonly IMongoCollection<Lot> _lots;
    private readonly IMongoCollection<SerialNumber> _serials;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public QuarantineLedgerReader(IMongoDatabase database, ITenantContext tenant)
    {
        _transactions = database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions);
        _positions = database.GetCollection<InventorySkuPosition>(InventoryCollections.SkuPositions);
        _reservations = database.GetCollection<InventoryReservation>(InventoryCollections.Reservations);
        _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        _lots = database.GetCollection<Lot>(TraceCollections.Lots);
        _serials = database.GetCollection<SerialNumber>(TraceCollections.Serials);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    private FilterDefinition<T> Scope<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<T>.Filter.Eq(x => x.LegalEntityId, _legalEntityId)
           & Builders<T>.Filter.Eq(x => x.IsDeleted, false);

    public async Task<IReadOnlyList<InventoryTransaction>> TransactionsAsync(
        IReadOnlyCollection<string> sourceDocumentIds, IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default)
    {
        if (sourceDocumentIds.Count == 0 && transactionIds.Count == 0)
        {
            return [];
        }

        var f = Builders<InventoryTransaction>.Filter;
        var match = f.Or(
            f.Eq(x => x.SourceModule, QuarantineContract.ModuleId) & f.In(x => x.SourceDocumentId, sourceDocumentIds),
            f.In(x => x.Id, transactionIds));
        return await _transactions.Find(Scope<InventoryTransaction>() & match).Limit(500).ToListAsync(cancellationToken);
    }

    public async Task<decimal?> SkuUnreservedAsync(Guid itemId, Guid skuId, SkuLevel skuLevel, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventorySkuPosition>.Filter;
        var position = await _positions.Find(Scope<InventorySkuPosition>() & f.Eq(x => x.ItemId, itemId) & f.Eq(x => x.SkuId, skuId)
                                             & f.Eq(x => x.SkuLevel, skuLevel)).FirstOrDefaultAsync(cancellationToken);
        return position is null ? null : position.AvailableOnHand - position.Reserved;
    }

    public async Task<IReadOnlyList<ReservationLine>> ActiveReservationsAsync(Guid itemId, Guid skuId, SkuLevel skuLevel, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryReservation>.Filter;
        var now = DateTimeOffset.UtcNow;
        var rows = await _reservations.Find(Scope<InventoryReservation>() & f.Eq(x => x.ItemId, itemId) & f.Eq(x => x.SkuId, skuId)
                                            & f.Eq(x => x.SkuLevel, skuLevel) & f.Eq(x => x.Status, ReservationStatus.Active)
                                            & (f.Eq(x => x.ExpiresAt, null) | f.Gt(x => x.ExpiresAt, (DateTimeOffset?)now)))
            .Limit(100).ToListAsync(cancellationToken);
        return rows.Where(r => r.RemainingQuantity > 0m)
            .Select(r => new ReservationLine(r.Id, r.SourceModule, r.SourceDocumentId, r.RemainingQuantity)).ToList();
    }

    public async Task<IReadOnlyList<InventoryBalance>> LotBalancesAsync(IReadOnlyCollection<string> lotIds, StockStatus status, CancellationToken cancellationToken = default)
    {
        if (lotIds.Count == 0)
        {
            return [];
        }

        var f = Builders<InventoryBalance>.Filter;
        return await _balances.Find(Scope<InventoryBalance>() & f.In(x => x.LotId, lotIds) & f.Eq(x => x.StockStatus, status) & f.Gt(x => x.OnHand, 0m))
            .Sort(Builders<InventoryBalance>.Sort.Ascending(x => x.LotId).Ascending(x => x.WarehouseId).Ascending(x => x.LocationId))
            .Limit(2000).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Lot>> RetestLotsAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
    {
        var f = Builders<Lot>.Filter;
        return await _lots.Find(f.Eq(x => x.TenantId, _tenantId) & f.Eq(x => x.IsDeleted, false) & f.Lt(x => x.RetestDate, before)
                                & f.Ne(x => x.Status, LotStatus.Expired))
            .Sort(Builders<Lot>.Sort.Ascending(x => x.RetestDate).Ascending(x => x.LotNumber))
            .Limit(1000).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SerialsAtAsync(
        Guid itemId, Guid skuId, SkuLevel skuLevel, string? lotId, string warehouseId, string locationId, CancellationToken cancellationToken = default)
    {
        var f = Builders<SerialNumber>.Filter;
        Guid? lot = Guid.TryParse(lotId, out var parsed) ? parsed : null;
        var rows = await _serials.Find(f.Eq(x => x.TenantId, _tenantId) & f.Eq(x => x.IsDeleted, false) & f.Eq(x => x.ItemId, itemId)
                                       & f.Eq(x => x.SkuId, skuId) & f.Eq(x => x.SkuLevel, skuLevel) & f.Eq(x => x.LotId, lot)
                                       & f.Eq(x => x.Status, SerialStatus.Active) & f.Eq(x => x.WarehouseId, warehouseId) & f.Eq(x => x.LocationId, locationId))
            .Sort(Builders<SerialNumber>.Sort.Ascending(x => x.CreatedAt).Ascending(x => x.Number))
            .Limit(5000).ToListAsync(cancellationToken);
        return rows.Select(r => r.Number).ToList();
    }
}

/// <summary>
/// Cross-tenant READ-ONLY feed for the intake sweep: recent inbound transactions into QUARANTINE / QUALITY_INSPECTION /
/// BLOCKED that MOD-0175 did not post itself. Only <c>Find</c> on <c>inventory_transactions</c>.
/// </summary>
public sealed class QuarantineIntakeFeed : IQuarantineIntakeFeed
{
    private static readonly MovementType[] Inbound =
        [MovementType.OpeningBalance, MovementType.GoodsReceiptPo, MovementType.GoodsReceiptProd, MovementType.CustomerReturn, MovementType.CountGain];

    private static readonly StockStatus[] Targets = [StockStatus.Quarantine, StockStatus.QualityInspection, StockStatus.Blocked];

    private readonly IMongoCollection<InventoryTransaction> _transactions;

    public QuarantineIntakeFeed(IMongoDatabase database)
        => _transactions = database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions);

    public async Task<IReadOnlyList<IntakeCandidate>> RecentIntakesAsync(DateTimeOffset since, DateTimeOffset until, int limit, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryTransaction>.Filter;
        var filter = f.Eq(x => x.IsDeleted, false) & f.In(x => x.MovementType, Inbound) & f.In(x => x.ToStockStatus, Targets.Select(t => (StockStatus?)t))
                     & f.Ne(x => x.SourceModule, QuarantineContract.ModuleId) & f.Gte(x => x.CreatedAt, since) & f.Lt(x => x.CreatedAt, until)
                     & f.Ne(x => x.TenantId, null) & f.Ne(x => x.LegalEntityId, null);
        var rows = await _transactions.Find(filter).Sort(Builders<InventoryTransaction>.Sort.Ascending(x => x.CreatedAt)).Limit(limit).ToListAsync(cancellationToken);
        return rows.Select(t => new IntakeCandidate(t, t.TenantId!.Value, t.LegalEntityId!.Value)).ToList();
    }
}

public sealed class QuarantineSweeperOptions
{
    public const string SectionName = "Inventory:Quarantine";

    public bool IntakeSweeperEnabled { get; set; } = true;

    public int IntakeSweepIntervalSeconds { get; set; } = 300;

    /// <summary>How far back the sweep looks for an intake the observer missed.</summary>
    public int IntakeLookbackHours { get; set; } = 72;

    /// <summary>Transactions younger than this are left to the post-commit observer.</summary>
    public int IntakeSettleSeconds { get; set; } = 60;

    /// <summary>Q4 (KQ-H): the retest hold sweeper (<see cref="RetestHoldSweeperHostedService"/>).</summary>
    public bool RetestSweeperEnabled { get; set; } = true;

    public int RetestSweepIntervalSeconds { get; set; } = 900;
}

/// <summary>
/// The intake sweep (pack §8): every candidate of <see cref="IQuarantineIntakeFeed"/> is handed to the SAME
/// <see cref="QuarantineIntakeService"/> the observer uses, inside a scope resolved to the transaction's tenant and company
/// (actor = the poster). The unique source-movement index makes it idempotent: a record the observer already wrote is found
/// and nothing is created.
/// </summary>
public sealed class QuarantineIntakeSweeper
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly QuarantineSweeperOptions _options;

    public QuarantineIntakeSweeper(IServiceScopeFactory scopeFactory, IOptions<QuarantineSweeperOptions> options)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    /// <summary>Returns how many records this sweep created.</summary>
    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct, Guid? onlyTenant = null)
    {
        IReadOnlyList<IntakeCandidate> candidates;
        using (var feedScope = _scopeFactory.CreateScope())
        {
            candidates = await feedScope.ServiceProvider.GetRequiredService<IQuarantineIntakeFeed>()
                .RecentIntakesAsync(now.AddHours(-_options.IntakeLookbackHours), now.AddSeconds(-_options.IntakeSettleSeconds), 500, ct);
        }

        var created = 0;
        foreach (var candidate in candidates.Where(c => onlyTenant is null || c.TenantId == onlyTenant))
        {
            using var scope = _scopeFactory.CreateScope();
            var actor = candidate.Transaction.CreatedBy != Guid.Empty ? candidate.Transaction.CreatedBy : QuarantineSweeperActor;
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(candidate.TenantId, candidate.LegalEntityId, actor);
            // one correlation id per swept transaction: the posting's own, so the history row joins the ledger audit row
            scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(
                candidate.Transaction.CorrelationId != Guid.Empty ? candidate.Transaction.CorrelationId : Guid.NewGuid());
            if (await scope.ServiceProvider.GetRequiredService<QuarantineIntakeService>().TakeInAsync(candidate.Transaction, ct))
            {
                created++;
            }
        }

        return created;
    }

    /// <summary>Actor of a sweep-created record when the transaction names none.</summary>
    public static readonly Guid QuarantineSweeperActor = new("00000000-0000-0000-0000-000000000175");
}

public sealed class QuarantineIntakeSweeperHostedService : BackgroundService
{
    private readonly QuarantineIntakeSweeper _sweeper;
    private readonly QuarantineSweeperOptions _options;
    private readonly ILogger<QuarantineIntakeSweeperHostedService> _logger;

    public QuarantineIntakeSweeperHostedService(
        QuarantineIntakeSweeper sweeper, IOptions<QuarantineSweeperOptions> options, ILogger<QuarantineIntakeSweeperHostedService> logger)
    {
        _sweeper = sweeper;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IntakeSweeperEnabled)
        {
            _logger.LogWarning("MOD-0175 quarantine intake sweeper is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.IntakeSweepIntervalSeconds)));
        try
        {
            do
            {
                try
                {
                    var created = await _sweeper.SweepAsync(DateTimeOffset.UtcNow, stoppingToken);
                    if (created > 0)
                    {
                        _logger.LogWarning("MOD-0175 intake sweep created {Count} record(s) the post-commit observer had missed.", created);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MOD-0175 intake sweep failed; retrying on the next tick.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
