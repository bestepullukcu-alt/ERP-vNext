using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed record FirstGskuIdentityWorkflowRecoveryRunResult(
    int TenantCount,
    int OperationCount,
    int CompletedCount,
    int DeferredCount,
    int FailedCount)
{
    // FIX3 item 1 (B13): the same cycle, counted the way every GSKU flow counts it.
    // Null for a disabled runner (no cycle ran), so that two "nothing ran" results stay equal.
    public ProductIdentityRecoveryCycle? Cycle { get; init; }
}

public sealed class FirstGskuIdentityWorkflowRecoveryRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<FirstGskuIdentityWorkflowWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<FirstGskuIdentityWorkflowRecoveryRunner> logger,
    ProductIdentityRecoveryMetrics? metrics = null)
{
    public const string Flow = "FIRST_GSKU_IDENTITY";

    public async Task<FirstGskuIdentityWorkflowRecoveryRunResult> RunCycleAsync(
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
        var cycle = new ProductIdentityRecoveryCycle();
        Guid? tenantCursor = null;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            FirstGskuIdentityWorkflowTenantPartitionPage tenantPage;
            await using (var discoveryScope = scopeFactory.CreateAsyncScope())
            {
                tenantPage = await discoveryScope.ServiceProvider
                    .GetRequiredService<IFirstGskuIdentityWorkflowTenantPartitionDiscovery>()
                    .DiscoverAsync(tenantCursor, timeProvider.GetUtcNow().UtcTicks,
                        settings.TenantPageSize, cancellationToken);
            }

            foreach (var tenantId in tenantPage.TenantIds)
            {
                tenantCount++;
                FirstGskuIdentityWorkflowRecoveryCursor? operationCursor = null;
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await using var tenantScope = scopeFactory.CreateAsyncScope();
                    tenantScope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                    var repository = tenantScope.ServiceProvider
                        .GetRequiredService<IFirstGskuIdentityWorkflowOperationRepository>();
                    var page = await repository.DiscoverRecoverableAsync(
                        timeProvider.GetUtcNow().UtcTicks,
                        settings.OperationPageSize,
                        operationCursor,
                        cancellationToken);
                    var processor = tenantScope.ServiceProvider
                        .GetRequiredService<FirstGskuIdentityWorkflowProcessor>();
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
                            cycle.NotClaimed();
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
                            var after = result.Operation?.Checkpoint;
                            var quarantined = after == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
                                              && operation.Checkpoint != FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired;
                            cycle.Count(!ProductIdentityRecoveryCycle.WasNotClaimed(result.ErrorCode), quarantined,
                                result.Succeeded || (after.HasValue && after != operation.Checkpoint));
                            if (result.Succeeded)
                            {
                                completedCount++;
                            }
                            else if (result.Operation?.RecoveryDisposition is
                                     ProductIdentityWorkflowRecoveryDisposition.Retryable
                                     or ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay)
                            {
                                deferredCount++;
                            }
                            else
                            {
                                failedCount++;
                                logger.LogError(
                                    "First GSKU identity workflow recovery failed for tenant {TenantId} and operation {OperationId} with {ErrorCode}.",
                                    tenantId,
                                    operation.OperationId,
                                    SanitizeErrorCode(result.ErrorCode));
                            }
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            failedCount++;
                            cycle.Failed++;
                            cycle.Seen++;
                            logger.LogError(
                                "First GSKU identity workflow recovery failed for tenant {TenantId} and operation {OperationId} with {ErrorCode}.",
                                tenantId,
                                operation.OperationId,
                                SanitizeErrorCode($"UNHANDLED_{exception.GetType().Name.ToUpperInvariant()}"));
                        }
                    }

                    operationCursor = page.NextCursor;
                } while (operationCursor is not null);
            }

            tenantCursor = tenantPage.NextAfterTenantId;
        } while (tenantCursor.HasValue);

        await using (var countScope = scopeFactory.CreateAsyncScope())
        {
            cycle.InManualReconciliation = await countScope.ServiceProvider
                .GetRequiredService<IFirstGskuIdentityWorkflowTenantPartitionDiscovery>()
                .CountManualReconciliationAsync(cancellationToken);
        }
        cycle.Tenants = tenantCount;
        cycle.Log(logger, Flow);
        metrics?.Record(Flow, cycle.InManualReconciliation);
        return new(tenantCount, operationCount, completedCount, deferredCount, failedCount) { Cycle = cycle };
    }

    private static string SanitizeErrorCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
        {
            return "UNEXPECTED_FAILURE";
        }

        return value.All(character => character is >= 'A' and <= 'Z'
                                      || character is >= '0' and <= '9'
                                      || character == '_')
            ? value
            : "UNEXPECTED_FAILURE";
    }
}
