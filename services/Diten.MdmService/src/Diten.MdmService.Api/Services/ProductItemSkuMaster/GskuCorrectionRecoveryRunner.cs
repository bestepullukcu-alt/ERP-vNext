using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed record GskuCorrectionRecoveryRunResult(int TenantCount, int OperationCount,
    int CompletedCount, int DeferredCount, int FailedCount)
{
    // FIX3 item 1 (B13): the same cycle, counted the way every GSKU flow counts it.
    // Null for a disabled runner (no cycle ran), so that two "nothing ran" results stay equal.
    public ProductIdentityRecoveryCycle? Cycle { get; init; }
}

public sealed class GskuCorrectionRecoveryRunner(IServiceScopeFactory scopes,
    IOptions<GskuCorrectionWorkflowWorkerOptions> options, TimeProvider clock,
    ILogger<GskuCorrectionRecoveryRunner> logger, ProductIdentityRecoveryMetrics? metrics = null)
{
    public const string Flow = "GSKU_CORRECTION";

    public async Task<GskuCorrectionRecoveryRunResult> RunCycleAsync(CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.IsValid()) throw new InvalidOperationException("GSKU_CORRECTION_WORKER_CONFIGURATION_INVALID");
        if (!settings.Enabled) return new(0, 0, 0, 0, 0);
        var tenants = 0; var count = 0; var completed = 0; var deferred = 0; var failed = 0;
        var cycle = new ProductIdentityRecoveryCycle();
        Guid? tenantCursor = null;
        do
        {
            GlobalProductIdentityWorkflowTenantPartitionPage tenantPage;
            await using (var scope = scopes.CreateAsyncScope())
                tenantPage = await scope.ServiceProvider.GetRequiredService<IGskuCorrectionWorkflowTenantDiscoveryRepository>()
                    .DiscoverAsync(tenantCursor, settings.BatchSize, ct);
            foreach (var tenantId in tenantPage.TenantIds)
            {
                tenants++; Guid? cursor = null;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                    var repo = scope.ServiceProvider.GetRequiredService<IGskuCorrectionWorkflowOperationRepository>();
                    var page = await repo.DiscoverRecoverableAsync(clock.GetUtcNow().UtcTicks, settings.BatchSize, cursor, ct);
                    var processor = scope.ServiceProvider.GetRequiredService<GskuCorrectionWorkflowProcessor>();
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
                                $"gsku-correction-worker-{Environment.ProcessId}",
                                new GskuCorrectionExecutionConfiguration(TimeSpan.FromSeconds(settings.LeaseSeconds),
                                    TimeSpan.FromSeconds(settings.RetryDelaySeconds), settings.QuarantineAfterFailedAttempts,
                                    TimeSpan.FromSeconds(settings.MaxRetryDelaySeconds),
                                    TimeSpan.FromSeconds(settings.StartSettleSeconds)), ct);
                            if (result.Succeeded) completed++; else if (result.StatusCode is 409 or 503) deferred++; else failed++;
                            var after = result.Operation?.Checkpoint;
                            var quarantined = after == GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired;
                            cycle.Count(!ProductIdentityRecoveryCycle.WasNotClaimed(result.ErrorCode), quarantined,
                                result.Succeeded || (after.HasValue && after != operation.Checkpoint));
                            if (quarantined)
                                logger.LogError("{Flow} operation {OperationId} of tenant {TenantId} is in manual reconciliation ({ResultCode}).",
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
                        .GetRequiredService<IGskuCorrectionWorkflowOperationRepository>().CountManualReconciliationAsync(ct);
                }
            }
            tenantCursor = tenantPage.NextAfterTenantId;
        } while (tenantCursor.HasValue);
        cycle.Tenants = tenants;
        cycle.Log(logger, Flow);
        metrics?.Record(Flow, cycle.InManualReconciliation);
        return new(tenants, count, completed, deferred, failed) { Cycle = cycle };
    }
}
