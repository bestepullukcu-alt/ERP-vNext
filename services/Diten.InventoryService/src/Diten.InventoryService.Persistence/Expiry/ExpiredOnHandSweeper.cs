using System.Globalization;
using Diten.InventoryService.Application.Features.Expiry;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Persistence.Traceability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Expiry;

/// <summary>Configuration of <see cref="ExpiredOnHandSweeperHostedService"/> (section <c>Inventory:Expiry</c>, pack §8.5).</summary>
public sealed class ExpirySweeperOptions
{
    public const string SectionName = "Inventory:Expiry";

    public bool SweeperEnabled { get; set; } = true;

    public int SweepIntervalSeconds { get; set; } = 900;
}

/// <summary>
/// Pack §8.5 — the expired-on-hand watch and the SLA clock. TENANT + COMPANY SCOPED by construction (unlike MOD-0174's global
/// <c>LotExpirySweeper</c>, which is not touched): every case it opens carries the Tenant + LegalEntity of the stock it is about,
/// and the threshold is that company's policy.
/// <list type="bullet">
/// <item><b>EXPIRED_ON_HAND</b> — per (company, warehouse, lot) with on-hand &gt; 0 in any status and a lot expiry before
/// <c>today − ExpiredOnHandThresholdDays</c>: one case, unless an ACTIVE one with the key <c>EXPIRED_ON_HAND:{lot}:{warehouse}</c>
/// exists (the unique partial index makes a concurrent second run fail, not duplicate). System case: no origin user (CT-F10).</item>
/// <item><b>SlaBreached</b> — one event per case awaiting a decision past its due time (once-only key).</item>
/// </list>
/// Each case is written with its Opened event and audit row in ONE transaction. Idempotent: a second run finds nothing new.
/// </summary>
public sealed class ExpiredOnHandSweeper
{
    private const string SlaBreachedOnce = nameof(ExpiryExceptionEventAction.SlaBreached);

    private readonly IMongoClient _client;
    private readonly IMongoCollection<InventoryBalance> _balances;
    private readonly IMongoCollection<Lot> _lots;
    private readonly IMongoCollection<ExpiryPolicy> _policies;
    private readonly IMongoCollection<FefoExceptionCase> _cases;
    private readonly IMongoCollection<FefoExceptionEvent> _events;
    private readonly IMongoCollection<BsonDocument> _counters;
    private readonly IMongoCollection<ExpiryAuditRecord> _audit;
    private readonly ILogger<ExpiredOnHandSweeper> _logger;

    public ExpiredOnHandSweeper(IMongoClient client, IMongoDatabase database, ILogger<ExpiredOnHandSweeper> logger)
    {
        _client = client;
        _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        _lots = database.GetCollection<Lot>(TraceCollections.Lots);
        _policies = database.GetCollection<ExpiryPolicy>(ExpiryCollections.Policies);
        _cases = database.GetCollection<FefoExceptionCase>(ExpiryCollections.Exceptions);
        _events = database.GetCollection<FefoExceptionEvent>(ExpiryCollections.ExceptionEvents);
        _counters = database.GetCollection<BsonDocument>(ExpiryCollections.Counters);
        _audit = database.GetCollection<ExpiryAuditRecord>(InventoryCollections.Audit);
        _logger = logger;
    }

    public sealed record SweepResult(int Opened, int SlaBreached);

    private sealed record OnHand(Guid TenantId, Guid LegalEntityId, string WarehouseId, string LotId, decimal Quantity, string BaseUomId);

    /// <summary>One sweep over every tenant (or one, for tests).</summary>
    public async Task<SweepResult> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken = default, Guid? onlyTenant = null)
    {
        var opened = await OpenExpiredOnHandAsync(now, onlyTenant, cancellationToken);
        var breached = await RecordSlaBreachesAsync(now, onlyTenant, cancellationToken);
        return new SweepResult(opened, breached);
    }

    private async Task<int> OpenExpiredOnHandAsync(DateTimeOffset now, Guid? onlyTenant, CancellationToken ct)
    {
        var b = Builders<InventoryBalance>.Filter;
        var filter = b.Eq(x => x.IsDeleted, false) & b.Gt(x => x.OnHand, 0m) & b.Ne(x => x.LotId, null) & b.Ne(x => x.WarehouseId, null)
                     & b.Ne(x => x.TenantId, null) & b.Ne(x => x.LegalEntityId, null);
        if (onlyTenant is { } tenant) filter &= b.Eq(x => x.TenantId, tenant);
        var rows = await _balances.Find(filter).ToListAsync(ct);
        var onHand = rows
            .GroupBy(r => (Tenant: r.TenantId!.Value, Company: r.LegalEntityId!.Value, Warehouse: r.WarehouseId!, Lot: r.LotId!))
            .Select(g => new OnHand(g.Key.Tenant, g.Key.Company, g.Key.Warehouse, g.Key.Lot, g.Sum(r => r.OnHand), g.First().BaseUomId))
            .ToList();
        if (onHand.Count == 0) return 0;

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var opened = 0;
        foreach (var tenantGroup in onHand.GroupBy(o => o.TenantId))
        {
            var lotIds = tenantGroup.Select(o => Guid.TryParse(o.LotId, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
            var lots = (await _lots.Find(Builders<Lot>.Filter.Eq(x => x.TenantId, tenantGroup.Key) & Builders<Lot>.Filter.In(x => x.Id, lotIds)
                                         & Builders<Lot>.Filter.Ne(x => x.ExpiryDate, null)).ToListAsync(ct))
                .ToDictionary(l => l.Id.ToString("D"), StringComparer.Ordinal);

            foreach (var companyGroup in tenantGroup.GroupBy(o => o.LegalEntityId))
            {
                var policy = await _policies.Find(Builders<ExpiryPolicy>.Filter.Eq(x => x.TenantId, tenantGroup.Key)
                                                  & Builders<ExpiryPolicy>.Filter.Eq(x => x.LegalEntityId, companyGroup.Key)
                                                  & Builders<ExpiryPolicy>.Filter.Eq(x => x.IsDeleted, false)).FirstOrDefaultAsync(ct)
                             ?? ExpiryPolicyRules.Defaults();
                var cutoff = today.AddDays(-policy.ExpiredOnHandThresholdDays);
                foreach (var stock in companyGroup)
                {
                    if (!lots.TryGetValue(stock.LotId, out var lot) || DateOnly.FromDateTime(lot.ExpiryDate!.Value.UtcDateTime) >= cutoff)
                    {
                        continue;
                    }

                    if (await OpenAsync(stock, lot, policy, today, now, ct)) opened++;
                }
            }
        }

        return opened;
    }

    private async Task<bool> OpenAsync(OnHand stock, Lot lot, ExpiryPolicy policy, DateOnly today, DateTimeOffset now, CancellationToken ct)
    {
        var dedupKey = ExpiryExceptionRules.DedupKey(ExpiryExceptionType.ExpiredOnHand, stock.LotId, stock.WarehouseId);
        var c = Builders<FefoExceptionCase>.Filter;
        if (await _cases.Find(c.Eq(x => x.TenantId, stock.TenantId) & c.Eq(x => x.LegalEntityId, stock.LegalEntityId) & c.Eq(x => x.DedupKey, dedupKey)).AnyAsync(ct))
        {
            return false;
        }

        var daysSinceExpiry = today.DayNumber - DateOnly.FromDateTime(lot.ExpiryDate!.Value.UtcDateTime).DayNumber;
        var exceptionCase = new FefoExceptionCase
        {
            Id = Guid.NewGuid(),
            TenantId = stock.TenantId,
            LegalEntityId = stock.LegalEntityId,
            CreatedAt = now,
            Type = ExpiryExceptionType.ExpiredOnHand,
            Origin = ExpiryExceptionOrigin.System,
            Status = ExpiryExceptionStatus.Open,
            ItemId = lot.ItemId,
            SkuId = lot.SkuId,
            SkuLevel = lot.SkuLevel,
            LotId = stock.LotId,
            LotNumber = lot.LotNumber,
            ExpiryDate = lot.ExpiryDate,
            RetestDate = lot.RetestDate,
            WarehouseId = stock.WarehouseId,
            Quantity = stock.Quantity,
            BaseUomId = stock.BaseUomId,
            Facts = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["daysSinceExpiry"] = daysSinceExpiry.ToString(CultureInfo.InvariantCulture),
                ["thresholdDays"] = policy.ExpiredOnHandThresholdDays.ToString(CultureInfo.InvariantCulture)
            },
            OpenedAtUtc = now,
            OpenedBy = null,
            OriginUserId = null,
            DueAtUtc = ExpiryExceptionRules.DueAt(now, policy, ExpiryExceptionType.ExpiredOnHand),
            DedupKey = dedupKey
        };

        try
        {
            using var session = await _client.StartSessionAsync(cancellationToken: ct);
            await session.WithTransactionAsync<bool>(async (s, tx) =>
            {
                exceptionCase.Number = await ExpiryNumbers.NextAsync(_counters, s, stock.TenantId, now.UtcDateTime.Year, tx);
                await _cases.InsertOneAsync(s, exceptionCase, cancellationToken: tx);
                await _events.InsertOneAsync(s, SystemEvent(exceptionCase, ExpiryExceptionEventAction.Opened, now, null), cancellationToken: tx);
                await _audit.InsertOneAsync(s, SystemAudit(exceptionCase, ExpiryContract.AuditExceptionOpened, now, new Dictionary<string, string?>
                {
                    ["type"] = ExpiryWire.Code(exceptionCase.Type),
                    ["origin"] = exceptionCase.Origin.ToString(),
                    ["lotId"] = exceptionCase.LotId,
                    ["warehouseId"] = exceptionCase.WarehouseId,
                    ["daysSinceExpiry"] = exceptionCase.Facts["daysSinceExpiry"]
                }), cancellationToken: tx);
                return true;
            }, new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority), ct);
        }
        catch (MongoWriteException ex) when (ExpiryRepository.UniqueIndexOf(ex.WriteError?.Category, ex.WriteError?.Message) is not null)
        {
            return false; // a concurrent run opened it
        }

        _logger.LogInformation("MOD-0176 expired-on-hand exception {Number} opened (lot {LotId}, warehouse {WarehouseId}, {Days} days after expiry).",
            exceptionCase.Number, exceptionCase.LotId, exceptionCase.WarehouseId, daysSinceExpiry);
        return true;
    }

    private async Task<int> RecordSlaBreachesAsync(DateTimeOffset now, Guid? onlyTenant, CancellationToken ct)
    {
        var c = Builders<FefoExceptionCase>.Filter;
        var filter = c.Eq(x => x.IsDeleted, false) & c.In(x => x.Status, [ExpiryExceptionStatus.Open, ExpiryExceptionStatus.InReview]) & c.Lt(x => x.DueAtUtc, now);
        if (onlyTenant is { } tenant) filter &= c.Eq(x => x.TenantId, tenant);
        var overdue = await _cases.Find(filter).ToListAsync(ct);
        if (overdue.Count == 0) return 0;

        var ids = overdue.Select(x => x.Id).ToList();
        var already = (await _events.Find(Builders<FefoExceptionEvent>.Filter.In(x => x.CaseId, ids) & Builders<FefoExceptionEvent>.Filter.Eq(x => x.OnceKey, SlaBreachedOnce))
            .Project(x => x.CaseId).ToListAsync(ct)).ToHashSet();
        var recorded = 0;
        foreach (var exceptionCase in overdue.Where(x => !already.Contains(x.Id)))
        {
            try
            {
                using var session = await _client.StartSessionAsync(cancellationToken: ct);
                await session.WithTransactionAsync<bool>(async (s, tx) =>
                {
                    var breach = SystemEvent(exceptionCase, ExpiryExceptionEventAction.SlaBreached, now, $"due {exceptionCase.DueAtUtc.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'}");
                    breach.OnceKey = SlaBreachedOnce;
                    await _events.InsertOneAsync(s, breach, cancellationToken: tx);
                    await _audit.InsertOneAsync(s, SystemAudit(exceptionCase, ExpiryContract.AuditSlaBreached, now, new Dictionary<string, string?>
                    {
                        ["type"] = ExpiryWire.Code(exceptionCase.Type),
                        ["dueAtUtc"] = exceptionCase.DueAtUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                    }), cancellationToken: tx);
                    return true;
                }, new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority), ct);
                recorded++;
            }
            catch (MongoWriteException ex) when (ExpiryRepository.UniqueIndexOf(ex.WriteError?.Category, ex.WriteError?.Message) is not null)
            {
                // recorded by a concurrent run
            }
        }

        return recorded;
    }

    private static FefoExceptionEvent SystemEvent(FefoExceptionCase c, ExpiryExceptionEventAction action, DateTimeOffset at, string? text) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = c.TenantId,
        LegalEntityId = c.LegalEntityId,
        CreatedAt = at,
        CaseId = c.Id,
        Sequence = Application.Features.Expiry.Services.ExpirySupport.NextSequence(),
        Action = action,
        ActorUserId = null,
        AtUtc = at,
        Text = text
    };

    private static ExpiryAuditRecord SystemAudit(FefoExceptionCase c, string action, DateTimeOffset at, Dictionary<string, string?> detail) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = c.TenantId,
        LegalEntityId = c.LegalEntityId,
        CreatedAt = at,
        Action = action,
        SubjectType = ExpiryContract.SubjectException,
        SubjectId = c.Id,
        SubjectNumber = c.Number,
        UserId = null,
        Detail = detail,
        LeSource = "system",
        OccurredAt = at
    };
}

/// <summary>
/// Runs <see cref="ExpiredOnHandSweeper"/> once at start-up and then every <c>Inventory:Expiry:SweepIntervalSeconds</c> (default 900,
/// minimum 5; the <c>LotExpirySweeperHostedService</c> pattern, with the injected <see cref="TimeProvider"/>). A failed sweep is logged
/// and retried on the next tick; it never stops the host. <c>SweeperEnabled = false</c> turns it off (tests drive the sweeper).
/// </summary>
public sealed class ExpiredOnHandSweeperHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ExpirySweeperOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ExpiredOnHandSweeperHostedService> _logger;

    public ExpiredOnHandSweeperHostedService(
        IServiceScopeFactory scopeFactory, IOptions<ExpirySweeperOptions> options, TimeProvider clock, ILogger<ExpiredOnHandSweeperHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.SweeperEnabled)
        {
            _logger.LogWarning("MOD-0176 expired-on-hand sweeper is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.SweepIntervalSeconds)), _clock);
        try
        {
            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var result = await scope.ServiceProvider.GetRequiredService<ExpiredOnHandSweeper>().SweepAsync(_clock.GetUtcNow(), stoppingToken);
                    if (result.Opened + result.SlaBreached > 0)
                    {
                        _logger.LogInformation("MOD-0176 sweep: {Opened} expired-on-hand exception(s) opened, {Breached} SLA breach(es) recorded.",
                            result.Opened, result.SlaBreached);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MOD-0176 expired-on-hand sweep failed; retrying on the next tick.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
