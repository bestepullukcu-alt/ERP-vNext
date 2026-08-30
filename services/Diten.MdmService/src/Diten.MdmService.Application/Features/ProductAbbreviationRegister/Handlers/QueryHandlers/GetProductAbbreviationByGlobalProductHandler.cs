using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Queries;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.QueryHandlers;

public sealed class GetProductAbbreviationByGlobalProductHandler
    : IRequestHandler<GetProductAbbreviationByGlobalProductQuery, Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    private readonly ProductAbbreviationScopeGuard _scopeGuard;
    public GetProductAbbreviationByGlobalProductHandler(
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
        GetProductAbbreviationByGlobalProductQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.EvaluateGlobalProductAsync(
            request.GlobalProductId,
            "mdm.product-abbreviations.read",
            cancellationToken);
        return scope.IsSuccessful
            ? await _workflow.GetByGlobalProductAsync(request.GlobalProductId, cancellationToken)
            : Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
    }
}

public sealed class ProductAbbreviationScopeGuard
{
    private readonly IProductAbbreviationRegisterRepository _register;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public ProductAbbreviationScopeGuard(
        IProductAbbreviationRegisterRepository register,
        IGlobalProductRepository globalProducts,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _register = register;
        _globalProducts = globalProducts;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<ProductAbbreviationScopeResult> EvaluateGlobalProductAsync(
        Guid globalProductId,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.ResolveContextAsync(permissionKey, cancellationToken);
        if (!scope.IsSuccessful)
        {
            return ProductAbbreviationScopeResult.Fail(scope.StatusCode, scope.FailureCode!);
        }
        var context = scope.Context!;
        if (context.RolloutMode == ProductLegalEntityScopeRolloutMode.Enforced
            && await _globalProducts.GetByIdAsync(globalProductId, cancellationToken) is null)
        {
            return ProductAbbreviationScopeResult.Fail(404, "ABBREVIATION_NOT_FOUND");
        }
        var decision = await _scopeGuard.EvaluateAsync(context, globalProductId, cancellationToken);
        return decision.Allowed
            ? ProductAbbreviationScopeResult.Success(globalProductId)
            : ProductAbbreviationScopeResult.Fail(404, "ABBREVIATION_NOT_FOUND");
    }

    public async Task<ProductAbbreviationScopeResult> EvaluateRegisterEntryAsync(
        Guid registerEntryId,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.ResolveContextAsync(permissionKey, cancellationToken);
        if (!scope.IsSuccessful)
        {
            return ProductAbbreviationScopeResult.Fail(scope.StatusCode, scope.FailureCode!);
        }
        var context = scope.Context!;
        var entry = await _register.GetByIdAsync(registerEntryId, cancellationToken);
        var product = entry is null || context.RolloutMode != ProductLegalEntityScopeRolloutMode.Enforced
            ? null
            : await _globalProducts.GetByIdAsync(entry.GlobalProductId, cancellationToken);
        return entry is null
               || context.RolloutMode == ProductLegalEntityScopeRolloutMode.Enforced && product is null
            ? ProductAbbreviationScopeResult.Fail(404, "ABBREVIATION_NOT_FOUND")
            : EvaluateDecision(
                entry.GlobalProductId,
                await _scopeGuard.EvaluateAsync(context, entry.GlobalProductId, cancellationToken));
    }

    public async Task<ProductAbbreviationScopeResult> EvaluateAbbreviationAsync(
        string abbreviation,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.ResolveContextAsync(permissionKey, cancellationToken);
        if (!scope.IsSuccessful)
        {
            return ProductAbbreviationScopeResult.Fail(scope.StatusCode, scope.FailureCode!);
        }
        var context = scope.Context!;
        var normalized = string.IsNullOrWhiteSpace(abbreviation)
            ? string.Empty
            : abbreviation.Trim().ToUpperInvariant();
        var entry = normalized.Length == 0
            ? null
            : await _register.ResolveActiveAsync(normalized, cancellationToken);
        var product = entry is null || context.RolloutMode != ProductLegalEntityScopeRolloutMode.Enforced
            ? null
            : await _globalProducts.GetByIdAsync(entry.GlobalProductId, cancellationToken);
        return entry is null
               || context.RolloutMode == ProductLegalEntityScopeRolloutMode.Enforced && product is null
            ? ProductAbbreviationScopeResult.Fail(404, "ABBREVIATION_NOT_FOUND")
            : EvaluateDecision(
                entry.GlobalProductId,
                await _scopeGuard.EvaluateAsync(context, entry.GlobalProductId, cancellationToken));
    }

    private static ProductAbbreviationScopeResult EvaluateDecision(
        Guid globalProductId,
        ProductLegalEntityScopeConsumerDecision decision) => decision.Allowed
        ? ProductAbbreviationScopeResult.Success(globalProductId)
        : ProductAbbreviationScopeResult.Fail(404, "ABBREVIATION_NOT_FOUND");
}

public sealed record ProductAbbreviationScopeResult(
    bool IsSuccessful,
    int StatusCode,
    string? FailureCode,
    Guid GlobalProductId)
{
    public static ProductAbbreviationScopeResult Success(Guid globalProductId) =>
        new(true, 200, null, globalProductId);

    public static ProductAbbreviationScopeResult Fail(int statusCode, string failureCode) =>
        new(false, statusCode, failureCode, Guid.Empty);
}
