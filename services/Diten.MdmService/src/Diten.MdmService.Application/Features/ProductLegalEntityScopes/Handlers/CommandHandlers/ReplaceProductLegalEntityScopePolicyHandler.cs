using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
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
    private readonly IProductLegalEntityScopeWriterAuthorityProvider _authorityProvider;
    private readonly ProductLegalEntityScopeWriteFenceCoordinator _coordinator;

    public ReplaceProductLegalEntityScopePolicyHandler(
        IProductLegalEntityScopePolicyRepository policies,
        IProductLegalEntityScopeRolloutStateRepository rollout,
        ILegalEntityRepository legalEntities,
        ProductLegalEntityScopeCandidateFacade candidates,
        IProductLegalEntityScopeWriterAuthorityProvider authorityProvider,
        ProductLegalEntityScopeWriteFenceCoordinator coordinator)
    {
        _policies = policies;
        _rollout = rollout;
        _legalEntities = legalEntities;
        _candidates = candidates;
        _authorityProvider = authorityProvider;
        _coordinator = coordinator;
    }

    public async Task<Response<ProductLegalEntityScopeModels.PolicyDto>> Handle(
        ReplaceProductLegalEntityScopePolicyCommand request,
        CancellationToken cancellationToken)
    {
        var (unreadable, rollout) = await CreateProductLegalEntityScopePolicyHandler.TryReadRolloutAsync(
            _rollout, "replace", cancellationToken);
        if (unreadable is not null) return CreateProductLegalEntityScopePolicyHandler.RolloutUnreadable(unreadable);
        if (rollout?.Mode != ProductLegalEntityScopeRolloutMode.Preparation)
            return CreateProductLegalEntityScopePolicyHandler.Fail("PRODUCT_SCOPE_ROLLOUT_NOT_PREPARATION");
        var policy = await _policies.GetByGlobalProductIdAsync(request.GlobalProductId, cancellationToken);
        if (policy is null)
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail("PRODUCT_SCOPE_POLICY_NOT_FOUND", 404);

        var mutation = ProductLegalEntityScopeMutationIdentity.Create(request);
        var authority = await _authorityProvider.ResolveForegroundReplaceAsync(
            policy.Id,
            mutation,
            cancellationToken);
        if (authority is null)
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail(
                "PRODUCT_SCOPE_WRITER_AUTHORITY_REQUIRED",
                403);

        if (HasCommand(policy, request.CommandId))
            return ExactReplay(policy, request, authority)
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

        var now = DateTimeOffset.UtcNow;
        var before = policy.GetEffectivePeriod(now);
        try
        {
            policy.ReplaceCurrent(request.Request.ExpectedVersion, request.CommandId,
                request.Request.Mode, request.Request.LegalEntityIds, authority.SubjectId, now);
        }
        catch (InvalidOperationException)
        {
            return CreateProductLegalEntityScopePolicyHandler.Fail("PRODUCT_SCOPE_CONFLICT");
        }
        policy.AuditIntents.Add(ProductLegalEntityScopeAuditIntentFactory.Create(
            policy, ProductAuditOperation.ProductLegalEntityScopePolicyReplaced,
            request.Request.ExpectedVersion, policy.Version, request.CommandId, authority.SubjectId, now,
            before, policy.GetEffectivePeriod(now)));

        var admission = await _coordinator.EnterForegroundReplaceAsync(
            authority,
            request,
            cancellationToken);
        // FIX4 (5): the coordinator's own status travels (503 contended / store unavailable, 409 busy …).
        if (!admission.IsAllowed || admission.Context is null)
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail(
                admission.FailureCode ?? "PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", admission.StatusCode);

        ProductLegalEntityScopePolicyWriteResult result;
        try
        {
            result = await _policies.ReplaceAsync(
                authority,
                admission.Context.Lease,
                policy,
                request.Request.ExpectedVersion,
                cancellationToken);
        }
        catch (InvalidOperationException exception) when (
            CreateProductLegalEntityScopePolicyHandler.IsBsonBudgetFailure(exception))
        {
            // FIX4 (1): the budget is checked on the projected document BEFORE any write — nothing was written, so the
            // slot is released (principle A), not left held tenant-wide for its whole lease.
            await _coordinator.CompleteForegroundReplaceAsync(admission,
                new ProductLegalEntityScopePolicyWriteResult(false, null, VerifiedZeroMutation: true), CancellationToken.None);
            return CreateProductLegalEntityScopePolicyHandler.Fail("PRODUCT_SCOPE_BSON_LIMIT_EXCEEDED");
        }
        catch
        {
            // FIX4 (1): any other failure of the write — whether anything landed is not known: the slot is kept (its exit
            // is the same command repeated, or the lease's expiry) and counted, never silent.
            ProductLegalEntityScopeRolloutMetrics.CountRetained("replace_exception");
            throw;
        }

        await _coordinator.CompleteForegroundReplaceAsync(admission, result, cancellationToken);
        if (result.TransientRetriesExhausted)
            // FIX4 (4b): every retry of a transaction the server aborted as transient was used — nothing was written.
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail("PRODUCT_SCOPE_REPLACE_CONTENDED", 503);
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
        ReplaceProductLegalEntityScopePolicyCommand request,
        ProductLegalEntityScopeVerifiedWriterAuthority authority)
    {
        var period = policy.ScopePeriods.SingleOrDefault(x => x.CommandId == request.CommandId);
        var audit = policy.AuditIntents.SingleOrDefault(x =>
            x.CommandId == request.CommandId.ToString("D")
            && x.Operation == ProductAuditOperation.ProductLegalEntityScopePolicyReplaced);
        var ids = Domain.Entities.ProductLegalEntityScopePolicy.NormalizeLegalEntityIds(
            request.Request.Mode, request.Request.LegalEntityIds);
        return period is not null
            && audit is not null
            && audit.AggregateType == AuditAggregateType.ProductLegalEntityScopePolicy
            && audit.AggregateId == policy.Id
            && string.Equals(audit.ActorId, authority.SubjectId.ToString("D"), StringComparison.Ordinal)
            && audit.PreVersion == request.Request.ExpectedVersion
            && period.Mode == request.Request.Mode
            && period.LegalEntityIds.SequenceEqual(ids);
    }
}
