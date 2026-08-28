using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers;

public sealed class CreateProductLegalEntityScopePolicyHandler
    : IRequestHandler<CreateProductLegalEntityScopePolicyCommand, Response<ProductLegalEntityScopeModels.PolicyDto>>
{
    private readonly IProductLegalEntityScopePolicyRepository _policies;
    private readonly IProductLegalEntityScopeRolloutStateRepository _rollout;
    private readonly IGlobalProductRepository _products;
    private readonly ILegalEntityRepository _legalEntities;
    private readonly ProductLegalEntityScopeCandidateFacade _candidates;
    private readonly ITenantContext _tenant;
    private readonly IProductIdentityActorContext _actor;

    public CreateProductLegalEntityScopePolicyHandler(
        IProductLegalEntityScopePolicyRepository policies,
        IProductLegalEntityScopeRolloutStateRepository rollout,
        IGlobalProductRepository products,
        ILegalEntityRepository legalEntities,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenant,
        IProductIdentityActorContext actor)
    {
        _policies = policies;
        _rollout = rollout;
        _products = products;
        _legalEntities = legalEntities;
        _candidates = candidates;
        _tenant = tenant;
        _actor = actor;
    }

    public async Task<Response<ProductLegalEntityScopeModels.PolicyDto>> Handle(
        CreateProductLegalEntityScopePolicyCommand request,
        CancellationToken cancellationToken)
    {
        var rollout = await _rollout.GetAsync(cancellationToken);
        if (rollout?.Mode != ProductLegalEntityScopeRolloutMode.Preparation)
            return Fail("PRODUCT_SCOPE_ROLLOUT_NOT_PREPARATION");

        var replay = await _policies.GetByCreationCommandIdAsync(request.CommandId, cancellationToken);
        if (replay is not null)
            return ExactCreateReplay(replay, request)
                ? Response<ProductLegalEntityScopeModels.PolicyDto>.Success(Map(replay), 201)
                : Fail("IDEMPOTENCY_KEY_CONFLICT");

        if (await _products.GetByIdAsync(request.GlobalProductId, cancellationToken) is null)
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail("GLOBAL_PRODUCT_NOT_FOUND", 404);
        var trusted = await ValidateTrustedReferences(
            request.Request.Mode, request.Request.LegalEntityIds, cancellationToken);
        if (!trusted.IsSuccessful)
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail(
                trusted.ErrorCode!, trusted.StatusCode);

        var actorId = ParseActor(_actor.ActorId);
        var now = DateTimeOffset.UtcNow;
        var policy = ProductLegalEntityScopePolicy.Create(
            _tenant.TenantId,
            request.GlobalProductId,
            request.CommandId,
            request.Request.Mode,
            request.Request.LegalEntityIds,
            actorId,
            now);
        policy.AuditIntents.Add(ProductLegalEntityScopeAuditIntentFactory.Create(
            policy, ProductAuditOperation.ProductLegalEntityScopePolicyCreated,
            -1, 0, request.CommandId, actorId, now));

        ProductLegalEntityScopePolicyWriteResult result;
        try
        {
            result = await _policies.CreateAsync(policy, cancellationToken);
        }
        catch (InvalidOperationException exception) when (IsBsonBudgetFailure(exception))
        {
            return Fail("PRODUCT_SCOPE_BSON_LIMIT_EXCEEDED");
        }
        if (result.WriteOutcomeAmbiguous)
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail(
                "PRODUCT_SCOPE_RECONCILIATION_REQUIRED", 202);
        if (!result.Succeeded || result.Policy is null)
            return Fail("PRODUCT_SCOPE_CONFLICT");
        return Response<ProductLegalEntityScopeModels.PolicyDto>.Success(Map(result.Policy), 201);
    }

    private async Task<TrustedReferenceValidation> ValidateTrustedReferences(
        ProductLegalEntityScopeMode mode,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (mode == ProductLegalEntityScopeMode.GroupWide)
            return ids.Count == 0
                ? TrustedReferenceValidation.Success()
                : TrustedReferenceValidation.Fail(400, "PRODUCT_SCOPE_REQUEST_INVALID");
        var candidate = await _candidates.ResolveAsync(
            "product-item-sku-master",
            "mdm.product-legal-entity-scopes.configure",
            cancellationToken);
        if (!candidate.IsSuccessful)
            return TrustedReferenceValidation.Fail(
                candidate.StatusCode,
                candidate.FailureCode ?? "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE");
        if (ids.Any(id => !candidate.LegalEntityIds.Contains(id)))
            return TrustedReferenceValidation.Fail(404, "LEGAL_ENTITY_REFERENCE_NOT_FOUND");
        var entities = await _legalEntities.GetReferenceableByIdsAsync(ids, cancellationToken);
        return entities.Count == ids.Count
            ? TrustedReferenceValidation.Success()
            : TrustedReferenceValidation.Fail(404, "LEGAL_ENTITY_REFERENCE_NOT_FOUND");
    }

    private static bool ExactCreateReplay(
        ProductLegalEntityScopePolicy policy,
        CreateProductLegalEntityScopePolicyCommand request)
    {
        var period = policy.ScopePeriods.SingleOrDefault();
        var ids = ProductLegalEntityScopePolicy.NormalizeLegalEntityIds(
            request.Request.Mode, request.Request.LegalEntityIds);
        return policy.GlobalProductId == request.GlobalProductId
            && period is not null
            && period.Mode == request.Request.Mode
            && period.LegalEntityIds.SequenceEqual(ids);
    }

    internal static Guid ParseActor(string value) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("TRUSTED_ACTOR_REQUIRED");

    internal static ProductLegalEntityScopeModels.PolicyDto Map(ProductLegalEntityScopePolicy policy) => new(
        policy.Id,
        policy.GlobalProductId,
        policy.Version,
        policy.ScopePeriods.Select(Map).ToArray(),
        policy.CreatedAt,
        policy.UpdatedAt);

    internal static ProductLegalEntityScopeModels.PeriodDto Map(ProductLegalEntityScopePeriod period) => new(
        period.PeriodId,
        period.Mode,
        period.LegalEntityIds,
        period.EffectiveFromUtc,
        period.EffectiveToUtc);

    internal static Response<ProductLegalEntityScopeModels.PolicyDto> Fail(string code) =>
        Response<ProductLegalEntityScopeModels.PolicyDto>.Fail(code, 409);

    internal static bool IsBsonBudgetFailure(InvalidOperationException exception) =>
        exception.Message is "PRODUCT_LEGAL_ENTITY_SCOPE_BSON_LIMIT_EXCEEDED"
            or "PRODUCT_LEGAL_ENTITY_SCOPE_AUDIT_HEADROOM_EXCEEDED";

    private sealed record TrustedReferenceValidation(bool IsSuccessful, int StatusCode, string? ErrorCode)
    {
        public static TrustedReferenceValidation Success() => new(true, 200, null);
        public static TrustedReferenceValidation Fail(int statusCode, string code) => new(false, statusCode, code);
    }
}
