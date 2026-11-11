using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.SafetyStockService.Application;

public sealed class PolicyMutationValidationBehavior : IPipelineBehavior<PolicyMutationCommand, PolicyMutationResult>
{
    public Task<PolicyMutationResult> Handle(PolicyMutationCommand request,
        RequestHandlerDelegate<PolicyMutationResult> next, CancellationToken cancellationToken)
        => request.TenantId == Guid.Empty || request.ActorId == Guid.Empty || request.LegalEntityId == Guid.Empty
            ? Task.FromResult(new PolicyMutationResult(PolicyMutationStatus.Invalid)) : next();
}

public sealed class PolicyMutationLoggingBehavior(ILogger<PolicyMutationLoggingBehavior> logger)
    : IPipelineBehavior<PolicyMutationCommand, PolicyMutationResult>
{
    public async Task<PolicyMutationResult> Handle(PolicyMutationCommand request,
        RequestHandlerDelegate<PolicyMutationResult> next, CancellationToken cancellationToken)
    {
        var result = await next();
        logger.LogInformation("Safety stock {Operation} command completed with {Status}", request.Operation, result.Status);
        return result;
    }
}

public sealed class PolicyMutationExceptionBehavior(ILogger<PolicyMutationExceptionBehavior> logger)
    : IPipelineBehavior<PolicyMutationCommand, PolicyMutationResult>
{
    public async Task<PolicyMutationResult> Handle(PolicyMutationCommand request,
        RequestHandlerDelegate<PolicyMutationResult> next, CancellationToken cancellationToken)
    {
        try { return await next(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Safety stock {Operation} command failed", request.Operation);
            return new(PolicyMutationStatus.Unavailable);
        }
    }
}

public sealed class PolicyMutationPerformanceBehavior(ILogger<PolicyMutationPerformanceBehavior> logger)
    : IPipelineBehavior<PolicyMutationCommand, PolicyMutationResult>
{
    public async Task<PolicyMutationResult> Handle(PolicyMutationCommand request,
        RequestHandlerDelegate<PolicyMutationResult> next, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var result = await next();
        if (timer.ElapsedMilliseconds > 500)
            logger.LogWarning("Safety stock {Operation} command took {ElapsedMilliseconds} ms",
                request.Operation, timer.ElapsedMilliseconds);
        return result;
    }
}
