using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.QueryHandlers;

public sealed class GetProductLegalEntityScopePolicyHandler
    : IRequestHandler<GetProductLegalEntityScopePolicyQuery, Response<ProductLegalEntityScopeModels.PolicyDto>>
{
    private readonly ProductLegalEntityScopeProductAccess _access;
    private readonly IProductLegalEntityScopePolicyRepository _policies;

    public GetProductLegalEntityScopePolicyHandler(
        ProductLegalEntityScopeProductAccess access,
        IProductLegalEntityScopePolicyRepository policies)
    {
        _access = access;
        _policies = policies;
    }

    public async Task<Response<ProductLegalEntityScopeModels.PolicyDto>> Handle(
        GetProductLegalEntityScopePolicyQuery request,
        CancellationToken cancellationToken)
    {
        // FIX1 S1: only a product the caller may see (Enforced: by their legal entities); else as if it did not exist.
        var access = await _access.ResolveAsync(
            request.GlobalProductId, ProductLegalEntityScopeProductAccess.ReadPermission, cancellationToken);
        if (!access.Allowed)
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail(access.FailureCode!, access.StatusCode);
        var policy = await _policies.GetByGlobalProductIdAsync(request.GlobalProductId, cancellationToken);
        return policy is null
            ? Response<ProductLegalEntityScopeModels.PolicyDto>.Fail("PRODUCT_SCOPE_POLICY_NOT_FOUND", 404)
            : Response<ProductLegalEntityScopeModels.PolicyDto>.Success(ProductLegalEntityScopeProductAccess.Mask(
                CreateProductLegalEntityScopePolicyHandler.Map(policy), access.Context!));
    }
}
