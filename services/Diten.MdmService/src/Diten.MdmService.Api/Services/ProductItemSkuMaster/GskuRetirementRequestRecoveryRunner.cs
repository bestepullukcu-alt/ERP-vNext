using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed record GskuRetirementRequestRecoveryRunResult(int TenantCount, int OperationCount,
    int CompletedCount, int DeferredCount, int FailedCount)
{
    // FIX3 item 1 (B13): the same cycle, counted the way every GSKU flow counts it.
    // Null for a disabled runner (no cycle ran), so that two "nothing ran" results stay equal.
    public ProductIdentityRecoveryCycle? Cycle { get; init; }
}

public sealed class GskuRetirementRequestRecoveryRunner(IServiceScopeFactory scopes,
    IOptions<GskuRetirementRequestWorkflowWorkerOptions> options, TimeProvider clock,
    ILogger<GskuRetirementRequestRecoveryRunner> logger, ProductIdentityRecoveryMetrics? metrics = null)
{
    public const string Flow = "GSKU_RETIREMENT_REQUEST";
    public const string PairRetirementFlow = "GSKU_PAIR_RETIREMENT";

    public async Task<GskuRetirementRequestRecoveryRunResult> RunCycleAsync(CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.IsValid()) throw new InvalidOperationException("GSKU_RETIREMENT_REQUEST_WORKER_CONFIGURATION_INVALID");
        if (!settings.Enabled) return new(0, 0, 0, 0, 0);
        var tenants = 0; var count = 0; var completed = 0; var deferred = 0; var failed = 0;
        var cycle = new ProductIdentityRecoveryCycle();
        long pairManual = 0;
        Guid? tenantCursor = null;
        do
        {
            GlobalProductIdentityWorkflowTenantPartitionPage tenantPage;
            await using (var scope = scopes.CreateAsyncScope())
                tenantPage = await scope.ServiceProvider.GetRequiredService<IGskuRetirementRequestWorkflowTenantDiscoveryRepository>()
                    .DiscoverAsync(tenantCursor, settings.BatchSize, ct);
            foreach (var tenantId in tenantPage.TenantIds)
            {
                tenants++; Guid? cursor = null;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                    var repo = scope.ServiceProvider.GetRequiredService<IGskuRetirementRequestOperationRepository>();
                    var page = await repo.DiscoverRecoverableAsync(clock.GetUtcNow().UtcTicks, settings.BatchSize, cursor, ct);
                    var processor = scope.ServiceProvider.GetRequiredService<GskuRetirementRequestWorkflowProcessor>();
                    var backgroundScope = scope.ServiceProvider.GetRequiredService<ProductIdentityBackgroundScope>();
                    var scopeMode = await backgroundScope.ResolveAsync(ct);
                    foreach (var operation in page.Operations)
                    {
                        count++;
                        // The same scope rule as every product-identity worker (it had none before GSKU FIX1).
                        if (!await backgroundScope.InScopeAsync(scopeMode, operation.GlobalProductId, ct))
                        {
                            deferred++;
                            cycle.NotClaimed();
                            continue;
                        }
                        try
                        {
                            var result = await processor.RecoverAsync(operation,
                                $"gsku-retirement-request-worker-{Environment.ProcessId}",
                                new GskuRetirementRequestExecutionConfiguration(TimeSpan.FromSeconds(settings.LeaseSeconds),
                                    TimeSpan.FromSeconds(settings.RetryDelaySeconds), settings.QuarantineAfterFailedAttempts,
                                    TimeSpan.FromSeconds(settings.EffectiveMaxRetryDelaySeconds),
                                    TimeSpan.FromSeconds(settings.StartSettleSeconds)), ct);
                            if (result.Succeeded) completed++; else if (result.StatusCode is 409 or 503) deferred++; else failed++;
                            var after = result.Operation?.Checkpoint;
                            // FIX4 item 4: only what is STUCK (handed to a person with the lock held, or past the retry
                            // limit) is counted and said as an error; a request that ended (lock given back) is information.
                            var ended = after == GskuRetirementRequestCheckpoint.ManualReconciliationRequired
                                        && result.Operation?.RecoveryDisposition != ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired;
                            var stuck = !ended && (after == GskuRetirementRequestCheckpoint.ManualReconciliationRequired
                                        || result.Operation?.LastFailureCode?.EndsWith("_RETRY_LIMIT_REACHED", StringComparison.Ordinal) == true);
                            // FIX5 item 7: said as an ERROR once — when the request BECOMES stuck — with the cause the
                            // attempt before it met (the record then holds only "…_RETRY_LIMIT_REACHED"); a request still
                            // retrying at the ceiling is information on the next cycles, and is not "quarantined" again.
                            var wasStuck = operation.Checkpoint == GskuRetirementRequestCheckpoint.ManualReconciliationRequired
                                           || operation.LastFailureCode?.EndsWith("_RETRY_LIMIT_REACHED", StringComparison.Ordinal) == true;
                            var newlyStuck = stuck && !wasStuck;
                            cycle.Count(!ProductIdentityRecoveryCycle.WasNotClaimed(result.ErrorCode), newlyStuck,
                                result.Succeeded || ended || (after.HasValue && after != operation.Checkpoint));
                            if (newlyStuck)
                                logger.LogError("{Flow} operation {OperationId} of tenant {TenantId} is stuck ({ResultCode}); the cause before: {CauseCode}.",
                                    Flow, operation.OperationId, tenantId, result.Operation?.LastFailureCode ?? result.ErrorCode,
                                    operation.LastFailureCode ?? "none");
                            else if (stuck)
                                logger.LogInformation("{Flow} operation {OperationId} of tenant {TenantId} is still stuck ({ResultCode}).",
                                    Flow, operation.OperationId, tenantId, result.Operation?.LastFailureCode ?? result.ErrorCode);
                            else if (ended)
                                logger.LogInformation("{Flow} operation {OperationId} of tenant {TenantId} ended ({ResultCode}).",
                                    Flow, operation.OperationId, tenantId, result.Operation?.LastFailureCode ?? result.ErrorCode);
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
                await using (var countScope = scopes.CreateAsyncScope())
                {
                    countScope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                    cycle.InManualReconciliation += await countScope.ServiceProvider
                        .GetRequiredService<IGskuRetirementRequestOperationRepository>().CountManualReconciliationAsync(ct);
                    // The pair retirement has no worker of its own: it runs inside this flow's apply step, and
                    // its quarantined operations are reported here, as their own flow.
                    pairManual += await countScope.ServiceProvider
                        .GetRequiredService<IFirstGskuIdentityRetirementOperationRepository>().CountManualReconciliationAsync(ct);
                }
            }
            tenantCursor = tenantPage.NextAfterTenantId;
        } while (tenantCursor.HasValue);
        cycle.Tenants = tenants;
        cycle.Log(logger, Flow);
        metrics?.Record(Flow, cycle.InManualReconciliation);
        metrics?.Record(PairRetirementFlow, pairManual);
        if (pairManual > 0)
            logger.LogWarning("{Flow}: {InManualReconciliation} pair retirements are in manual reconciliation.",
                PairRetirementFlow, pairManual);
        return new(tenants, count, completed, deferred, failed) { Cycle = cycle };
    }
}
