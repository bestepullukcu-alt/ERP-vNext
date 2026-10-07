using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

/*
 * FG FIX1 item 1 (BL-559; MOD-0290 s21.2 / s21.5): cancelling a Draft Finished Good. Permissioned, tenant- and
 * scope-bounded like every FG action, with a mandatory reason. The FG is retained (no delete, its code is never handed
 * out again); one CAS writes the immutable decision and its 72 intent. The cancellation is then PendingAudit — it keeps
 * blocking its parent GSKU — until the intent's central receipt is on the FG and validates
 * (FinishedGoodDraftCancellationProof); only then is it Cancelled and the GSKU free of it.
 */
public sealed class CancelFinishedGoodDraftHandler(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    IFinishedGoodRepository finishedGoods,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IGlobalProductRepository products,
    ICodeReservationRepository reservations,
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository scopePolicies,
    ProductLegalEntityScopeCandidateFacade scopeCandidates,
    TimeProvider clock)
    : IRequestHandler<CancelFinishedGoodDraftCommand, Response<FinishedGoodDraftCancellationResult>>
{
    public async Task<Response<FinishedGoodDraftCancellationResult>> Handle(
        CancelFinishedGoodDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!actorContext.TryResolveCanonicalHumanSubject(out var actor)
            || !actorContext.HasPermission(FinishedGoodDraftCancellationPermissions.CancelDraft))
            return Fail("FINISHED_GOOD_DRAFT_CANCEL_FORBIDDEN", 403);
        var reason = command.ReasonCode?.Trim();
        // FIX1 review m2: the version's successor must exist (int.MaxValue is a 400, not an overflow 500).
        if (command.FinishedGoodId == Guid.Empty || command.OperationId == Guid.Empty || command.ExpectedVersion < 0
            || command.ExpectedVersion == int.MaxValue
            || string.IsNullOrEmpty(reason) || reason.Length > 128 || reason.Any(char.IsControl))
            return Fail("FINISHED_GOOD_DRAFT_CANCEL_INVALID", 400);

        var scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates, scopePolicies, scopeCandidates, tenantContext);
        var scope = await scopeGuard.ResolveContextAsync(
            FinishedGoodDraftCancellationPermissions.CancelDraft, cancellationToken);
        if (!scope.IsSuccessful) return Fail(scope.FailureCode!, scope.StatusCode);
        var finishedGood = await finishedGoods.GetByIdAsync(command.FinishedGoodId, cancellationToken);
        var gsku = finishedGood is null ? null : await gskus.GetByIdAsync(finishedGood.GskuId, cancellationToken);
        var revision = gsku is null ? null
            : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null ? null
            : await products.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (finishedGood is null || finishedGood.TenantId != tenantContext.TenantId
            || gsku is null || revision is null || product is null)
            return Fail("FINISHED_GOOD_NOT_FOUND", 404);
        var decision = await scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed) return Fail("FINISHED_GOOD_NOT_FOUND", 404);

        var now = clock.GetUtcNow().ToUniversalTime();
        var intent = FinishedGoodDraftCancellationAuditIntentFactory.Create(
            finishedGood, command.ExpectedVersion, command.OperationId, actor, reason, now);
        var cancellation = new FinishedGoodDraftCancellation
        {
            OperationId = command.OperationId,
            Fingerprint = FinishedGoodDraftCancellationAuditIntentFactory.Fingerprint(
                finishedGood.TenantId, finishedGood.Id, command.ExpectedVersion, reason, actor),
            IntentId = intent.IntentId,
            IdempotencyKey = intent.IdempotencyKey,
            EvidenceHash = intent.EvidenceHash,
            ReasonCode = reason,
            RequesterSubjectId = actor,
            BaseVersion = command.ExpectedVersion,
            RequestedAtUtc = now
        };
        var written = await finishedGoods.CancelDraftAsync(
            finishedGood.Id, command.ExpectedVersion, cancellation, intent, cancellationToken);
        if (!written.Succeeded || written.FinishedGood is not { } result)
            return Fail(written.ErrorCode ?? "FINISHED_GOOD_DRAFT_CANCEL_FAILED", written.ErrorCode switch
            {
                "FINISHED_GOOD_NOT_FOUND" => 404,
                "AUDIT_INTENT_CAPACITY_REACHED" => 503,
                "FINISHED_GOOD_DRAFT_CANCEL_CONTRACT_INVALID" => 400,
                _ => 409
            });
        // s21.5 / FIX1 review R1: the creation of THIS FG is settled — its original command and fingerprint, never the
        // cancel's. A code still pending for the retained row is confirmed to it (the FG keeps its code for good),
        // then the parent admission is completed; a creation cut off before its completion must not keep the GSKU
        // held. Each step is replay-safe: a step refused here is named, and the same cancel again finishes it.
        var code = await reservations.GetByIdAsync(result.CodeReservationId, cancellationToken);
        if (code is { ReservationState: CodeReservationState.Consumed,
                      BindingState: CodeReservationBindingState.PendingIdentityWrite } pending
            && pending.ConsumedEntityId == result.Id)
        {
            var confirmed = await reservations.ConfirmIdentityBindingAsync(pending.Id, result.Id, pending.Version,
                $"finished-good-draft-cancel:{command.OperationId:D}:confirm", actor.ToString("D"),
                command.OperationId.ToString("D"), cancellationToken);
            // FG FIX1 part B K1: a refused confirm may be the creator's own confirm winning the race — read the code
            // again: confirmed to THIS Finished Good is the settled state the cancel wanted; anything else is named.
            if (!confirmed.Succeeded)
            {
                var current = await reservations.GetByIdAsync(pending.Id, cancellationToken);
                if (current is not { BindingState: CodeReservationBindingState.Confirmed }
                    || current.ConsumedEntityId != result.Id)
                    return Fail(CreationNotSettled, 409);
            }
        }
        var completed = await gskus.CompleteChildCreationAdmissionAsync(result.GskuId, GskuChildIdentityKind.FinishedGood,
            result.CreationCommandId, GskuChildCreationAdmission.ComputeRequestFingerprint(
                result.GskuId, GskuChildIdentityKind.FinishedGood, result.CreationCommandId), cancellationToken);
        if (!completed.Succeeded) return Fail(CreationNotSettled, 409);
        return Response<FinishedGoodDraftCancellationResult>.Success(new(result.Id, result.Version,
            FinishedGoodDraftCancellationProof.Disposition(result), written.IsReplay), 200);
    }

    // FIX1 review R1: the cancel committed but its FG's creation (code binding / parent admission) is not settled yet.
    public const string CreationNotSettled = "FINISHED_GOOD_CREATION_NOT_SETTLED";

    private static Response<FinishedGoodDraftCancellationResult> Fail(string code, int status) =>
        Response<FinishedGoodDraftCancellationResult>.Fail(code, status);
}
