using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.QueryHandlers;

public sealed class GetProductLegalEntityScopeHistoryHandler
    : IRequestHandler<GetProductLegalEntityScopeHistoryQuery,
        Response<IReadOnlyList<ProductLegalEntityScopeModels.PeriodDto>>>
{
    private readonly ProductLegalEntityScopeProductAccess _access;
    private readonly IProductLegalEntityScopePolicyRepository _policies;

    public GetProductLegalEntityScopeHistoryHandler(
        ProductLegalEntityScopeProductAccess access,
        IProductLegalEntityScopePolicyRepository policies)
    {
        _access = access;
        _policies = policies;
    }

    public async Task<Response<IReadOnlyList<ProductLegalEntityScopeModels.PeriodDto>>> Handle(
        GetProductLegalEntityScopeHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var access = await _access.ResolveAsync(
            request.GlobalProductId, ProductLegalEntityScopeProductAccess.ReadPermission, cancellationToken);
        if (!access.Allowed)
            return Response<IReadOnlyList<ProductLegalEntityScopeModels.PeriodDto>>.Fail(
                access.FailureCode!, access.StatusCode);
        var policy = await _policies.GetByGlobalProductIdAsync(request.GlobalProductId, cancellationToken);
        if (policy is null)
            return Response<IReadOnlyList<ProductLegalEntityScopeModels.PeriodDto>>.Fail(
                "PRODUCT_SCOPE_POLICY_NOT_FOUND", 404);
        return Response<IReadOnlyList<ProductLegalEntityScopeModels.PeriodDto>>.Success(
            policy.ScopePeriods.Select(CreateProductLegalEntityScopePolicyHandler.Map).ToArray());
    }
}
