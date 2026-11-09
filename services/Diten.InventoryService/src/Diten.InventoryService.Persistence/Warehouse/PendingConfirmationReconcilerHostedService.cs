using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.PutawayTasks.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Persistence.Warehouse;

/// <summary>What one reconciler tick did (for the log and the tests).</summary>
public sealed record ReconcileTick(int NormalScanned, int DefensiveScanned, int CommandsSent, int Failures);

/// <summary>
/// The Pending reconciler (pack §8.3 "Sweeper", Q-09; AC-66): two branches, each with its OWN in-memory cursor over <c>(TenantId, _id)</c> and its
/// own budget (≤ 200 per tick) — the normal branch (Pending, last attempt older than the configured age) and the defensive branch (refused on
/// a retry within 30 minutes). Per task: a fresh DI scope in the task's tenant + company with the confirmation's <c>ConfirmedBy</c> as actor
/// and a new correlation; the unmarked planner reads the ledger key; the internal command is sent ONLY when there is something to write.
/// One task's failure never stops the others. It opens no transaction itself (the command's repository does).
/// </summary>
public sealed class PutawayReconciler
{
    private readonly IServiceScopeFactory _scopes;
    private readonly WarehousePutawayOptions _options;
    private readonly ILogger<PutawayReconciler> _logger;
    private (Guid TenantId, Guid TaskId)? _normalCursor;
    private (Guid TenantId, Guid TaskId)? _defensiveCursor;

    public PutawayReconciler(IServiceScopeFactory scopes, IOptions<WarehousePutawayOptions> options, ILogger<PutawayReconciler> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>One tick at <paramref name="now"/>; <paramref name="onlyTenants"/> narrows the scan (the shared test database).</summary>
    public async Task<ReconcileTick> RunOnceAsync(DateTimeOffset now, CancellationToken ct, IReadOnlyCollection<Guid>? onlyTenants = null)
    {
        var budget = _options.ReconcileBatchSize;
        using var scanScope = _scopes.CreateScope();
        var scan = scanScope.ServiceProvider.GetRequiredService<IWarehousePendingScan>();
        var normal = await scan.PendingAsync(now - TimeSpan.FromSeconds(_options.ReconcileAgeSeconds), _normalCursor, budget, onlyTenants, ct);
        _normalCursor = normal.Count < budget ? null : (normal[^1].TenantId, normal[^1].TaskId);
        var defensive = await scan.DefensiveAsync(now - PutawayTaskRules.DefensiveWindow, _defensiveCursor, budget, onlyTenants, ct);
        _defensiveCursor = defensive.Count < budget ? null : (defensive[^1].TenantId, defensive[^1].TaskId);

        var sent = 0;
        var failures = 0;
        foreach (var (row, isDefensive) in normal.Select(r => (r, false)).Concat(defensive.Select(r => (r, true))))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(row.TenantId, row.LegalEntityId, row.ActorUserId);
                scope.ServiceProvider.GetService<IPostingActorName>()?.Use(row.ActorName);
                scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
                if (!await scope.ServiceProvider.GetRequiredService<PutawayReconcilePlanner>().NeedsWriteAsync(row.TaskId, isDefensive, ct)) continue;
                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ReconcilePendingConfirmationCommand(row.TaskId, isDefensive), ct);
                sent++;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
            {
                failures++;
                _logger.LogError(ex, "wms.putaway.reconcile_failed {TaskId} tenant {TenantId} company {LegalEntityId}", row.TaskId, row.TenantId, row.LegalEntityId);
            }
        }

        return new ReconcileTick(normal.Count, defensive.Count, sent, failures);
    }
}

/// <summary>The tick of <see cref="PutawayReconciler"/> (Q-09: every 60 s); off when <c>Warehouse:Putaway:ReconcilerEnabled</c> is false.</summary>
public sealed class PendingConfirmationReconcilerHostedService : BackgroundService
{
    private readonly PutawayReconciler _reconciler;
    private readonly WarehousePutawayOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<PendingConfirmationReconcilerHostedService> _logger;

    public PendingConfirmationReconcilerHostedService(PutawayReconciler reconciler, IOptions<WarehousePutawayOptions> options, TimeProvider clock,
        ILogger<PendingConfirmationReconcilerHostedService> logger)
    {
        _reconciler = reconciler;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ReconcilerEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.ReconcileIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var tick = await _reconciler.RunOnceAsync(_clock.GetUtcNow(), stoppingToken);
                    if (tick.CommandsSent > 0 || tick.Failures > 0)
                        _logger.LogWarning("MOD-0178 reconciler sent {Sent} command(s), {Failures} failure(s).", tick.CommandsSent, tick.Failures);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MOD-0178 reconciler tick failed; the next tick retries.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
