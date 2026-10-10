using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ItemMaster.Activation;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ItemMaster;

public sealed record ItemActivationRecoveryRunResult(int TenantCount, int OperationCount, int CompletedCount,
    int DeferredCount, int FailedCount)
{
    public ProductIdentityRecoveryCycle? Cycle { get; init; }
}

/// <summary>
/// MOD-0290-FU04 S2 — the item approval's recovery cycle (the LSKU identity runner's shape): every tenant with due work,
/// every due operation, one processor pass each; a newly stuck operation is said ONCE with its cause; the two gauges
/// are recorded after the cycle. Items are tenant-wide master data (no legal-entity scope): no scope filter.
/// </summary>
public sealed class ItemActivationWorkflowRecoveryRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<ItemActivationWorkflowWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<ItemActivationWorkflowRecoveryRunner> logger,
    ProductIdentityRecoveryMetrics? metrics = null)
{
    public const string Flow = ItemActivationWorkflowProcessor.Flow;

    public async Task<ItemActivationRecoveryRunResult> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = options.Value;
        settings.EnsureValidWhenEnabled();
        if (!settings.Enabled) return new(0, 0, 0, 0, 0);
        int tenants = 0, operations = 0, completed = 0, deferred = 0, failed = 0;
        var cycle = new ProductIdentityRecoveryCycle();
        Guid? tenantCursor = null;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            ItemWorkflowTenantPartitionPage tenantPage;
            await using (var scope = scopeFactory.CreateAsyncScope())
                tenantPage = await scope.ServiceProvider.GetRequiredService<IItemWorkflowTenantPartitionDiscovery>()
                    .DiscoverAsync(tenantCursor, timeProvider.GetUtcNow().UtcTicks, settings.TenantPageSize, cancellationToken);
            foreach (var tenantId in tenantPage.TenantIds)
            {
                tenants++;
                ItemWorkflowRecoveryCursor? cursor = null;
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await using var scope = scopeFactory.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                    var page = await scope.ServiceProvider.GetRequiredService<IItemWorkflowOperationRepository>()
                        .DiscoverRecoverableAsync(timeProvider.GetUtcNow().UtcTicks, settings.OperationPageSize, cursor,
                            cancellationToken);
                    var processor = scope.ServiceProvider.GetRequiredService<ItemActivationWorkflowProcessor>();
                    foreach (var operation in page.Operations)
                    {
                        operations++;
                        try
                        {
                            var result = await processor.RecoverAsync(operation, settings.LeaseOwner,
                                TimeSpan.FromSeconds(settings.LeaseSeconds), TimeSpan.FromSeconds(settings.RetryDelaySeconds),
                                cancellationToken);
                            var after = result.Operation;
                            var stuck = after is not null && IsStuck(after);
                            var newlyStuck = stuck && !IsStuck(operation);
                            cycle.Count(!ProductIdentityRecoveryCycle.WasNotClaimed(result.ErrorCode), newlyStuck,
                                result.Succeeded || after is not null && after.Checkpoint != operation.Checkpoint);
                            if (result.Succeeded) completed++;
                            else if (after?.RecoveryDisposition is ProductIdentityWorkflowRecoveryDisposition.Retryable
                                     or ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay) deferred++;
                            else failed++;
                            if (newlyStuck)
                                logger.LogError("{Flow} operation {OperationId} of tenant {TenantId} is stuck ({ResultCode}); the cause before: {CauseCode}.",
                                    Flow, operation.OperationId, tenantId, Safe(after!.LastFailureCode ?? result.ErrorCode),
                                    Safe(after.RetryLimitCauseCode ?? operation.LastFailureCode ?? "NONE"));
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                        catch (Exception exception)
                        {
                            failed++;
                            cycle.Failed++;
                            cycle.Seen++;
                            logger.LogError("{Flow} recovery failed for {TenantId}/{OperationId}: {ErrorCode}", Flow, tenantId,
                                operation.OperationId, Safe(exception.GetType().Name.ToUpperInvariant()));
                        }
                    }
                    cursor = page.NextCursor;
                } while (cursor is not null);
            }
            tenantCursor = tenantPage.NextAfterTenantId;
        } while (tenantCursor.HasValue);
        await using (var countScope = scopeFactory.CreateAsyncScope())
        {
            var discovery = countScope.ServiceProvider.GetRequiredService<IItemWorkflowTenantPartitionDiscovery>();
            var now = timeProvider.GetUtcNow().UtcTicks;
            cycle.InManualReconciliation = await discovery.CountManualReconciliationAsync(now, cancellationToken);
            cycle.NeedingAPerson = await discovery.CountNeedingAPersonAsync(now, cancellationToken);
        }
        cycle.Tenants = tenants;
        cycle.Log(logger, Flow);
        metrics?.Record(Flow, cycle.InManualReconciliation, cycle.NeedingAPerson);
        return new(tenants, operations, completed, deferred, failed) { Cycle = cycle };
    }

    // A wait that reached its limit (still Retryable, still waiting) is stuck too — as in the gauge.
    public static bool IsStuck(ItemWorkflowOperation operation) =>
        operation.Checkpoint == ItemWorkflowCheckpoint.AwaitingMakerReplay
        || operation.Checkpoint == ItemWorkflowCheckpoint.ManualReconciliationRequired
           && operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
        || operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
           && operation.LastFailureCode == ItemActivationWorkflowProcessor.RetryLimitReached;

    private static string Safe(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100
        && value.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_') ? value : "UNEXPECTED_FAILURE";
}

public sealed class ItemActivationWorkflowRecoveryWorker(
    ItemActivationWorkflowRecoveryRunner runner,
    IOptions<ItemActivationWorkflowWorkerOptions> options,
    ILogger<ItemActivationWorkflowRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        settings.EnsureValidWhenEnabled();
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await runner.RunCycleAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError("{Flow} recovery cycle failed: {ErrorCode}", ItemActivationWorkflowRecoveryRunner.Flow,
                    exception.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
        }
    }
}

/// <summary>
/// MOD-0290-FU04 S2 (§8.4 B, §13): the item gauges, tenant-free counts on their OWN always-on worker (production runs
/// with background jobs off — a gauge that only counts while the recovery worker runs would never count there):
/// ITEM_ACTIVATION manual_reconciliation / needing_a_person, and ITEM_ORPHAN_CODE (IT- codes held with no item row).
/// A failed count is flagged (count_failed = 1), never silent.
/// </summary>
public sealed class ItemActivationGauge(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ItemActivationGauge> logger,
    ProductIdentityRecoveryMetrics? metrics = null, IOptions<ItemActivationGaugeOptions>? options = null)
{
    public const string OrphanCodeFlow = "ITEM_ORPHAN_CODE";
    // S2 FIX1 item 2: approvals nobody is processing (the flow on, no worker carrying them) — counted, never silent.
    public const string UnprocessedFlow = "ITEM_ACTIVATION_UNPROCESSED";

    public async Task RecordAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var discovery = scope.ServiceProvider.GetRequiredService<IItemWorkflowTenantPartitionDiscovery>();
            var manual = await discovery.CountManualReconciliationAsync(now.UtcTicks, ct);
            var person = await discovery.CountNeedingAPersonAsync(now.UtcTicks, ct);
            metrics?.Record(ItemActivationWorkflowRecoveryRunner.Flow, manual, person);
            metrics?.RecordCountFailed(ItemActivationWorkflowRecoveryRunner.Flow, false);
            if (manual > 0)
                logger.LogWarning("{Flow}: {Count} item approvals are stuck ({Person} need a person).",
                    ItemActivationWorkflowRecoveryRunner.Flow, manual, person);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            metrics?.RecordCountFailed(ItemActivationWorkflowRecoveryRunner.Flow, true);
            logger.LogError("{Flow}: the count failed with {ExceptionType}; the gauges keep their last values.",
                ItemActivationWorkflowRecoveryRunner.Flow, exception.GetType().Name);
        }
        try
        {
            var after = (options?.Value ?? new ItemActivationGaugeOptions()).UnprocessedAfter;
            await using var scope = scopes.CreateAsyncScope();
            var unprocessed = await scope.ServiceProvider.GetRequiredService<IItemWorkflowTenantPartitionDiscovery>()
                .CountUnprocessedAsync((now - after).UtcTicks, ct);
            metrics?.Record(UnprocessedFlow, unprocessed);
            metrics?.RecordCountFailed(UnprocessedFlow, false);
            if (unprocessed > 0)
                logger.LogWarning("{Flow}: {Count} item approvals have not been processed for more than {Minutes} minutes (is an approval worker running?).",
                    UnprocessedFlow, unprocessed, (int)after.TotalMinutes);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            metrics?.RecordCountFailed(UnprocessedFlow, true);
            logger.LogError("{Flow}: the count failed with {ExceptionType}.", UnprocessedFlow, exception.GetType().Name);
        }
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var orphans = await scope.ServiceProvider.GetRequiredService<IItemOrphanCodeCensus>()
                .CountOrphanItemCodesAsync(now - ItemOrphanCodes.MinimumAge, ct);
            metrics?.Record(OrphanCodeFlow, orphans);
            metrics?.RecordCountFailed(OrphanCodeFlow, false);
            if (orphans > 0)
                logger.LogWarning("{Flow}: {Count} IT- code reservations have been held longer than {Minutes} minutes with no item row (swept by the operator repair).",
                    OrphanCodeFlow, orphans, (int)ItemOrphanCodes.MinimumAge.TotalMinutes);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            metrics?.RecordCountFailed(OrphanCodeFlow, true);
            logger.LogError("{Flow}: the count failed with {ExceptionType}.", OrphanCodeFlow, exception.GetType().Name);
        }
    }
}

/// <summary>
/// S2 FIX1 item 2 (CT decision: warn, never refuse — in a multi-instance deployment one instance carries the worker): the
/// approval is on while THIS instance's worker is off — said once at start; the unprocessed gauge counts what nobody carries.
/// </summary>
public sealed class ItemActivationWorkerOffWarning(
    IOptions<ItemActivationWorkflowOptions> workflow,
    IOptions<ItemActivationWorkflowWorkerOptions> worker,
    ILogger<ItemActivationWorkerOffWarning> logger) : IHostedService
{
    public const string Message =
        "ITEM_ACTIVATION: the item approval is on and this instance's approval worker is off — approvals are carried only if another instance runs the worker (ITEM_ACTIVATION_UNPROCESSED counts the ones nobody carries).";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (workflow.Value.Enabled && !worker.Value.Enabled) logger.LogWarning(Message);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ItemActivationGaugeWorker(ItemActivationGauge gauge, IOptions<ItemActivationGaugeOptions> options)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        var interval = TimeSpan.FromSeconds(Math.Clamp(settings.IntervalSeconds, 30, 3600));
        while (!ct.IsCancellationRequested)
        {
            await gauge.RecordAsync(ct);
            await Task.Delay(interval, ct);
        }
    }
}
