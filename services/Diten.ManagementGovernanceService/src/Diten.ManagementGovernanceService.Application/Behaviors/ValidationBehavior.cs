using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Behaviors;

public sealed class ValidationBehavior<TRequest,TResponse> : IPipelineBehavior<TRequest,TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is DwsDispatchRequest dispatch)
        {
            if (dispatch.Context.TenantId == Guid.Empty || dispatch.Context.ActorId == Guid.Empty || string.IsNullOrWhiteSpace(dispatch.Context.IdempotencyKey))
                throw new DwsValidationException(DwsErrors.AuthenticationRequired);
            dispatch.Contract.Validate();
            _ = DwsAuthorizationManifest.RequireExact(dispatch.Operation);
        }
        return await next();
    }
}
