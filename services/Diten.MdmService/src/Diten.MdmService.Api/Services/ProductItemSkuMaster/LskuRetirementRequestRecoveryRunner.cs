using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed record LskuRetirementRequestRecoveryRunResult(int TenantCount, int OperationCount,
    int CompletedCount, int DeferredCount, int FailedCount)
{
    // LSKU FIX1 S4: the same cycle, counted the way every GSKU flow counts it (null when no cycle ran).
    public ProductIdentityRecoveryCycle? Cycle { get; init; }
}

/*
 * LSKU FIX1 S4 / B4 — the GSKU retirement request runner's shape: every failure is logged (none is swallowed), the
 * cycle is counted and logged once, the stuck requests (and those only a person can settle) feed the shared gauge,
 * and the scope rule is the LSKU's one place (LskuBackgroundScope): out-of-scope operations are skipped, not the tenant.
 */
public sealed class LskuRetirementRequestRecoveryRunner(IServiceScopeFactory scopeFactory,
    IOptions<LskuRetirementRequestWorkflowWorkerOptions> options, TimeProvider clock,
    ILogger<LskuRetirementRequestRecoveryRunner> logger, ProductIdentityRecoveryMetrics? metrics = null)
{
    public const string Flow = "LSKU_RETIREMENT_REQUEST";

    public async Task<LskuRetirementRequestRecoveryRunResult> RunCycleAsync(CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.IsValid()) throw new InvalidOperationException("LSKU_RETIREMENT_WORKER_CONFIGURATION_INVALID");
        if (!settings.Enabled) return new(0, 0, 0, 0, 0);
        var tenants = 0; var count = 0; var completed = 0; var deferred = 0; var failed = 0;
        var cycle = new ProductIdentityRecoveryCycle();
        Guid? tenantCursor = null;
        do
        {
            GlobalProductIdentityWorkflowTenantPartitionPage tenantPage;
            await using (var scope = scopeFactory.CreateAsyncScope())
                tenantPage = await scope.ServiceProvider.GetRequiredService<ILskuRetirementRequestOperationRepository>()
                    .DiscoverTenantPartitionsAsync(tenantCursor, settings.BatchSize, ct);
            foreach (var tenantId in tenantPage.TenantIds)
            {
                tenants++; Guid? cursor = null;
                do
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                    var repository = scope.ServiceProvider.GetRequiredService<ILskuRetirementRequestOperationRepository>();
                    var page = await repository.DiscoverRecoverableAsync(clock.GetUtcNow().UtcTicks,
                        settings.BatchSize, cursor, ct);
                    var processor = scope.ServiceProvider.GetRequiredService<LskuRetirementRequestWorkflowProcessor>();
                    var backgroundScope = scope.ServiceProvider.GetRequiredService<LskuBackgroundScope>();
                    var scopeMode = await backgroundScope.ResolveAsync(ct);
                    if (scopeMode == ProductIdentityBackgroundScope.TenantMode.Nothing && page.Operations.Count > 0)
                        logger.LogWarning("{Flow}: tenant {TenantId} is closed to background recovery (its scope rollout is suspended or cannot be read); {Count} operations wait.",
                            Flow, tenantId, page.Operations.Count);
                    foreach (var operation in page.Operations)
                    {
                        count++;
                        if (!await backgroundScope.InScopeForLskuAsync(scopeMode, operation.LskuId, ct))
                        {
                            deferred++;
                            cycle.NotClaimed();
                            continue;
                        }
                        try
                        {
                            var result = await processor.RecoverAsync(operation,
                                $"lsku-retirement-worker-{Environment.ProcessId}",
                                new(TimeSpan.FromSeconds(settings.LeaseSeconds),
                                    TimeSpan.FromSeconds(settings.RetryDelaySeconds), settings.QuarantineAfterFailedAttempts,
                                    TimeSpan.FromSeconds(settings.EffectiveMaxRetryDelaySeconds),
                                    TimeSpan.FromSeconds(settings.StartSettleSeconds)), ct);
                            if (result.Succeeded) completed++;
                            else if (result.StatusCode is 409 or 503) deferred++;
                            else failed++;
                            var after = result.Operation;
                            var ended = after?.Checkpoint == LskuRetirementRequestCheckpoint.ManualReconciliationRequired
                                        && after.RecoveryDisposition != ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired;
                            var stuck = after is not null && !ended && LskuRetirementRequestWorkflowProcessor.IsStuck(after);
                            var newlyStuck = stuck && !LskuRetirementRequestWorkflowProcessor.IsStuck(operation);
                            cycle.Count(!ProductIdentityRecoveryCycle.WasNotClaimed(result.ErrorCode), newlyStuck,
                                result.Succeeded || ended || (after is not null && after.Checkpoint != operation.Checkpoint));
                            if (newlyStuck)
                                logger.LogError("{Flow} operation {OperationId} of tenant {TenantId} is stuck ({ResultCode}); the cause before: {CauseCode}.",
                                    Flow, operation.OperationId, tenantId, after?.LastFailureCode ?? result.ErrorCode,
                                    operation.LastFailureCode ?? "none");
                            else if (stuck)
                                logger.LogInformation("{Flow} operation {OperationId} of tenant {TenantId} is still stuck ({ResultCode}).",
                                    Flow, operation.OperationId, tenantId, after?.LastFailureCode ?? result.ErrorCode);
                            else if (ended)
                                logger.LogInformation("{Flow} operation {OperationId} of tenant {TenantId} ended ({ResultCode}).",
                                    Flow, operation.OperationId, tenantId, after?.LastFailureCode ?? result.ErrorCode);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch (Exception exception)
                        {
                            failed++;
                            cycle.Failed++;
                            cycle.Seen++;
                            logger.LogError("{Flow} operation {OperationId} of tenant {TenantId} failed with {ExceptionType}.",
                                Flow, operation.OperationId, tenantId, exception.GetType().Name);
                        }
                    }
                    cursor = page.NextAfterOperationId;
                } while (cursor.HasValue);
                await using (var countScope = scopeFactory.CreateAsyncScope())
                {
                    countScope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                    var repository = countScope.ServiceProvider.GetRequiredService<ILskuRetirementRequestOperationRepository>();
                    cycle.InManualReconciliation += await repository.CountManualReconciliationAsync(ct);
                    cycle.NeedingAPerson += await repository.CountNeedingAPersonAsync(ct);
                }
            }
            tenantCursor = tenantPage.NextAfterTenantId;
        } while (tenantCursor.HasValue);
        cycle.Tenants = tenants;
        cycle.Log(logger, Flow);
        metrics?.Record(Flow, cycle.InManualReconciliation, cycle.NeedingAPerson);
        return new(tenants, count, completed, deferred, failed) { Cycle = cycle };
    }
}
