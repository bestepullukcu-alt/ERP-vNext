using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed record GlobalProductCorrectionRecoveryRunResult(
    int TenantCount, int OperationCount, int CompletedCount, int DeferredCount, int FailedCount)
{
    // BL-552: the same cycle, counted the way every product-identity flow counts it. Null for a disabled runner.
    public ProductIdentityRecoveryCycle? Cycle { get; init; }
}

public sealed class GlobalProductCorrectionRecoveryRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<GlobalProductCorrectionWorkflowWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<GlobalProductCorrectionRecoveryRunner>? logger = null,
    ProductIdentityRecoveryMetrics? metrics = null)
{
    public const string Flow = "GLOBAL_PRODUCT_CORRECTION";
    private readonly ILogger log = logger ?? NullLogger<GlobalProductCorrectionRecoveryRunner>.Instance;

    public async Task<GlobalProductCorrectionRecoveryRunResult> RunCycleAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.IsValid()) throw new InvalidOperationException(
            "GLOBAL_PRODUCT_CORRECTION_WORKER_CONFIGURATION_INVALID");
        if (!settings.Enabled) return new(0, 0, 0, 0, 0);
        var tenants = 0; var operations = 0; var completed = 0; var deferred = 0; var failed = 0;
        var cycle = new ProductIdentityRecoveryCycle();
        var execution = new GlobalProductCorrectionExecutionConfiguration(TimeSpan.FromSeconds(settings.LeaseSeconds),
            TimeSpan.FromSeconds(settings.RetryDelaySeconds), settings.QuarantineAfterFailedAttempts,
            TimeSpan.FromSeconds(settings.EffectiveMaxRetryDelaySeconds),
            TimeSpan.FromSeconds(settings.StartSettleSeconds));
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
                                $"gp-correction-worker-{Environment.ProcessId}", execution, cancellationToken);
                            if (result.Succeeded) completed++;
                            else if (result.StatusCode is 409 or 503) deferred++;
                            else failed++;
                            var after = result.Operation?.Checkpoint;
                            // Only what is STUCK (lock held for a person, or past the retry limit) is counted and said
                            // as an error — once, when it becomes stuck, with the cause before; a request that ended
                            // (lock given back) is information.
                            var ended = after == GlobalProductCorrectionCheckpoint.ManualReconciliationRequired
                                        && result.Operation?.RecoveryDisposition != ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired;
                            var stuck = !ended && (after == GlobalProductCorrectionCheckpoint.ManualReconciliationRequired
                                        || result.Operation?.LastFailureCode == GlobalProductCorrectionWorkflowProcessor.RetryLimitReached);
                            var wasStuck = operation.Checkpoint == GlobalProductCorrectionCheckpoint.ManualReconciliationRequired
                                           || operation.LastFailureCode == GlobalProductCorrectionWorkflowProcessor.RetryLimitReached;
                            var newlyStuck = stuck && !wasStuck;
                            cycle.Count(!ProductIdentityRecoveryCycle.WasNotClaimed(result.ErrorCode), newlyStuck,
                                result.Succeeded || ended || (after.HasValue && after != operation.Checkpoint));
                            if (newlyStuck)
                                log.LogError("{Flow} operation {OperationId} of tenant {TenantId} is stuck ({ResultCode}); the cause before: {CauseCode}.",
                                    Flow, operation.OperationId, tenantId, result.Operation?.LastFailureCode ?? result.ErrorCode,
                                    operation.LastFailureCode ?? "none");
                            else if (stuck)
                                log.LogInformation("{Flow} operation {OperationId} of tenant {TenantId} is still stuck ({ResultCode}).",
                                    Flow, operation.OperationId, tenantId, result.Operation?.LastFailureCode ?? result.ErrorCode);
                            else if (ended)
                                log.LogInformation("{Flow} operation {OperationId} of tenant {TenantId} ended ({ResultCode}).",
                                    Flow, operation.OperationId, tenantId, result.Operation?.LastFailureCode ?? result.ErrorCode);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                        catch (Exception exception)
                        {
                            failed++;
                            cycle.Failed++;
                            cycle.Seen++;
                            log.LogError("{Flow} operation {OperationId} of tenant {TenantId} failed with {ExceptionType}.",
                                Flow, operation.OperationId, tenantId, exception.GetType().Name);
                        }
                    }
                    operationCursor = page.NextAfterOperationId;
                } while (operationCursor.HasValue);
                await using (var countScope = scopeFactory.CreateAsyncScope())
                {
                    countScope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                    var repository = countScope.ServiceProvider.GetRequiredService<IGlobalProductCorrectionOperationRepository>();
                    cycle.InManualReconciliation += await repository.CountManualReconciliationAsync(cancellationToken);
                    cycle.NeedingAPerson += await repository.CountNeedingAPersonAsync(cancellationToken);
                }
            }
            tenantCursor = tenantPage.NextAfterTenantId;
        } while (tenantCursor.HasValue);
        cycle.Tenants = tenants;
        cycle.Log(log, Flow);
        metrics?.Record(Flow, cycle.InManualReconciliation, cycle.NeedingAPerson);
        return new(tenants, operations, completed, deferred, failed) { Cycle = cycle };
    }
}
