using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Queries;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.QueryHandlers;

public sealed class ResolveProductAbbreviationHandler
    : IRequestHandler<ResolveProductAbbreviationQuery, Response<ProductAbbreviationRegisterModels.ProductAbbreviationResolutionDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    private readonly ProductAbbreviationScopeGuard _scopeGuard;
    public ResolveProductAbbreviationHandler(
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
    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationResolutionDto>> Handle(
        ResolveProductAbbreviationQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.EvaluateAbbreviationAsync(
            request.Abbreviation,
            "mdm.product-abbreviations.read",
            cancellationToken);
        return scope.IsSuccessful
            ? await _workflow.ResolveAsync(request.Abbreviation, cancellationToken)
            : Response<ProductAbbreviationRegisterModels.ProductAbbreviationResolutionDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
    }
}
