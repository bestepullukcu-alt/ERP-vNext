using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers;

public sealed class ReplaceProductLegalEntityScopePolicyHandler
    : IRequestHandler<ReplaceProductLegalEntityScopePolicyCommand, Response<ProductLegalEntityScopeModels.PolicyDto>>
{
    private readonly IProductLegalEntityScopePolicyRepository _policies;
    private readonly IProductLegalEntityScopeRolloutStateRepository _rollout;
    private readonly ILegalEntityRepository _legalEntities;
    private readonly ProductLegalEntityScopeCandidateFacade _candidates;
    private readonly IProductIdentityActorContext _actor;

    public ReplaceProductLegalEntityScopePolicyHandler(
        IProductLegalEntityScopePolicyRepository policies,
        IProductLegalEntityScopeRolloutStateRepository rollout,
        ILegalEntityRepository legalEntities,
        ProductLegalEntityScopeCandidateFacade candidates,
        IProductIdentityActorContext actor)
    {
        _policies = policies;
        _rollout = rollout;
        _legalEntities = legalEntities;
        _candidates = candidates;
        _actor = actor;
    }

    public async Task<Response<ProductLegalEntityScopeModels.PolicyDto>> Handle(
        ReplaceProductLegalEntityScopePolicyCommand request,
        CancellationToken cancellationToken)
    {
        if ((await _rollout.GetAsync(cancellationToken))?.Mode != ProductLegalEntityScopeRolloutMode.Preparation)
            return CreateProductLegalEntityScopePolicyHandler.Fail("PRODUCT_SCOPE_ROLLOUT_NOT_PREPARATION");
        var policy = await _policies.GetByGlobalProductIdAsync(request.GlobalProductId, cancellationToken);
        if (policy is null)
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail("PRODUCT_SCOPE_POLICY_NOT_FOUND", 404);

        if (HasCommand(policy, request.CommandId))
            return ExactReplay(policy, request)
                ? Response<ProductLegalEntityScopeModels.PolicyDto>.Success(
                    CreateProductLegalEntityScopePolicyHandler.Map(policy))
                : CreateProductLegalEntityScopePolicyHandler.Fail("IDEMPOTENCY_KEY_CONFLICT");
        if (request.Request.Mode == ProductLegalEntityScopeMode.Scoped)
        {
            var candidate = await _candidates.ResolveAsync(
                "product-item-sku-master",
                "mdm.product-legal-entity-scopes.replace",
                cancellationToken);
            if (!candidate.IsSuccessful)
                return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail(
                    candidate.FailureCode ?? "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE",
                    candidate.StatusCode);
            if (request.Request.LegalEntityIds.Any(id => !candidate.LegalEntityIds.Contains(id)))
                return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail(
                    "LEGAL_ENTITY_REFERENCE_NOT_FOUND", 404);
            if ((await _legalEntities.GetReferenceableByIdsAsync(
                    request.Request.LegalEntityIds, cancellationToken)).Count
                != request.Request.LegalEntityIds.Count)
                return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail(
                    "LEGAL_ENTITY_REFERENCE_NOT_FOUND", 404);
        }

        var actorId = CreateProductLegalEntityScopePolicyHandler.ParseActor(_actor.ActorId);
        var now = DateTimeOffset.UtcNow;
        try
        {
            policy.ReplaceCurrent(request.Request.ExpectedVersion, request.CommandId,
                request.Request.Mode, request.Request.LegalEntityIds, actorId, now);
        }
        catch (InvalidOperationException)
        {
            return CreateProductLegalEntityScopePolicyHandler.Fail("PRODUCT_SCOPE_CONFLICT");
        }
        policy.AuditIntents.Add(ProductLegalEntityScopeAuditIntentFactory.Create(
            policy, ProductAuditOperation.ProductLegalEntityScopePolicyReplaced,
            request.Request.ExpectedVersion, policy.Version, request.CommandId, actorId, now));
        ProductLegalEntityScopePolicyWriteResult result;
        try
        {
            result = await _policies.UpdateAsync(policy, request.Request.ExpectedVersion, cancellationToken);
        }
        catch (InvalidOperationException exception) when (
            CreateProductLegalEntityScopePolicyHandler.IsBsonBudgetFailure(exception))
        {
            return CreateProductLegalEntityScopePolicyHandler.Fail("PRODUCT_SCOPE_BSON_LIMIT_EXCEEDED");
        }
        if (result.WriteOutcomeAmbiguous)
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail(
                "PRODUCT_SCOPE_RECONCILIATION_REQUIRED", 202);
        if (!result.Succeeded || result.Policy is null)
            return CreateProductLegalEntityScopePolicyHandler.Fail("PRODUCT_SCOPE_CONFLICT");
        return Response<ProductLegalEntityScopeModels.PolicyDto>.Success(
            CreateProductLegalEntityScopePolicyHandler.Map(result.Policy));
    }

    private static bool HasCommand(Domain.Entities.ProductLegalEntityScopePolicy policy, Guid commandId) =>
        policy.ScopePeriods.Any(x => x.CommandId == commandId || x.EndCommandId == commandId);

    private static bool ExactReplay(
        Domain.Entities.ProductLegalEntityScopePolicy policy,
        ReplaceProductLegalEntityScopePolicyCommand request)
    {
        var period = policy.ScopePeriods.SingleOrDefault(x => x.CommandId == request.CommandId);
        var audit = policy.AuditIntents.SingleOrDefault(x =>
            x.CommandId == request.CommandId.ToString("D")
            && x.Operation == ProductAuditOperation.ProductLegalEntityScopePolicyReplaced);
        var ids = Domain.Entities.ProductLegalEntityScopePolicy.NormalizeLegalEntityIds(
            request.Request.Mode, request.Request.LegalEntityIds);
        return period is not null
            && audit is not null
            && audit.PreVersion == request.Request.ExpectedVersion
            && period.Mode == request.Request.Mode
            && period.LegalEntityIds.SequenceEqual(ids);
    }
}
