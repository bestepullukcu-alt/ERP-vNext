using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.CountVariances.Commands;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.CycleCounting;

/// <summary>The MOD-0181 sweeper's settings (<c>CycleCount:Reconciler</c>): on / off, tick, the age of a Pending correction, the batch.</summary>
public sealed class CycleCountReconcilerOptions
{
    public const string Section = "CycleCount:Reconciler";

    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 60;

    /// <summary>A Pending correction is picked up once its last attempt is this old (the approving request has long answered).</summary>
    public int AgeSeconds { get; set; } = 120;

    public int BatchSize { get; set; } = 200;
}

/// <summary>
/// The sweeper's cross-tenant scan (pack §8.6): approved lines whose correction is Pending and whose last attempt is older than the age. Read only;
/// the actor of a row is the approver, or — for a tolerance approval, which has none — the count's submitter.
/// </summary>
public sealed class CycleCountPendingScan : ICycleCountPendingScan
{
    private readonly IMongoCollection<CycleCountLine> _lines;
    private readonly IMongoCollection<CycleCount> _counts;

    public CycleCountPendingScan(IMongoDatabase database)
    {
        _lines = database.GetCollection<CycleCountLine>(CycleCountCollections.Lines);
        _counts = database.GetCollection<CycleCount>(CycleCountCollections.Counts);
    }

    public async Task<IReadOnlyList<CycleCountPendingRow>> PendingAsync(DateTimeOffset lastAttemptBefore, int limit, IReadOnlyCollection<Guid>? onlyTenants,
        CancellationToken cancellationToken = default)
    {
        var lf = Builders<CycleCountLine>.Filter;
        var filter = lf.Eq(x => x.Status, CycleCountLineStatus.Approved) & lf.Eq(x => x.Adjustment!.Status, CycleCountAdjustmentStatus.Pending)
                     & lf.Lt(x => x.Adjustment!.LastAttemptAt, lastAttemptBefore);
        if (onlyTenants is not null) filter &= lf.In(x => x.TenantId, onlyTenants.Select(t => (Guid?)t));
        var lines = await _lines.Find(filter).SortBy(x => x.Adjustment!.LastAttemptAt).Limit(limit).ToListAsync(cancellationToken);
        if (lines.Count == 0) return [];

        var countIds = lines.Select(l => l.CountId).Distinct().ToList();
        var submitters = (await _counts.Find(Builders<CycleCount>.Filter.In(x => x.Id, countIds)).ToListAsync(cancellationToken))
            .ToDictionary(c => (c.TenantId, c.Id), c => c.SubmittedBy);
        return lines
            .Select(l => (Line: l, Actor: l.Decision?.By ?? submitters.GetValueOrDefault((l.TenantId, l.CountId))))
            .Where(x => x.Actor is not null && x.Line.TenantId is not null && x.Line.LegalEntityId is not null)
            .Select(x => new CycleCountPendingRow(x.Line.TenantId!.Value, x.Line.LegalEntityId!.Value, x.Line.Id, x.Actor!.Value))
            .ToList();
    }
}

/// <summary>What one sweeper tick did (for the log and the tests).</summary>
public sealed record CycleCountReconcileTick(int Scanned, int CommandsSent, int Failures);

/// <summary>
/// The MOD-0181 sweeper (pack §8.6, MOD-0178 <c>PutawayReconciler</c> pattern, F-18): per Pending line, a fresh DI scope in the line's tenant +
/// company with its actor and a new correlation, and ONE <c>ReconcilePendingCountAdjustmentCommand</c> through MediatR — the command asks the ledger
/// key first and writes through its own repository's transaction. The sweeper itself opens no transaction and enlists nothing. One line's
/// failure never stops the others.
/// </summary>
public sealed class CycleCountReconciler
{
    private readonly IServiceScopeFactory _scopes;
    private readonly CycleCountReconcilerOptions _options;
    private readonly ILogger<CycleCountReconciler> _logger;

    public CycleCountReconciler(IServiceScopeFactory scopes, IOptions<CycleCountReconcilerOptions> options, ILogger<CycleCountReconciler> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>One tick at <paramref name="now"/>; <paramref name="onlyTenants"/> narrows the scan (the shared test database).</summary>
    public async Task<CycleCountReconcileTick> RunOnceAsync(DateTimeOffset now, CancellationToken ct, IReadOnlyCollection<Guid>? onlyTenants = null)
    {
        IReadOnlyList<CycleCountPendingRow> rows;
        using (var scanScope = _scopes.CreateScope())
        {
            rows = await scanScope.ServiceProvider.GetRequiredService<ICycleCountPendingScan>()
                .PendingAsync(now - TimeSpan.FromSeconds(_options.AgeSeconds), _options.BatchSize, onlyTenants, ct);
        }

        var sent = 0;
        var failures = 0;
        foreach (var row in rows)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(row.TenantId, row.LegalEntityId, row.ActorUserId);
                scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ReconcilePendingCountAdjustmentCommand(row.LineId), ct);
                sent++;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
            {
                failures++;
                _logger.LogError(ex, "cycle-count.reconcile_failed {LineId} tenant {TenantId} company {LegalEntityId}", row.LineId, row.TenantId, row.LegalEntityId);
            }
        }

        return new CycleCountReconcileTick(rows.Count, sent, failures);
    }
}

/// <summary>The tick of <see cref="CycleCountReconciler"/>; off when <c>CycleCount:Reconciler:Enabled</c> is false.</summary>
public sealed class PendingCountAdjustmentReconcilerHostedService : BackgroundService
{
    private readonly CycleCountReconciler _reconciler;
    private readonly CycleCountReconcilerOptions _options;
    private readonly ILogger<PendingCountAdjustmentReconcilerHostedService> _logger;

    public PendingCountAdjustmentReconcilerHostedService(CycleCountReconciler reconciler, IOptions<CycleCountReconcilerOptions> options,
        ILogger<PendingCountAdjustmentReconcilerHostedService> logger)
    {
        _reconciler = reconciler;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var tick = await _reconciler.RunOnceAsync(DateTimeOffset.UtcNow, stoppingToken);
                    if (tick.CommandsSent > 0 || tick.Failures > 0)
                        _logger.LogWarning("MOD-0181 reconciler sent {Sent} command(s), {Failures} failure(s).", tick.CommandsSent, tick.Failures);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MOD-0181 reconciler tick failed; the next tick retries.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
