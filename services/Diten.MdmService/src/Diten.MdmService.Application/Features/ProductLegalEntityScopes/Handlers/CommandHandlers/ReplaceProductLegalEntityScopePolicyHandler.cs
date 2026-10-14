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
    // BL-598 FIX1: the store-backed collaborators are built inside Handle (their constructors may reach Mongo), so "the
    // store cannot be reached" is this handler's coded 503, not an uncoded 500 from the container.
    private readonly Lazy<IProductLegalEntityScopeRolloutStateRepository> _rollout;
    private readonly Lazy<ILegalEntityRepository> _legalEntities;
    private readonly Lazy<ProductLegalEntityScopeCandidateFacade> _candidates;
    private readonly Lazy<IProductLegalEntityScopeWriterAuthorityProvider> _authorityProvider;
    private readonly Lazy<ProductLegalEntityScopeWriteFenceCoordinator> _coordinator;

    public ReplaceProductLegalEntityScopePolicyHandler(
        IProductLegalEntityScopePolicyRepository policies,
        Lazy<IProductLegalEntityScopeRolloutStateRepository> rollout,
        Lazy<ILegalEntityRepository> legalEntities,
        Lazy<ProductLegalEntityScopeCandidateFacade> candidates,
        Lazy<IProductLegalEntityScopeWriterAuthorityProvider> authorityProvider,
        Lazy<ProductLegalEntityScopeWriteFenceCoordinator> coordinator)
    {
        _policies = policies;
        _rollout = rollout;
        _legalEntities = legalEntities;
        _candidates = candidates;
        _authorityProvider = authorityProvider;
        _coordinator = coordinator;
    }

    // Tests and callers that already hold the collaborators.
    internal ReplaceProductLegalEntityScopePolicyHandler(
        IProductLegalEntityScopePolicyRepository policies,
        IProductLegalEntityScopeRolloutStateRepository rollout,
        ILegalEntityRepository legalEntities,
        ProductLegalEntityScopeCandidateFacade candidates,
        IProductLegalEntityScopeWriterAuthorityProvider authorityProvider,
        ProductLegalEntityScopeWriteFenceCoordinator coordinator)
        : this(policies, new Lazy<IProductLegalEntityScopeRolloutStateRepository>(rollout),
            new Lazy<ILegalEntityRepository>(legalEntities), new Lazy<ProductLegalEntityScopeCandidateFacade>(candidates),
            new Lazy<IProductLegalEntityScopeWriterAuthorityProvider>(authorityProvider),
            new Lazy<ProductLegalEntityScopeWriteFenceCoordinator>(coordinator))
    {
    }

    public async Task<Response<ProductLegalEntityScopeModels.PolicyDto>> Handle(
        ReplaceProductLegalEntityScopePolicyCommand request,
        CancellationToken cancellationToken)
    {
        IProductLegalEntityScopeRolloutStateRepository rolloutStates;
        ILegalEntityRepository legalEntityStore;
        ProductLegalEntityScopeCandidateFacade candidateFacade;
        IProductLegalEntityScopeWriterAuthorityProvider authorities;
        ProductLegalEntityScopeWriteFenceCoordinator fence;
        try
        {
            rolloutStates = _rollout.Value;
            legalEntityStore = _legalEntities.Value;
            candidateFacade = _candidates.Value;
            authorities = _authorityProvider.Value;
            fence = _coordinator.Value;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
                                          && Common.StoreConnectionFailure.Is(exception))
        {
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail(
                ProductLegalEntityScopeWriteFenceCoordinator.RolloutStoreUnavailableCode, 503);
        }
        var (unreadable, rollout) = await CreateProductLegalEntityScopePolicyHandler.TryReadRolloutAsync(
            rolloutStates, "replace", cancellationToken);
        if (unreadable is not null) return CreateProductLegalEntityScopePolicyHandler.RolloutUnreadable(unreadable);
        if (rollout?.Mode != ProductLegalEntityScopeRolloutMode.Preparation)
            return CreateProductLegalEntityScopePolicyHandler.Fail("PRODUCT_SCOPE_ROLLOUT_NOT_PREPARATION");
        var policy = await _policies.GetByGlobalProductIdAsync(request.GlobalProductId, cancellationToken);
        if (policy is null)
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail("PRODUCT_SCOPE_POLICY_NOT_FOUND", 404);

        var mutation = ProductLegalEntityScopeMutationIdentity.Create(request);
        var authority = await authorities.ResolveForegroundReplaceAsync(
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
            var candidate = await candidateFacade.ResolveAsync(
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
            if ((await legalEntityStore.GetReferenceableByIdsAsync(
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

        var admission = await fence.EnterForegroundReplaceAsync(
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
            await fence.CompleteForegroundReplaceAsync(admission,
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

        await fence.CompleteForegroundReplaceAsync(admission, result, cancellationToken);
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
