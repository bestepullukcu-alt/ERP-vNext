using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.SafetyStockService.Application;

public sealed class PolicyReadValidationBehavior : IPipelineBehavior<PolicyReadQuery, PolicyReadResult>
{
    public Task<PolicyReadResult> Handle(PolicyReadQuery request, RequestHandlerDelegate<PolicyReadResult> next, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || request.ActorId == Guid.Empty || request.LegalEntityId == Guid.Empty)
            return Task.FromResult(new PolicyReadResult(ReadStatus.Unavailable));
        if (request.Kind != PolicyReadKind.List && request.PolicyId is null)
            return Task.FromResult(new PolicyReadResult(ReadStatus.NotFound));
        if (request.Kind is PolicyReadKind.Revision or PolicyReadKind.Evaluation && request.ChildId is null)
            return Task.FromResult(new PolicyReadResult(ReadStatus.NotFound));
        return next();
    }
}

public sealed class PolicyReadLoggingBehavior(ILogger<PolicyReadLoggingBehavior> logger) : IPipelineBehavior<PolicyReadQuery, PolicyReadResult>
{
    public async Task<PolicyReadResult> Handle(PolicyReadQuery request, RequestHandlerDelegate<PolicyReadResult> next, CancellationToken cancellationToken)
    {
        var result = await next();
        logger.LogInformation("Safety stock {Kind} read completed with {Status}", request.Kind, result.Status);
        return result;
    }
}

public sealed class PolicyReadExceptionBehavior(ILogger<PolicyReadExceptionBehavior> logger) : IPipelineBehavior<PolicyReadQuery, PolicyReadResult>
{
    public async Task<PolicyReadResult> Handle(PolicyReadQuery request, RequestHandlerDelegate<PolicyReadResult> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Safety stock {Kind} read failed", request.Kind);
            return new(ReadStatus.Unavailable);
        }
    }
}

public sealed class PolicyReadPerformanceBehavior(ILogger<PolicyReadPerformanceBehavior> logger) : IPipelineBehavior<PolicyReadQuery, PolicyReadResult>
{
    public async Task<PolicyReadResult> Handle(PolicyReadQuery request, RequestHandlerDelegate<PolicyReadResult> next, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var result = await next();
        if (timer.ElapsedMilliseconds > 500)
            logger.LogWarning("Safety stock {Kind} read took {ElapsedMilliseconds} ms", request.Kind, timer.ElapsedMilliseconds);
        return result;
    }
}
