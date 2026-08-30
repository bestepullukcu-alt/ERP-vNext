using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.CommandHandlers;

public sealed class InitiateProductAbbreviationCorrectionHandler
    : IRequestHandler<InitiateProductAbbreviationCorrectionCommand, Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    private readonly ProductAbbreviationScopeGuard _scopeGuard;
    public InitiateProductAbbreviationCorrectionHandler(
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
        InitiateProductAbbreviationCorrectionCommand request,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.EvaluateRegisterEntryAsync(
            request.ActiveRegisterEntryId, "mdm.product-abbreviations.correct", cancellationToken);
        return scope.IsSuccessful
            ? await _workflow.InitiateCorrectionAsync(request, cancellationToken)
            : Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                scope.FailureCode!, scope.StatusCode);
    }
}
