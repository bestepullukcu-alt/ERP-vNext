using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed record GlobalProductCorrectionRecoveryRunResult(
    int TenantCount, int OperationCount, int CompletedCount, int DeferredCount, int FailedCount);

public sealed class GlobalProductCorrectionRecoveryRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<GlobalProductCorrectionWorkflowWorkerOptions> options,
    TimeProvider timeProvider)
{
    public async Task<GlobalProductCorrectionRecoveryRunResult> RunCycleAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.IsValid()) throw new InvalidOperationException(
            "GLOBAL_PRODUCT_CORRECTION_WORKER_CONFIGURATION_INVALID");
        if (!settings.Enabled) return new(0, 0, 0, 0, 0);
        var tenants = 0; var operations = 0; var completed = 0; var deferred = 0; var failed = 0;
        Guid? tenantCursor = null;
        do
        {
            GlobalProductIdentityWorkflowTenantPartitionPage tenantPage;
            await using (var scope = scopeFactory.CreateAsyncScope())
                tenantPage = await scope.ServiceProvider
                    .GetRequiredService<IGlobalProductCorrectionOperationRepository>()
                    .DiscoverTenantPartitionsAsync(tenantCursor, settings.BatchSize, cancellationToken);
            foreach (var tenantId in tenantPage.TenantIds)
            {
                tenants++;
                Guid? operationCursor = null;
                do
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                    var repository = scope.ServiceProvider
                        .GetRequiredService<IGlobalProductCorrectionOperationRepository>();
                    var page = await repository.DiscoverRecoverableAsync(timeProvider.GetUtcNow().UtcTicks,
                        settings.BatchSize, operationCursor, cancellationToken);
                    var processor = scope.ServiceProvider.GetRequiredService<GlobalProductCorrectionWorkflowProcessor>();
                    foreach (var operation in page.Operations)
                    {
                        operations++;
                        try
                        {
                            var result = await processor.RecoverAsync(operation,
                                $"gp-correction-worker-{Environment.ProcessId}",
                                new(TimeSpan.FromSeconds(settings.LeaseSeconds),
                                    TimeSpan.FromSeconds(settings.RetryDelaySeconds)), cancellationToken);
                            if (result.Succeeded) completed++;
                            else if (result.StatusCode is 409 or 503) deferred++;
                            else failed++;
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                        catch { failed++; }
                    }
                    operationCursor = page.NextAfterOperationId;
                } while (operationCursor.HasValue);
            }
            tenantCursor = tenantPage.NextAfterTenantId;
        } while (tenantCursor.HasValue);
        return new(tenants, operations, completed, deferred, failed);
    }
}
