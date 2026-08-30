using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers;

public sealed class EndProductLegalEntityScopePolicyHandler
    : IRequestHandler<EndProductLegalEntityScopePolicyCommand, Response<ProductLegalEntityScopeModels.PolicyDto>>
{
    private readonly IProductLegalEntityScopePolicyRepository _policies;
    private readonly IProductLegalEntityScopeRolloutStateRepository _rollout;
    private readonly IProductIdentityActorContext _actor;

    public EndProductLegalEntityScopePolicyHandler(
        IProductLegalEntityScopePolicyRepository policies,
        IProductLegalEntityScopeRolloutStateRepository rollout,
        IProductIdentityActorContext actor)
    {
        _policies = policies;
        _rollout = rollout;
        _actor = actor;
    }

    public async Task<Response<ProductLegalEntityScopeModels.PolicyDto>> Handle(
        EndProductLegalEntityScopePolicyCommand request,
        CancellationToken cancellationToken)
    {
        if ((await _rollout.GetAsync(cancellationToken))?.Mode != ProductLegalEntityScopeRolloutMode.Preparation)
            return CreateProductLegalEntityScopePolicyHandler.Fail("PRODUCT_SCOPE_ROLLOUT_NOT_PREPARATION");
        var policy = await _policies.GetByGlobalProductIdAsync(request.GlobalProductId, cancellationToken);
        if (policy is null)
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail("PRODUCT_SCOPE_POLICY_NOT_FOUND", 404);
        var replay = policy.ScopePeriods.SingleOrDefault(x => x.EndCommandId == request.CommandId);
        if (replay is not null)
        {
            var audit = policy.AuditIntents.SingleOrDefault(x =>
                x.CommandId == request.CommandId.ToString("D")
                && x.Operation == ProductAuditOperation.ProductLegalEntityScopePolicyEnded);
            return audit?.PreVersion == request.Request.ExpectedVersion
                ? Response<ProductLegalEntityScopeModels.PolicyDto>.Success(
                    CreateProductLegalEntityScopePolicyHandler.Map(policy))
                : CreateProductLegalEntityScopePolicyHandler.Fail("IDEMPOTENCY_KEY_CONFLICT");
        }
        if (policy.ScopePeriods.Any(x => x.CommandId == request.CommandId))
            return CreateProductLegalEntityScopePolicyHandler.Fail("IDEMPOTENCY_KEY_CONFLICT");

        var actorId = CreateProductLegalEntityScopePolicyHandler.ParseActor(_actor.ActorId);
        var now = DateTimeOffset.UtcNow;
        try
        {
            policy.EndCurrent(request.Request.ExpectedVersion, request.CommandId, actorId, now);
        }
        catch (InvalidOperationException)
        {
            return CreateProductLegalEntityScopePolicyHandler.Fail("PRODUCT_SCOPE_CONFLICT");
        }
        policy.AuditIntents.Add(ProductLegalEntityScopeAuditIntentFactory.Create(
            policy, ProductAuditOperation.ProductLegalEntityScopePolicyEnded,
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
}
