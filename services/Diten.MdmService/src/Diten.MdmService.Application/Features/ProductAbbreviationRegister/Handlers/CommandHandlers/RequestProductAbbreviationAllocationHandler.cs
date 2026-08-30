using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.CommandHandlers;

public sealed class RequestProductAbbreviationAllocationHandler
    : IRequestHandler<RequestProductAbbreviationAllocationCommand, Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    private readonly ProductAbbreviationScopeGuard _scopeGuard;
    public RequestProductAbbreviationAllocationHandler(
        ProductAbbreviationWorkflow workflow,
        IProductAbbreviationRegisterRepository register,
        IGlobalProductRepository globalProducts,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _workflow = workflow;
        _scopeGuard = new(register, globalProducts, rolloutStates, policies, candidates, tenantContext);
    }
    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>> Handle(
        RequestProductAbbreviationAllocationCommand request,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.EvaluateGlobalProductAsync(
            request.GlobalProductId,
            "mdm.product-abbreviations.request",
            cancellationToken);
        return scope.IsSuccessful
            ? await _workflow.RequestAsync(request, cancellationToken)
            : Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                scope.FailureCode!, scope.StatusCode);
    }
}
