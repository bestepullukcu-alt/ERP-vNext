using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.SafetyStockService.Application;

public sealed class ManualQuantityValidationBehavior<TRequest>
    : IPipelineBehavior<TRequest, ManualQuantityResult> where TRequest : IRequest<ManualQuantityResult>
{
    public Task<ManualQuantityResult> Handle(TRequest request,
        RequestHandlerDelegate<ManualQuantityResult> next, CancellationToken ct)
    {
        var valid = request switch
        {
            ManualQuantityCommand command => command.TenantId != Guid.Empty &&
                command.LegalEntityId != Guid.Empty && command.ActorId != Guid.Empty &&
                command.SkuId != Guid.Empty && command.WarehouseId != Guid.Empty,
            EffectiveManualQuantityQuery query => query.TenantId != Guid.Empty &&
                query.LegalEntityId != Guid.Empty && query.ActorId != Guid.Empty &&
                query.SkuId != Guid.Empty && query.WarehouseId != Guid.Empty &&
                query.AsOf != default,
            _ => false
        };
        return valid ? next() : Task.FromResult(new ManualQuantityResult(ManualQuantityStatus.Invalid));
    }
}

public sealed class ManualQuantityLoggingBehavior<TRequest>(ILogger<ManualQuantityLoggingBehavior<TRequest>> logger)
    : IPipelineBehavior<TRequest, ManualQuantityResult> where TRequest : IRequest<ManualQuantityResult>
{
    public async Task<ManualQuantityResult> Handle(TRequest request,
        RequestHandlerDelegate<ManualQuantityResult> next, CancellationToken ct)
    {
        var result = await next();
        logger.LogInformation("Manual safety stock {RequestType} completed with {Status}",
            typeof(TRequest).Name, result.Status);
        return result;
    }
}

public sealed class ManualQuantityExceptionBehavior<TRequest>(ILogger<ManualQuantityExceptionBehavior<TRequest>> logger)
    : IPipelineBehavior<TRequest, ManualQuantityResult> where TRequest : IRequest<ManualQuantityResult>
{
    public async Task<ManualQuantityResult> Handle(TRequest request,
        RequestHandlerDelegate<ManualQuantityResult> next, CancellationToken ct)
    {
        try { return await next(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Manual safety stock {RequestType} failed", typeof(TRequest).Name);
            return new(ManualQuantityStatus.Unavailable);
        }
    }
}

public sealed class ManualQuantityPerformanceBehavior<TRequest>(ILogger<ManualQuantityPerformanceBehavior<TRequest>> logger)
    : IPipelineBehavior<TRequest, ManualQuantityResult> where TRequest : IRequest<ManualQuantityResult>
{
    public async Task<ManualQuantityResult> Handle(TRequest request,
        RequestHandlerDelegate<ManualQuantityResult> next, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        var result = await next();
        if (timer.ElapsedMilliseconds > 500)
            logger.LogWarning("Manual safety stock {RequestType} took {ElapsedMilliseconds} ms",
                typeof(TRequest).Name, timer.ElapsedMilliseconds);
        return result;
    }
}
