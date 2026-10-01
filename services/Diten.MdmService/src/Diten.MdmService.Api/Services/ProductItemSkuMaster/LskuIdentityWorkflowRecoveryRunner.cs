using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed record LskuIdentityWorkflowRecoveryRunResult(
    int TenantCount, int OperationCount, int CompletedCount, int DeferredCount, int FailedCount);

public sealed class LskuIdentityWorkflowRecoveryRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<LskuIdentityWorkflowWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<LskuIdentityWorkflowRecoveryRunner> logger)
{
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
                    var recoveryAllowed = await IsBackgroundRecoveryAllowedAsync(
                        scope.ServiceProvider, tenantId, cancellationToken);
                    foreach (var operation in page.Operations)
                    {
                        operations++;
                        if (!recoveryAllowed) { deferred++; continue; }
                        try
                        {
                            var result = await processor.RecoverAsync(operation, settings.LeaseOwner,
                                TimeSpan.FromSeconds(settings.LeaseSeconds),
                                TimeSpan.FromSeconds(settings.RetryDelaySeconds), cancellationToken);
                            if (result.Succeeded) completed++;
                            else if (result.Operation?.RecoveryDisposition is
                                ProductIdentityWorkflowRecoveryDisposition.Retryable
                                or ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay) deferred++;
                            else { failed++; logger.LogError("LSKU workflow recovery failed for {TenantId}/{OperationId}: {ErrorCode}", tenantId, operation.OperationId, Safe(result.ErrorCode)); }
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                        catch (Exception exception)
                        {
                            failed++;
                            logger.LogError("LSKU workflow recovery failed for {TenantId}/{OperationId}: {ErrorCode}", tenantId, operation.OperationId, Safe(exception.GetType().Name.ToUpperInvariant()));
                        }
                    }
                    cursor = page.NextCursor;
                } while (cursor is not null);
            }
            tenantCursor = tenantPage.NextAfterTenantId;
        } while (tenantCursor.HasValue);
        return new(tenants, operations, completed, deferred, failed);
    }

    private static string Safe(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 100 && value.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
            ? value : "UNEXPECTED_FAILURE";

    private static async Task<bool> IsBackgroundRecoveryAllowedAsync(
        IServiceProvider services, Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var rollout = await services
                .GetRequiredService<IProductLegalEntityScopeRolloutStateRepository>()
                .GetAsync(cancellationToken);
            if (rollout is null) return true;
            rollout.EnsureValid();
            return rollout.TenantId == tenantId && !rollout.IsDeleted
                   && rollout.Mode == ProductLegalEntityScopeRolloutMode.Preparation;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return false; }
    }
}
