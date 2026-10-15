using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed record LskuIdentityWorkflowRecoveryRunResult(
    int TenantCount, int OperationCount, int CompletedCount, int DeferredCount, int FailedCount)
{
    // LSKU FIX2 item 3: the cycle counted the way every product-identity flow counts it (null: no cycle ran).
    public ProductIdentityRecoveryCycle? Cycle { get; init; }
}

public sealed class LskuIdentityWorkflowRecoveryRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<LskuIdentityWorkflowWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<LskuIdentityWorkflowRecoveryRunner> logger,
    ProductIdentityRecoveryMetrics? metrics = null)
{
    public const string Flow = "LSKU_IDENTITY";
    // BL-607 FIX1 item 3: an approval past the alert threshold is said when it crosses it, then at most once per this
    // interval while it stays past (not every cycle: 15 s × 24 h = ~5,760 lines a day per replica for one approval). An
    // hour keeps it in the day's log for whoever reads it, and the gauge carries the age between the lines.
    public static readonly TimeSpan OpenApprovalAlertRepeat = TimeSpan.FromHours(1);
    public const string OpenApprovalStartInvalid = "LSKU_IDENTITY_WORKFLOW_OPEN_APPROVAL_START_INVALID";
    private readonly object _alertGate = new();
    private DateTimeOffset? _openApprovalAlertedAt;

    public async Task<LskuIdentityWorkflowRecoveryRunResult> RunCycleAsync(
        CancellationToken cancellationToken = default,
        bool requireEnabled = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = options.Value;
        settings.EnsureValidWhenEnabled();
        if (!settings.Enabled)
        {
            if (requireEnabled)
                throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_RECOVERY_DISABLED");
            return new(0, 0, 0, 0, 0);
        }
        var tenants = 0; var operations = 0; var completed = 0; var deferred = 0; var failed = 0;
        var cycle = new ProductIdentityRecoveryCycle();
        Guid? tenantCursor = null;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            LskuIdentityWorkflowTenantPartitionPage tenantPage;
            await using (var scope = scopeFactory.CreateAsyncScope())
                tenantPage = await scope.ServiceProvider
                    .GetRequiredService<ILskuIdentityWorkflowTenantPartitionDiscovery>()
                    .DiscoverAsync(tenantCursor, timeProvider.GetUtcNow().UtcTicks,
                        settings.TenantPageSize, cancellationToken);
            foreach (var tenantId in tenantPage.TenantIds)
            {
                tenants++;
                LskuIdentityWorkflowRecoveryCursor? cursor = null;
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await using var scope = scopeFactory.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                    var page = await scope.ServiceProvider
                        .GetRequiredService<ILskuIdentityWorkflowOperationRepository>()
                        .DiscoverRecoverableAsync(timeProvider.GetUtcNow().UtcTicks,
                            settings.OperationPageSize, cursor, cancellationToken);
                    var processor = scope.ServiceProvider.GetRequiredService<LskuIdentityWorkflowProcessor>();
                    // LSKU FIX1 B4: the LSKU's one scope rule (LskuBackgroundScope) — in an Enforced tenant the
                    // operations whose product is in scope go on, the others are skipped (formerly the whole tenant).
                    var backgroundScope = scope.ServiceProvider.GetRequiredService<LskuBackgroundScope>();
                    var scopeMode = await backgroundScope.ResolveAsync(cancellationToken);
                    // S4: a tenant closed to recovery is said, not dropped silently.
                    if (scopeMode == ProductIdentityBackgroundScope.TenantMode.Nothing && page.Operations.Count > 0)
                        logger.LogWarning("LSKU identity recovery: tenant {TenantId} is closed to background recovery (its scope rollout is suspended or cannot be read); {Count} operations wait.",
                            tenantId, page.Operations.Count);
                    foreach (var operation in page.Operations)
                    {
                        operations++;
                        if (!await backgroundScope.InScopeForGskuAsync(scopeMode, operation.GskuId, cancellationToken))
                        {
                            deferred++;
                            cycle.NotClaimed();
                            logger.LogDebug("LSKU identity recovery: operation {OperationId} of tenant {TenantId} is out of the background scope.",
                                operation.OperationId, tenantId);
                            continue;
                        }
                        try
                        {
                            var result = await processor.RecoverAsync(operation, settings.LeaseOwner,
                                TimeSpan.FromSeconds(settings.LeaseSeconds),
                                TimeSpan.FromSeconds(settings.RetryDelaySeconds), cancellationToken);
                            var after = result.Operation;
                            // LSKU FIX2 item 3 (the GSKU runner's shape): STUCK = manual reconciliation held for a
                            // person or the requester, or waiting for the maker's replay — said ONCE, with its cause.
                            var stuck = after is not null && IsStuck(after);
                            var newlyStuck = stuck && !IsStuck(operation);
                            cycle.Count(!ProductIdentityRecoveryCycle.WasNotClaimed(result.ErrorCode), newlyStuck,
                                result.Succeeded || (after is not null && after.Checkpoint != operation.Checkpoint));
                            if (result.Succeeded) completed++;
                            else if (result.Operation?.RecoveryDisposition is
                                ProductIdentityWorkflowRecoveryDisposition.Retryable
                                or ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay) deferred++;
                            else if (!stuck) { failed++; logger.LogError("LSKU workflow recovery failed for {TenantId}/{OperationId}: {ErrorCode}", tenantId, operation.OperationId, Safe(result.ErrorCode)); }
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
                            logger.LogError("LSKU workflow recovery failed for {TenantId}/{OperationId}: {ErrorCode}", tenantId, operation.OperationId, Safe(exception.GetType().Name.ToUpperInvariant()));
                        }
                    }
                    cursor = page.NextCursor;
                } while (cursor is not null);
            }
            tenantCursor = tenantPage.NextAfterTenantId;
        } while (tenantCursor.HasValue);
        await using (var countScope = scopeFactory.CreateAsyncScope())
        {
            var discovery = countScope.ServiceProvider.GetRequiredService<ILskuIdentityWorkflowTenantPartitionDiscovery>();
            cycle.InManualReconciliation = await discovery.CountManualReconciliationAsync(cancellationToken);
            cycle.NeedingAPerson = await discovery.CountNeedingAPersonAsync(cancellationToken);
        }
        cycle.Tenants = tenants;
        cycle.Log(logger, Flow);
        metrics?.Record(Flow, cycle.InManualReconciliation, cycle.NeedingAPerson);
        await RecordOpenApprovalAsync(settings, cancellationToken);
        return new(tenants, operations, completed, deferred, failed) { Cycle = cycle };
    }

    /// <summary>
    /// BL-607 FIX1 item 1 — THE OPEN-APPROVAL GAUGE NEVER TAKES THE CYCLE DOWN. Its read and its conversion run after the
    /// cycle is counted and logged, in their own try / catch: a store that throws marks the gauge failed (the age is
    /// dropped, never left stale) and says so once per cycle at Error; a start that is not a valid instant (below 0 or past
    /// <see cref="DateTimeOffset.MaxValue"/>) is ignored by name, never thrown on every cycle. A cancellation still ends the
    /// cycle.
    /// </summary>
    private async Task RecordOpenApprovalAsync(LskuIdentityWorkflowWorkerOptions settings, CancellationToken cancellationToken)
    {
        var alertAfter = TimeSpan.FromMinutes(settings.OpenApprovalAlertAfterMinutes);
        TimeSpan? oldestOpenApproval = null;
        try
        {
            long? openedAt;
            await using (var scope = scopeFactory.CreateAsyncScope())
                openedAt = await scope.ServiceProvider.GetRequiredService<ILskuIdentityWorkflowTenantPartitionDiscovery>()
                    .OldestOpenApprovalStartedAtUtcTicksAsync(cancellationToken);
            if (openedAt is { } ticks)
            {
                if (ticks < 0 || ticks > DateTimeOffset.MaxValue.UtcTicks)
                    logger.LogError("{Flow}: the oldest open approval carries a start that is not an instant ({ResultCode}); it is ignored.",
                        Flow, OpenApprovalStartInvalid);
                else
                    oldestOpenApproval = timeProvider.GetUtcNow() - new DateTimeOffset(ticks, TimeSpan.Zero);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            metrics?.RecordOpenApprovalReadFailed(Flow, alertAfter);
            logger.LogError("{Flow}: the oldest open approval could not be read ({ErrorCode}); the cycle is counted, the open-approval gauge is marked failed.",
                Flow, Safe(exception.GetType().Name.ToUpperInvariant()));
            return;
        }

        metrics?.RecordOldestOpenApproval(Flow, oldestOpenApproval, alertAfter);
        if (!ShouldSayOpenApprovalAlert(oldestOpenApproval > alertAfter))
            return;
        logger.LogWarning("{Flow}: the oldest approval still open has waited {AgeMinutes} min, past the alert threshold of {ThresholdMinutes} min (a person has not decided; nothing is stuck).",
            Flow, (long)oldestOpenApproval!.Value.TotalMinutes, settings.OpenApprovalAlertAfterMinutes);
    }

    // BL-607 FIX1 item 3: said when the threshold is crossed, then at most once per OpenApprovalAlertRepeat while it stays
    // crossed; once nothing is past the threshold the next crossing is said at once again.
    private bool ShouldSayOpenApprovalAlert(bool past)
    {
        lock (_alertGate)
        {
            if (!past)
            {
                _openApprovalAlertedAt = null;
                return false;
            }
            var now = timeProvider.GetUtcNow();
            if (_openApprovalAlertedAt is { } said && now - said < OpenApprovalAlertRepeat)
                return false;
            _openApprovalAlertedAt = now;
            return true;
        }
    }

    // FIX3 item 2: a wait that reached its limit (still Retryable, still waiting) is stuck too — as in the gauge.
    private static bool IsStuck(LskuIdentityWorkflowOperation operation) =>
        operation.Checkpoint == LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
        || operation.Checkpoint == LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
           && operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
        || operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
           && operation.LastFailureCode == LskuIdentityWorkflowProcessor.RetryLimitReached;

    private static string Safe(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 100 && value.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
            ? value : "UNEXPECTED_FAILURE";
}
