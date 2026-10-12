using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Behaviors;

public sealed class ProductLegalEntityScopeWriteFenceBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    // WP-MDM-BL598-01 (CT decision b): the coordinator (and the rollout store it takes, whose constructor reaches the
    // store) is built only for a MUTATION — a query never builds it. A mutation whose coordinator cannot be built because
    // the store cannot be reached is refused with the rollout store's coded 503 and its handler is NOT called: never a
    // write without the fence (fail closed). Any other construction failure is a defect and propagates (500).
    private readonly Lazy<ProductLegalEntityScopeWriteFenceCoordinator> _coordinatorFactory;
    public ProductLegalEntityScopeWriteFenceBehavior(Lazy<ProductLegalEntityScopeWriteFenceCoordinator> coordinator) =>
        _coordinatorFactory = coordinator;

    // Tests (InternalsVisibleTo): a coordinator already built. Internal so DI sees ONE public constructor.
    internal ProductLegalEntityScopeWriteFenceBehavior(ProductLegalEntityScopeWriteFenceCoordinator coordinator)
        : this(new Lazy<ProductLegalEntityScopeWriteFenceCoordinator>(coordinator)) { }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IProductLegalEntityScopeInventoryMutation mutation) return await next();
        if (request is ReplaceProductLegalEntityScopePolicyCommand) return await next();
        if (!typeof(TResponse).IsGenericType
            || typeof(TResponse).GetGenericTypeDefinition() != typeof(Response<>))
            throw new InvalidOperationException("PRODUCT_SCOPE_WRITE_RESPONSE_CONTRACT_UNSUPPORTED");
        ProductLegalEntityScopeWriteFenceCoordinator coordinator;
        try
        {
            coordinator = _coordinatorFactory.Value;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
                                          && Application.Common.StoreConnectionFailure.Is(exception))
        {
            return Failure(ProductLegalEntityScopeWriteFenceCoordinator.RolloutStoreUnavailableCode, 503);
        }
        var admission = await coordinator.EnterAsync(mutation, cancellationToken);
        if (!admission.IsAllowed) return Failure(admission.FailureCode!, admission.StatusCode);
        using var ambientLease = coordinator.Activate(admission);
        try
        {
            var response = await next();
            if (response is null
                || response.GetType().GetProperty("IsSuccessful")?.GetValue(response) is not bool successful
                || response.GetType().GetProperty("StatusCode")?.GetValue(response) is not int status)
                throw new InvalidOperationException("PRODUCT_SCOPE_WRITE_RESPONSE_CONTRACT_UNSUPPORTED");
            await coordinator.CompleteAsync(admission, successful, status, false, cancellationToken);
            return response;
        }
        catch
        {
            await coordinator.CompleteAsync(admission, false, 500, true, CancellationToken.None);
            throw;
        }
    }

    private static TResponse Failure(string code, int statusCode)
    {
        var responseType = typeof(TResponse);
        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(Response<>))
            throw new InvalidOperationException(code);
        var method = responseType.GetMethod(nameof(Response<NoContent>.Fail), [typeof(string), typeof(int)])!;
        return (TResponse)method.Invoke(null, [code, statusCode])!;
    }
}
