using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Rules;
using Diten.InventoryService.Persistence.Traceability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Quarantine;

/// <summary>
/// MOD-0175 Q4 (KQ-H, CT-Q16) — the retest hold. The ONLY writer of <see cref="Lot.RetestHeldAtUtc"/>, the one stored fact every
/// "does this lot hold its stock" question reads (<see cref="LotPostingRules.HoldsStock(Lot)"/>: ledger, rebuilder, availability,
/// re-key). The date is compared here and in the posting rule only:
/// <list type="bullet">
/// <item><b>Set</b> — a lot whose retest date is before the UTC day of <c>now</c> and carries no hold: in ONE transaction the lot is
/// re-read, the hold is set (version filter), and <see cref="LotHeldStock.ApplyRetestAsync"/> takes the lot's AVAILABLE stock out of
/// the SKU positions (new reservations are refused by the unchanged S2 guard; existing ones stay, CT-Q6).</item>
/// <item><b>Clear</b> — a held lot whose retest date was extended (or removed): in ONE transaction the hold is cleared and the stock
/// comes back.</item>
/// </list>
/// A posting of the same lot writes the lot document in its own transaction (<c>InventoryLotDirectory.ResolveLotAsync</c> →
/// <c>StockTouchSeq</c>), so a posting and a set / clear conflict and one side re-runs on the committed state — never a double
/// subtraction. The lot <c>Version</c> (ETag) is not bumped: the hold is not a lot attribute anybody edits. Idempotent: a second run
/// finds nothing to change.
/// </summary>
public sealed class RetestHoldSweeper : ILotRetestHold
{
    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<Lot> _lots;
    private readonly ILogger<RetestHoldSweeper> _logger;

    public RetestHoldSweeper(IMongoClient client, IMongoDatabase database, ILogger<RetestHoldSweeper> logger)
    {
        _client = client;
        _database = database;
        _lots = database.GetCollection<Lot>(TraceCollections.Lots);
        _logger = logger;
    }

    public sealed record SweepResult(int Held, int Cleared);

    /// <summary>One sweep over every tenant (or one, for tests): sets the due holds, clears the extended ones.</summary>
    public async Task<SweepResult> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken = default, Guid? onlyTenant = null)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var startOfToday = new DateTimeOffset(today.Year, today.Month, today.Day, 0, 0, 0, TimeSpan.Zero);
        var f = Builders<Lot>.Filter;
        var scope = f.Eq(x => x.IsDeleted, false) & (onlyTenant is { } tenant ? f.Eq(x => x.TenantId, tenant) : f.Empty);
        var due = await _lots.Find(scope & f.Eq(x => x.RetestHeldAtUtc, null) & f.Lt(x => x.RetestDate, startOfToday))
            .Project(x => x.Id).ToListAsync(cancellationToken);
        var extended = await _lots.Find(scope & f.Ne(x => x.RetestHeldAtUtc, null) & (f.Eq(x => x.RetestDate, null) | f.Gte(x => x.RetestDate, startOfToday)))
            .Project(x => x.Id).ToListAsync(cancellationToken);

        var held = 0;
        foreach (var lotId in due)
        {
            if (await RefreshAsync(lotId, now, cancellationToken)) held++;
        }

        var cleared = 0;
        foreach (var lotId in extended)
        {
            if (await RefreshAsync(lotId, now, cancellationToken)) cleared++;
        }

        return new SweepResult(held, cleared);
    }

    /// <summary>Sets or clears the hold of one lot as its retest date says on the UTC day of <paramref name="now"/>; true when it changed.</summary>
    public async Task<bool> RefreshAsync(Guid lotId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(async (s, ct) =>
        {
            var lot = await _lots.Find(s, Builders<Lot>.Filter.Eq(x => x.Id, lotId)).FirstOrDefaultAsync(ct);
            if (lot is null || lot.IsDeleted)
            {
                return false;
            }

            var heldBefore = lot.RetestHeldAtUtc;
            var heldAfter = LotPostingRules.IsRetestDueOn(lot, now) ? heldBefore ?? now : (DateTimeOffset?)null;
            if (heldBefore == heldAfter)
            {
                return false;
            }

            var result = await _lots.UpdateOneAsync(s,
                Builders<Lot>.Filter.Eq(x => x.Id, lot.Id) & Builders<Lot>.Filter.Eq(x => x.Version, lot.Version),
                Builders<Lot>.Update.Set(x => x.RetestHeldAtUtc, heldAfter), cancellationToken: ct);
            if (result.MatchedCount != 1)
            {
                return false;
            }

            await LotHeldStock.ApplyRetestAsync(_database, s, lot, heldBefore, heldAfter, now, ct);
            _logger.LogInformation("MOD-0175 retest hold {Change} on lot {LotId} (retest date {RetestDate}).",
                heldAfter is null ? "cleared" : "set", lot.Id, lot.RetestDate);
            return true;
        }, new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority), cancellationToken);
    }
}

/// <summary>
/// Runs <see cref="RetestHoldSweeper"/> once at start-up and then every <c>Inventory:Quarantine:RetestSweepIntervalSeconds</c> (the day
/// rollover is caught within one interval; postings refuse a due lot on the date itself meanwhile). The clock is the injected
/// <see cref="TimeProvider"/>. <c>RetestSweeperEnabled = false</c> turns it off (tests drive the sweeper directly).
/// </summary>
public sealed class RetestHoldSweeperHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly QuarantineSweeperOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<RetestHoldSweeperHostedService> _logger;

    public RetestHoldSweeperHostedService(
        IServiceScopeFactory scopeFactory, IOptions<QuarantineSweeperOptions> options, TimeProvider clock, ILogger<RetestHoldSweeperHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.RetestSweeperEnabled)
        {
            _logger.LogWarning("MOD-0175 retest hold sweeper is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.RetestSweepIntervalSeconds)), _clock);
        try
        {
            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var result = await scope.ServiceProvider.GetRequiredService<RetestHoldSweeper>().SweepAsync(_clock.GetUtcNow(), stoppingToken);
                    if (result.Held + result.Cleared > 0)
                    {
                        _logger.LogInformation("MOD-0175 retest sweep: {Held} hold(s) set, {Cleared} cleared.", result.Held, result.Cleared);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MOD-0175 retest sweep failed; retrying on the next tick.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
