using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed record GskuCorrectionRecoveryRunResult(int TenantCount, int OperationCount,
    int CompletedCount, int DeferredCount, int FailedCount);

public sealed class GskuCorrectionRecoveryRunner(IServiceScopeFactory scopes,
    IOptions<GskuCorrectionWorkflowWorkerOptions> options, TimeProvider clock)
{
    public async Task<GskuCorrectionRecoveryRunResult> RunCycleAsync(CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.IsValid()) throw new InvalidOperationException("GSKU_CORRECTION_WORKER_CONFIGURATION_INVALID");
        if (!settings.Enabled) return new(0, 0, 0, 0, 0);
        var tenants = 0; var count = 0; var completed = 0; var deferred = 0; var failed = 0;
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
                    foreach (var operation in page.Operations)
                    {
                        count++;
                        try
                        {
                            var result = await processor.RecoverAsync(operation,
                                $"gsku-correction-worker-{Environment.ProcessId}",
                                new GskuCorrectionExecutionConfiguration(TimeSpan.FromSeconds(settings.LeaseSeconds),
                                    TimeSpan.FromSeconds(settings.RetryDelaySeconds)), ct);
                            if (result.Succeeded) completed++; else if (result.StatusCode is 409 or 503) deferred++; else failed++;
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch { failed++; }
                    }
                    cursor = page.NextAfterOperationId;
                } while (cursor.HasValue);
            }
            tenantCursor = tenantPage.NextAfterTenantId;
        } while (tenantCursor.HasValue);
        return new(tenants, count, completed, deferred, failed);
    }
}
