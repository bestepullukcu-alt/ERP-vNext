using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
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
    TimeProvider timeProvider)
{
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

        var tenantCount = 0;
        var operationCount = 0;
        var completedCount = 0;
        var deferredCount = 0;
        var failedCount = 0;
        Guid? tenantCursor = null;
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
                tenantCount++;
                GlobalProductIdentityWorkflowRecoveryCursor? operationCursor = null;
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
                    var recoveryAllowed = await IsBackgroundRecoveryAllowedAsync(
                        tenantScope.ServiceProvider,
                        tenantId,
                        cancellationToken);
                    foreach (var operation in page.Operations)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        operationCount++;
                        if (!recoveryAllowed)
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
                        catch
                        {
                            failedCount++;
                        }
                    }

                    operationCursor = page.NextCursor;
                } while (operationCursor is not null);
            }

            tenantCursor = tenantPage.NextAfterTenantId;
        } while (tenantCursor.HasValue);

        return new(tenantCount, operationCount, completedCount, deferredCount, failedCount);
    }

    private static async Task<bool> IsBackgroundRecoveryAllowedAsync(
        IServiceProvider services,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            var rollout = await services
                .GetRequiredService<IProductLegalEntityScopeRolloutStateRepository>()
                .GetAsync(cancellationToken);
            if (rollout is null)
            {
                return true;
            }

            rollout.EnsureValid();
            return rollout.TenantId == tenantId
                   && !rollout.IsDeleted
                   && rollout.Mode == ProductLegalEntityScopeRolloutMode.Preparation;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
