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
        TimeSpan? oldestOpenApproval = null;
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
            // BL-607: the oldest approval still open — from when the engine opened it (real progress), not the request.
            var openedAt = await discovery.OldestOpenApprovalStartedAtUtcTicksAsync(cancellationToken);
            oldestOpenApproval = openedAt is { } ticks
                ? timeProvider.GetUtcNow() - new DateTimeOffset(ticks, TimeSpan.Zero)
                : null;
        }
        cycle.Tenants = tenants;
        cycle.Log(logger, Flow);
        metrics?.Record(Flow, cycle.InManualReconciliation, cycle.NeedingAPerson);
        var alertAfter = TimeSpan.FromMinutes(settings.OpenApprovalAlertAfterMinutes);
        metrics?.RecordOldestOpenApproval(Flow, oldestOpenApproval, alertAfter);
        if (oldestOpenApproval > alertAfter)
            logger.LogWarning("{Flow}: the oldest approval still open has waited {AgeMinutes} min, past the alert threshold of {ThresholdMinutes} min (a person has not decided; nothing is stuck).",
                Flow, (long)oldestOpenApproval.Value.TotalMinutes, settings.OpenApprovalAlertAfterMinutes);
        return new(tenants, operations, completed, deferred, failed) { Cycle = cycle };
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
