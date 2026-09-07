using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.CommandHandlers;

public sealed class ApproveProductAbbreviationAllocationHandler
    : IRequestHandler<ApproveProductAbbreviationAllocationCommand, Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    private readonly ProductAbbreviationScopeGuard _scopeGuard;
    public ApproveProductAbbreviationAllocationHandler(
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
    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>> Handle(
        ApproveProductAbbreviationAllocationCommand request,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.EvaluateRegisterEntryAsync(
            request.RegisterEntryId, "mdm.product-abbreviations.approve", cancellationToken);
        return scope.IsSuccessful
            ? await _workflow.ApproveAsync(request, cancellationToken)
            : Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                scope.FailureCode!, scope.StatusCode);
    }
}
