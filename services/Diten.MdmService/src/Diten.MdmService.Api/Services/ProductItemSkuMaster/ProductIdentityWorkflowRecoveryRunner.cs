using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed record ProductIdentityWorkflowRecoveryRunResult(
    int TenantCount,
    int OperationCount,
    int CompletedCount,
    int DeferredCount,
    int FailedCount);

public sealed class ProductIdentityWorkflowRecoveryRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<ProductIdentityWorkflowWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<ProductIdentityWorkflowRecoveryRunner> logger,
    ProductIdentityRecoveryMetrics? metrics = null)
{
    // BL-552: the Global Product identity flow's name on the stuck gauges.
    public const string Flow = "GLOBAL_PRODUCT_IDENTITY";

    public async Task<ProductIdentityWorkflowRecoveryRunResult> RunCycleAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = options.Value;
        settings.EnsureValidWhenEnabled();
        if (!settings.Enabled)
        {
            return new(0, 0, 0, 0, 0);
        }

        // A service that is not configured cannot do anything for any operation: the cycle ends before it looks at
        // one, and says so once.
        await using (var readinessScope = scopeFactory.CreateAsyncScope())
        {
            if (readinessScope.ServiceProvider.GetRequiredService<IProductIdentityWorkflowTransportReadiness>()
                    .FindConfigurationFault() is { } configurationFault)
            {
                logger.LogError(
                    "Product identity workflow recovery cycle skipped: the transport is not configured ({ResultCode}). No operation was touched.",
                    configurationFault);
                return new(0, 0, 0, 0, 0);
            }
        }

        var tenantCount = 0;
        var operationCount = 0;
        var completedCount = 0;
        var deferredCount = 0;
        var failedCount = 0;
        long inManualReconciliation = 0;
        long needingAPerson = 0;
        Guid? tenantCursor = null;
        var stopCycle = false;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            GlobalProductIdentityWorkflowTenantPartitionPage tenantPage;
            await using (var discoveryScope = scopeFactory.CreateAsyncScope())
            {
                tenantPage = await discoveryScope.ServiceProvider
                    .GetRequiredService<IGlobalProductIdentityWorkflowTenantPartitionDiscovery>()
                    .DiscoverAsync(tenantCursor, timeProvider.GetUtcNow().UtcTicks,
                        settings.TenantPageSize, cancellationToken);
            }

            foreach (var tenantId in tenantPage.TenantIds)
            {
                if (stopCycle)
                {
                    break;
                }

                tenantCount++;
                GlobalProductIdentityWorkflowRecoveryCursor? operationCursor = null;
                var skipTenant = false;
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await using var tenantScope = scopeFactory.CreateAsyncScope();
                    tenantScope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                    var repository = tenantScope.ServiceProvider
                        .GetRequiredService<IGlobalProductIdentityWorkflowOperationRepository>();
                    var page = await repository.DiscoverRecoverableAsync(
                        timeProvider.GetUtcNow().UtcTicks,
                        settings.OperationPageSize,
                        operationCursor,
                        cancellationToken);
                    var processor = tenantScope.ServiceProvider
                        .GetRequiredService<GlobalProductIdentityWorkflowProcessor>();
                    // The legal-entity scope rule for a worker (ProductIdentityBackgroundScope): an Enforced tenant is
                    // recovered too, operation by operation — out-of-scope operations are skipped, not the tenant.
                    var backgroundScope = tenantScope.ServiceProvider.GetRequiredService<ProductIdentityBackgroundScope>();
                    var scopeMode = await backgroundScope.ResolveAsync(cancellationToken);
                    foreach (var operation in page.Operations)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        operationCount++;
                        if (!await backgroundScope.InScopeAsync(scopeMode, operation.GlobalProductId, cancellationToken))
                        {
                            deferredCount++;
                            continue;
                        }
                        try
                        {
                            var result = await processor.RecoverAsync(
                                operation,
                                settings.LeaseOwner,
                                TimeSpan.FromSeconds(settings.LeaseSeconds),
                                TimeSpan.FromSeconds(settings.RetryDelaySeconds),
                                cancellationToken);
                            if (result.Succeeded)
                            {
                                completedCount++;
                            }
                            else if (result.FaultScope != ProductIdentityWorkflowFaultScope.None)
                            {
                                // The fault is not this operation's. A refused credential or token is the same for
                                // every tenant, so nothing else is tried this cycle; a missing grant is one tenant's,
                                // so only the rest of that tenant is left alone.
                                deferredCount++;
                                if (result.FaultScope == ProductIdentityWorkflowFaultScope.AllTenants)
                                {
                                    stopCycle = true;
                                    logger.LogError(
                                        "Product identity workflow recovery cycle stopped: the service identity was refused ({ResultCode}) at operation {OperationId} of tenant {TenantId}. The remaining operations of every tenant were left untouched.",
                                        result.ErrorCode, operation.OperationId, tenantId);
                                }
                                else
                                {
                                    skipTenant = true;
                                    logger.LogWarning(
                                        "Product identity workflow recovery skipped the rest of tenant {TenantId}: the service identity may not act for it ({ResultCode}), first met at operation {OperationId}.",
                                        tenantId, result.ErrorCode, operation.OperationId);
                                }

                                break;
                            }
                            else if (result.Operation?.RecoveryDisposition is
                                     Domain.Enums.ProductIdentityWorkflowRecoveryDisposition.Retryable
                                     or Domain.Enums.ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay)
                            {
                                deferredCount++;
                            }
                            else
                            {
                                failedCount++;
                            }
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            // The type and the operation only: an exception message can carry whatever the failing
                            // layer put into it.
                            failedCount++;
                            logger.LogError(
                                "Product identity workflow recovery of operation {OperationId} of tenant {TenantId} failed with {ExceptionType}.",
                                operation.OperationId, tenantId, exception.GetType().FullName);
                        }
                    }

                    operationCursor = page.NextCursor;
                } while (operationCursor is not null && !skipTenant && !stopCycle);

            }

            tenantCursor = tenantPage.NextAfterTenantId;
        } while (tenantCursor.HasValue && !stopCycle);

        // BL-552: what is stuck (held for a person), and of it what only a person can settle — in EVERY tenant that
        // holds a stuck submit, not only those with work due this cycle (the work discovery skips manual
        // reconciliation). Store reads only: counted even when the cycle stopped on a refused service identity.
        Guid? stuckCursor = null;
        do
        {
            GlobalProductIdentityWorkflowTenantPartitionPage stuckPage;
            await using (var discoveryScope = scopeFactory.CreateAsyncScope())
            {
                stuckPage = await discoveryScope.ServiceProvider
                    .GetRequiredService<IGlobalProductIdentityWorkflowTenantPartitionDiscovery>()
                    .DiscoverStuckAsync(stuckCursor, settings.TenantPageSize, cancellationToken);
            }
            foreach (var tenantId in stuckPage.TenantIds)
            {
                await using var countScope = scopeFactory.CreateAsyncScope();
                countScope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                var counts = countScope.ServiceProvider.GetRequiredService<IGlobalProductIdentityWorkflowOperationRepository>();
                inManualReconciliation += await counts.CountManualReconciliationAsync(cancellationToken);
                needingAPerson += await counts.CountNeedingAPersonAsync(cancellationToken);
            }
            stuckCursor = stuckPage.NextAfterTenantId;
        } while (stuckCursor.HasValue);
        metrics?.Record(Flow, inManualReconciliation, needingAPerson);
        if (needingAPerson > 0)
        {
            logger.LogWarning(
                "{Flow} recovery cycle: {InManualReconciliation} submit(s) in manual reconciliation, {NeedingAPerson} needing a person.",
                Flow, inManualReconciliation, needingAPerson);
        }
        return new(tenantCount, operationCount, completedCount, deferredCount, failedCount);
    }
}
