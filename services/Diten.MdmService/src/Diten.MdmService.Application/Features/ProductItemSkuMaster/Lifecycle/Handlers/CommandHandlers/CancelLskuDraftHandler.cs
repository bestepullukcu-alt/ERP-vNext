using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
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
    ICodeReservationRepository reservations,
    LskuIdentityWorkflowProcessor identityProcessor,
    LskuIdentityWorkflowExecutionConfiguration identityExecution,
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
        // FIX2 item 6: a deleted (cancelled) LSKU is read too — the repeat of a cancel is a replay, not a 404.
        var lsku = await lskus.GetByIdOrTombstoneAsync(command.LskuId, cancellationToken);
        var gsku = lsku is null ? null : await gskus.GetByIdAsync(lsku.GskuId, cancellationToken);
        var revision = gsku is null ? null
            : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null ? null
            : await products.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (lsku is null || lsku.TenantId != tenantContext.TenantId || gsku is null || revision is null || product is null)
            return Fail("LSKU_NOT_FOUND", 404);
        var decision = await scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed) return Fail("LSKU_NOT_FOUND", 404);

        // FIX3 item 3: closing a submit moves the draft's version (its own audited, atomic end); the cancel then goes on
        // at that version. A repeat of a cancel whose closure already committed finds the closure at the version it
        // was asked for and goes on from there.
        var expectedVersion = command.ExpectedVersion;
        if (lsku.Version == expectedVersion + 1
            && await identityOperations.GetClosedByDraftCancelAsync(lsku.Id, expectedVersion, cancellationToken) is not null)
            expectedVersion++;

        if (!lsku.IsDeleted)
        {
            if (lsku.LifecycleStatus != ProductIdentityLifecycleStatus.Draft)
                return Fail("LSKU_NOT_A_DRAFT", 409);
            if (lsku.Version != expectedVersion)
                return Fail("LSKU_CONCURRENCY_CONFLICT", 409);
            // FIX2 item 10: a submit waiting for its maker's replay whose start is known never to have reached the
            // engine is closed by this cancel (a named end); any other unfinished submit refuses it — a stuck one is
            // counted by the identity gauge (LSKU_IDENTITY).
            var unfinished = await identityOperations.GetUnfinishedForLskuAsync(lsku.Id, cancellationToken);
            foreach (var operation in unfinished)
            {
                var closed = await identityProcessor.CloseForDraftCancelAsync(operation, actor,
                    $"draft-cancel-{actor:N}", identityExecution.LeaseDuration, cancellationToken);
                if (!closed.Closed) return Fail(closed.Code ?? "LSKU_SUBMIT_IN_FLIGHT", closed.Status);
                expectedVersion++;
            }
            // FIX3 item 3: a fence still held by a submit that has ENDED (written before the closure was atomic, or
            // left by any other path) holds nothing at the engine — it is given back here instead of refusing for good.
            if (lsku.ActiveLifecycleOperation is { Kind: LskuLifecycleOperationKind.IdentitySubmit } fence
                && unfinished.All(x => x.OperationId != fence.OperationId))
            {
                var holder = await identityOperations.GetByOperationIdAsync(fence.OperationId, cancellationToken);
                if (holder is null || holder.Checkpoint is LskuIdentityWorkflowCheckpoint.Completed
                        or LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
                        or LskuIdentityWorkflowCheckpoint.DecisionNotApplicable
                        or LskuIdentityWorkflowCheckpoint.Superseded)
                    await lskus.ReleaseIdentitySubmitFenceAsync(lsku.Id, fence.OperationId, cancellationToken);
            }
            if (unfinished.Count > 0 || lsku.ActiveLifecycleOperation is not null)
                lsku = await lskus.GetByIdOrTombstoneAsync(lsku.Id, cancellationToken) ?? lsku;
        }

        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateDraftCancelled(lsku, expectedVersion,
            command.OperationId, actor, reason, clock.GetUtcNow().ToUniversalTime());
        var cancelled = await lskus.CancelDraftAsync(lsku.Id, expectedVersion, audit, cancellationToken);
        if (!cancelled.Succeeded || cancelled.Lsku is null)
            return Fail(cancelled.ErrorCode ?? "LSKU_DRAFT_CANCEL_FAILED",
                cancelled.ErrorCode switch
                {
                    "LSKU_NOT_FOUND" => 404,
                    "AUDIT_INTENT_CAPACITY_REACHED" => 503,
                    "LSKU_DRAFT_CANCEL_CONTRACT_INVALID" => 400,
                    _ => 409
                });

        // FIX2 item 4: the code bound to the cancelled row is finished (burned: never handed out again), so nothing of
        // this creation can still become the child, and the admission it may still hold goes. A first try that threw
        // after the cancel committed is completed by the replay (the same steps, each replay-safe).
        var reservation = await reservations.GetByIdAsync(lsku.CodeReservationId, cancellationToken);
        if (reservation is { ReservationState: CodeReservationState.Consumed,
                             BindingState: CodeReservationBindingState.PendingIdentityWrite }
            && reservation.ConsumedEntityId == lsku.Id)
        {
            var burned = await reservations.BurnIdentityBindingAsync(reservation.Id, lsku.Id, reservation.Version,
                $"lsku-draft-cancel:{command.OperationId:D}:burn", actor.ToString("D"), command.OperationId.ToString("D"),
                "LSKU_DRAFT_CANCELLED", cancellationToken);
            if (!burned.Succeeded)
                return burned.ErrorCode == CodeReservationBurnCodes.TransactionUnavailable
                    ? Fail(CodeReservationBurnCodes.TransactionUnavailable, 503)
                    : Fail(CreateLskuDraftHandler.BurnRefused, 409);
        }
        var fingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
            lsku.GskuId, GskuChildIdentityKind.Lsku, lsku.CreationCommandId, lsku.MarketCode);
        var held = (await gskus.GetByIdAsync(lsku.GskuId, cancellationToken))?.ChildCreationAdmissions.Any(x =>
            x.ChildKind == GskuChildIdentityKind.Lsku && x.CreationCommandId == lsku.CreationCommandId) == true;
        var released = !held || (await gskus.ReleaseChildCreationAdmissionAsync(lsku.GskuId, GskuChildIdentityKind.Lsku,
            lsku.CreationCommandId, fingerprint, cancellationToken)).Succeeded;
        return Response<LskuDraftCancellationResult>.Success(
            new(cancelled.Lsku.Id, cancelled.Lsku.Version, released, cancelled.IsReplay), 200);
    }

    private static Response<LskuDraftCancellationResult> Fail(string code, int status) =>
        Response<LskuDraftCancellationResult>.Fail(code, status);
}
