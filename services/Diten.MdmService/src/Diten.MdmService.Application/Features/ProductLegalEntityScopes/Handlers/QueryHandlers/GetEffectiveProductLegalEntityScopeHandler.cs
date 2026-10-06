using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.QueryHandlers;

public sealed class GetEffectiveProductLegalEntityScopeHandler
    : IRequestHandler<GetEffectiveProductLegalEntityScopeQuery,
        Response<ProductLegalEntityScopeModels.EffectiveFactsDto>>
{
    private readonly ProductLegalEntityScopeProductAccess _access;
    private readonly IProductLegalEntityScopePolicyRepository _policies;

    public GetEffectiveProductLegalEntityScopeHandler(
        ProductLegalEntityScopeProductAccess access,
        IProductLegalEntityScopePolicyRepository policies)
    {
        _access = access;
        _policies = policies;
    }

    public async Task<Response<ProductLegalEntityScopeModels.EffectiveFactsDto>> Handle(
        GetEffectiveProductLegalEntityScopeQuery request,
        CancellationToken cancellationToken)
    {
        var access = await _access.ResolveAsync(
            request.GlobalProductId, ProductLegalEntityScopeProductAccess.ReadPermission, cancellationToken);
        if (!access.Allowed)
            return Response<ProductLegalEntityScopeModels.EffectiveFactsDto>.Fail(access.FailureCode!, access.StatusCode);
        var rollout = access.Context!.RolloutState;
        var policy = await _policies.GetByGlobalProductIdAsync(request.GlobalProductId, cancellationToken);
        var current = policy?.GetEffectivePeriod(access.Context.ServerNowUtc);
        return Response<ProductLegalEntityScopeModels.EffectiveFactsDto>.Success(new(
            request.GlobalProductId,
            rollout?.Mode.ToString() ?? "Uninitialized",
            policy is not null,
            policy?.Version,
            current is null ? null : CreateProductLegalEntityScopePolicyHandler.Map(current)));
    }
}
