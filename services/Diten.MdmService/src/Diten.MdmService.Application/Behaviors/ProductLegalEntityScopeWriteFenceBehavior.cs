using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Behaviors;

public sealed class ProductLegalEntityScopeWriteFenceBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly ProductLegalEntityScopeWriteFenceCoordinator _coordinator;
    public ProductLegalEntityScopeWriteFenceBehavior(ProductLegalEntityScopeWriteFenceCoordinator coordinator) =>
        _coordinator = coordinator;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IProductLegalEntityScopeInventoryMutation mutation) return await next();
        if (request is ReplaceProductLegalEntityScopePolicyCommand) return await next();
        if (!typeof(TResponse).IsGenericType
            || typeof(TResponse).GetGenericTypeDefinition() != typeof(Response<>))
            throw new InvalidOperationException("PRODUCT_SCOPE_WRITE_RESPONSE_CONTRACT_UNSUPPORTED");
        var admission = await _coordinator.EnterAsync(mutation, cancellationToken);
        if (!admission.IsAllowed) return Failure(admission.FailureCode!);
        using var ambientLease = _coordinator.Activate(admission);
        try
        {
            var response = await next();
            if (response is null
                || response.GetType().GetProperty("IsSuccessful")?.GetValue(response) is not bool successful
                || response.GetType().GetProperty("StatusCode")?.GetValue(response) is not int status)
                throw new InvalidOperationException("PRODUCT_SCOPE_WRITE_RESPONSE_CONTRACT_UNSUPPORTED");
            await _coordinator.CompleteAsync(admission, successful, status, false, cancellationToken);
            return response;
        }
        catch
        {
            await _coordinator.CompleteAsync(admission, false, 500, true, CancellationToken.None);
            throw;
        }
    }

    private static TResponse Failure(string code)
    {
        var responseType = typeof(TResponse);
        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(Response<>))
            throw new InvalidOperationException(code);
        var method = responseType.GetMethod(nameof(Response<NoContent>.Fail), [typeof(string), typeof(int)])!;
        return (TResponse)method.Invoke(null, [code, 409])!;
    }
}
