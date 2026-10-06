using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

/*
 * LSKU FIX1 B3: cancelling a draft LSKU. Permissioned (the create key: it undoes a creation), tenant- and
 * scope-bounded like the submit, audited (the intent rides in the cancel write). Refused while a submit of the draft
 * is unfinished — the engine may hold its start (principle A). The cancelled draft is deleted, so the GSKU's
 * retirement blocker no longer counts it, and the creation admission it may still hold is released.
 * The market stays taken by the tombstone (LSKU_IDENTITY_TOMBSTONED, the existing unique index on GSKU + market).
 */
public sealed class CancelLskuDraftHandler(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    ILskuRepository lskus,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IGlobalProductRepository products,
    ILskuIdentityWorkflowOperationRepository identityOperations,
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository scopePolicies,
    ProductLegalEntityScopeCandidateFacade scopeCandidates,
    TimeProvider clock)
    : IRequestHandler<CancelLskuDraftCommand, Response<LskuDraftCancellationResult>>
{
    public async Task<Response<LskuDraftCancellationResult>> Handle(
        CancelLskuDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!actorContext.TryResolveCanonicalHumanSubject(out var actor)
            || !actorContext.HasPermission(LskuIdentityLifecyclePermissions.CancelDraft))
            return Fail("LSKU_DRAFT_CANCEL_FORBIDDEN", 403);
        var reason = command.ReasonCode?.Trim();
        if (command.LskuId == Guid.Empty || command.OperationId == Guid.Empty || command.ExpectedVersion < 0
            || string.IsNullOrEmpty(reason) || reason.Length > 128 || reason.Any(char.IsControl))
            return Fail("LSKU_DRAFT_CANCEL_INVALID", 400);

        var scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates, scopePolicies, scopeCandidates, tenantContext);
        var scope = await scopeGuard.ResolveContextAsync(LskuIdentityLifecyclePermissions.CancelDraft, cancellationToken);
        if (!scope.IsSuccessful) return Fail(scope.FailureCode!, scope.StatusCode);
        var lsku = await lskus.GetByIdAsync(command.LskuId, cancellationToken);
        var gsku = lsku is null ? null : await gskus.GetByIdAsync(lsku.GskuId, cancellationToken);
        var revision = gsku is null ? null
            : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null ? null
            : await products.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (lsku is null || gsku is null || revision is null || product is null)
            return Fail("LSKU_NOT_FOUND", 404);
        var decision = await scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed) return Fail("LSKU_NOT_FOUND", 404);

        if (!lsku.IsDeleted)
        {
            if (lsku.LifecycleStatus != ProductIdentityLifecycleStatus.Draft)
                return Fail("LSKU_NOT_A_DRAFT", 409);
            if (await identityOperations.HasUnfinishedForLskuAsync(lsku.Id, cancellationToken))
                return Fail("LSKU_SUBMIT_IN_FLIGHT", 409);
        }

        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateDraftCancelled(lsku, command.ExpectedVersion,
            command.OperationId, actor, reason, clock.GetUtcNow().ToUniversalTime());
        var cancelled = await lskus.CancelDraftAsync(lsku.Id, command.ExpectedVersion, audit, cancellationToken);
        if (!cancelled.Succeeded || cancelled.Lsku is null)
            return Fail(cancelled.ErrorCode ?? "LSKU_DRAFT_CANCEL_FAILED",
                cancelled.ErrorCode switch
                {
                    "LSKU_NOT_FOUND" => 404,
                    "AUDIT_INTENT_CAPACITY_REACHED" => 503,
                    "LSKU_DRAFT_CANCEL_CONTRACT_INVALID" => 400,
                    _ => 409
                });

        // The admission a creation that never confirmed may still hold goes with the draft (a live row no longer
        // carries its command). An admission already completed is simply not there.
        var fingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
            lsku.GskuId, GskuChildIdentityKind.Lsku, lsku.CreationCommandId, lsku.MarketCode);
        var released = gsku.ChildCreationAdmissions.Any(x =>
                           x.ChildKind == GskuChildIdentityKind.Lsku && x.CreationCommandId == lsku.CreationCommandId)
                       && (await gskus.ReleaseChildCreationAdmissionAsync(lsku.GskuId, GskuChildIdentityKind.Lsku,
                           lsku.CreationCommandId, fingerprint, cancellationToken)).Succeeded;
        return Response<LskuDraftCancellationResult>.Success(
            new(cancelled.Lsku.Id, cancelled.Lsku.Version, released, cancelled.IsReplay), 200);
    }

    private static Response<LskuDraftCancellationResult> Fail(string code, int status) =>
        Response<LskuDraftCancellationResult>.Fail(code, status);
}
