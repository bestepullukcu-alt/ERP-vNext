using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Queries;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.QueryHandlers;

public sealed class GetProductAbbreviationAllocationEvidenceHandler
    : IRequestHandler<GetProductAbbreviationAllocationEvidenceQuery, Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationEvidenceDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    private readonly ProductAbbreviationScopeGuard _scopeGuard;
    public GetProductAbbreviationAllocationEvidenceHandler(
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
    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationEvidenceDto>> Handle(
        GetProductAbbreviationAllocationEvidenceQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.EvaluateRegisterEntryAsync(
            request.RegisterEntryId,
            "mdm.product-abbreviations.audit",
            cancellationToken);
        return scope.IsSuccessful
            ? await _workflow.GetEvidenceAsync(request.RegisterEntryId, cancellationToken)
            : Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationEvidenceDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
    }
}
