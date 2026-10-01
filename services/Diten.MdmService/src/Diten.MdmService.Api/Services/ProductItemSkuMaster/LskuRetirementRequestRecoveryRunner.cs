using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed record LskuRetirementRequestRecoveryRunResult(int TenantCount, int OperationCount,
    int CompletedCount, int DeferredCount, int FailedCount);

public sealed class LskuRetirementRequestRecoveryRunner(IServiceScopeFactory scopeFactory,
    IOptions<LskuRetirementRequestWorkflowWorkerOptions> options, TimeProvider clock)
{
    public async Task<LskuRetirementRequestRecoveryRunResult> RunCycleAsync(CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.IsValid()) throw new InvalidOperationException("LSKU_RETIREMENT_WORKER_CONFIGURATION_INVALID");
        if (!settings.Enabled) return new(0, 0, 0, 0, 0);
        var tenants = 0; var count = 0; var completed = 0; var deferred = 0; var failed = 0;
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
                    foreach (var operation in page.Operations)
                    {
                        count++;
                        try
                        {
                            var result = await processor.RecoverAsync(operation,
                                $"lsku-retirement-worker-{Environment.ProcessId}",
                                new(TimeSpan.FromSeconds(settings.LeaseSeconds),
                                    TimeSpan.FromSeconds(settings.RetryDelaySeconds)), ct);
                            if (result.Succeeded) completed++;
                            else if (result.StatusCode is 409 or 503) deferred++;
                            else failed++;
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
