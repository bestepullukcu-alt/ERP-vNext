using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;

public sealed class CreateFinishedGoodDraftHandler
    : IRequestHandler<CreateFinishedGoodDraftCommand, Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>>
{
    private readonly ICodeReservationRepository _reservations;
    private readonly IFinishedGoodRepository _finishedGoods;
    private readonly IGskuRepository _gskus;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ITenantContext _tenantContext;
    private readonly IProductIdentityActorContext _actorContext;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public CreateFinishedGoodDraftHandler(
        ICodeReservationRepository reservations,
        IFinishedGoodRepository finishedGoods,
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        ITenantContext tenantContext,
        IProductIdentityActorContext actorContext,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates)
    {
        _reservations = reservations;
        _finishedGoods = finishedGoods;
        _gskus = gskus;
        _revisions = revisions;
        _globalProducts = globalProducts;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>> Handle(
        CreateFinishedGoodDraftCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Request);
        var command = request.Request;
        var commandId = command.IdempotencyKey.Trim().ToUpperInvariant();
        var admissionFingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
            command.GskuId, GskuChildIdentityKind.FinishedGood, commandId);
        var scope = await _scopeGuard.ResolveContextAsync("mdm.finished-goods.create", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
        }

        var replay = await _finishedGoods.GetByCreationCommandIdAsync(commandId, cancellationToken);
        if (replay is not null)
        {
            if (replay.GskuId != command.GskuId)
            {
                var driftScopeFailure = await EvaluateGskuScopeAsync(command.GskuId, scope.Context!, cancellationToken);
                if (driftScopeFailure is not null)
                {
                    return driftScopeFailure;
                }

                if (await _gskus.GetReferenceableByIdAsync(command.GskuId, cancellationToken) is null)
                {
                    return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail("GSKU_NOT_REFERENCEABLE", 404);
                }

                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "IDEMPOTENCY_KEY_CONFLICT",
                    409);
            }

            var replayScopeFailure = await EvaluateGskuScopeAsync(
                replay.GskuId,
                scope.Context!,
                cancellationToken);

            var replayGsku = await _gskus.GetByIdAsync(replay.GskuId, cancellationToken);
            if (replayGsku is null)
            {
                return replayScopeFailure ?? Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "FINISHED_GOOD_BINDING_INVARIANT_VIOLATION",
                    500);
            }

            var replayReservation = await _reservations.GetByIdAsync(replay.CodeReservationId, cancellationToken);
            if (replayReservation?.ConsumedEntityId != replay.Id
                || replayReservation.ReservedCode != replay.CanonicalCode)
            {
                return replayScopeFailure ?? Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "FINISHED_GOOD_BINDING_INVARIANT_VIOLATION",
                    500);
            }

            // FG FIX1 items 2 / 3: the row of this command exists — its creation is finished FIRST (a pending code is
            // confirmed, then the admission completed), whatever is answered after: a scope refused now, a
            // cancelled or deleted draft. A replay never leaves the admission behind (LSKU CompleteReplayAsync).
            Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>? finished = null;
            if (!replay.IsDeleted)
            {
                finished = replayReservation.BindingState switch
                {
                    CodeReservationBindingState.Confirmed => await CompleteAdmissionAndMapAsync(
                        replay, replayGsku.CanonicalCode, commandId, admissionFingerprint, cancellationToken),
                    CodeReservationBindingState.PendingIdentityWrite => await ConfirmAndCompleteAsync(
                        replay, replay.GskuId, replayGsku.CanonicalCode, replayReservation, commandId,
                        admissionFingerprint, cancellationToken),
                    _ => null
                };
            }

            if (replayScopeFailure is not null)
            {
                return KeepKey(replayScopeFailure);
            }

            // FG FIX1 item 1 (s21.5): the original create key of a cancelled Draft never comes back as a new Draft —
            // a terminal conflict naming the cancellation (pending its audit or finished alike).
            if (replay.DraftCancellation is not null)
            {
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "FINISHED_GOOD_DRAFT_CANCELLED",
                    409);
            }

            if (replay.IsDeleted)
            {
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "CREATION_COMMAND_TOMBSTONED",
                    409);
            }

            return finished ?? Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                "FINISHED_GOOD_BINDING_INVARIANT_VIOLATION",
                500);
        }

        var scopeFailure = await EvaluateGskuScopeAsync(command.GskuId, scope.Context!, cancellationToken);
        if (scopeFailure is not null)
        {
            return scopeFailure;
        }

        var gsku = await _gskus.GetReferenceableByIdAsync(command.GskuId, cancellationToken);
        if (gsku is null)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail("GSKU_NOT_REFERENCEABLE", 404);
        }

        var creationAttempt = await _finishedGoods.BindCreationAttemptAsync(
            gsku.Id,
            commandId,
            admissionFingerprint,
            cancellationToken);
        if (creationAttempt.Outcome == FinishedGoodCreationAttemptOutcome.Conflict)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                "IDEMPOTENCY_KEY_CONFLICT",
                409);
        }
        // FG FIX1 item 7: an unacceptable key is the caller's (400); a store that did not answer is 503 (the same key
        // may be sent again) — neither is a half-done command that needs reconciliation (202).
        if (creationAttempt.Outcome == FinishedGoodCreationAttemptOutcome.Invalid)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(CreationKeyInvalid, 400);
        }
        if (creationAttempt.Outcome == FinishedGoodCreationAttemptOutcome.Unavailable)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(ProductIdentityStore.Unavailable, 503);
        }

        if (creationAttempt.Outcome != FinishedGoodCreationAttemptOutcome.Bound
            || creationAttempt.Attempt is null
            || creationAttempt.Attempt.TenantId != _tenantContext.TenantId
            || creationAttempt.Attempt.GskuId != gsku.Id
            || !string.Equals(creationAttempt.Attempt.CreationCommandId, commandId, StringComparison.Ordinal)
            || !string.Equals(creationAttempt.Attempt.RequestFingerprint, admissionFingerprint, StringComparison.Ordinal))
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                "FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED",
                202);
        }

        var admission = await _gskus.AcquireChildCreationAdmissionAsync(
            gsku.Id,
            GskuChildIdentityKind.FinishedGood,
            commandId,
            admissionFingerprint,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!admission.Succeeded)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                admission.ErrorCode ?? "GSKU_CHILD_ADMISSION_CONFLICT",
                AdmissionStatus(admission.ErrorCode));
        }

        // FG FIX1 item 2 / part B item 7: from here every exit TRIES to give the admission back (an exit by exception
        // too). The release refuses while something of this command can still become the child — a live row, or a
        // live code (reserved; or consumed and not burned). Such an admission is counted by the GSKU_CHILD_ADMISSION
        // gauge and has a named way out, by age, through the operator repair (GskuRepository.FindOrphanChild…):
        // a code reserved and never consumed (a crash between ReserveAsync and ConsumeForIdentityAsync) is consumed
        // and burned, then the admission released; a code consumed for an identity whose row never came is burned,
        // then released; a code bound to an ended row (a settled cancellation) is burned, then released.
        try
        {
            CodeReservation reservation;
            try
            {
                reservation = await _reservations.ReserveAsync(
                    CodeBearingEntityType.FinishedGood,
                    commandId,
                    _actorContext.ActorId,
                    commandId,
                    cancellationToken);
            }
            catch (InvalidOperationException exception)
                when (exception.Message is "IDEMPOTENCY_KEY_CONFLICT" or "RESERVATION_IDEMPOTENCY_KEY_TOMBSTONED")
            {
                await ReleaseAdmissionAsync(gsku.Id, commandId, admissionFingerprint, cancellationToken);
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(exception.Message, 409);
            }

            var requestedIdentityId = Guid.NewGuid();
            var consume = await _reservations.ConsumeForIdentityAsync(
                reservation.Id,
                CodeBearingEntityType.FinishedGood,
                requestedIdentityId,
                reservation.Version,
                commandId,
                _actorContext.ActorId,
                commandId,
                cancellationToken);
            if (!consume.Succeeded || consume.Reservation?.ConsumedEntityId is not { } identityId)
            {
                // The code stays reserved for this command (its repeat consumes it again): the release refuses while
                // that reservation is live, so the admission stays with it.
                await ReleaseAdmissionAsync(gsku.Id, commandId, admissionFingerprint, cancellationToken);
                // FG FIX2 item 4: a refusal the same key resumes (the code stays reserved for this command, its repeat
                // consumes it again) is said so — the screen keeps the key; a new key would leave this code and its
                // admission ownerless until the age-based repair.
                var consumeRefusal = consume.ErrorCode ?? "CODE_RESERVATION_REQUIRED";
                return TransientConsumeRefusals.Contains(consumeRefusal)
                    ? Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail([consumeRefusal, RetrySameKey], 409)
                    : Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(consumeRefusal, 409);
            }

            reservation = consume.Reservation;
            if (reservation.BindingState == CodeReservationBindingState.Burned)
            {
                await ReleaseAdmissionAsync(gsku.Id, commandId, admissionFingerprint, cancellationToken);
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail("CODE_RESERVATION_BURNED", 409);
            }

            var finishedGood = BuildFinishedGood(gsku.Id, reservation, identityId, commandId);
            var createResult = await _finishedGoods.CreateDraftWithAdmissionAsync(
                finishedGood, admissionFingerprint, cancellationToken);
            if (createResult.WriteOutcomeAmbiguous)
            {
                var persisted = await _finishedGoods.GetByReservationIdAsync(reservation.Id, cancellationToken);
                if (persisted is not null
                    && persisted.Id == identityId
                    && persisted.GskuId == gsku.Id
                    && persisted.CodeReservationId == reservation.Id
                    && persisted.CanonicalCode == reservation.ReservedCode
                    && persisted.CreationCommandId == commandId)
                {
                    createResult = new(true, persisted);
                }
                else
                {
                    // Unknown: the row may yet appear — the code stays pending, the admission with it (the replay
                    // or the repair finishes it).
                    return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                        "FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED",
                        202);
                }
            }

            if (!createResult.Succeeded || createResult.FinishedGood is null)
            {
                // A write the store definitely refused leaves no row of ours: the consumed code is burned (never
                // handed out again) and the admission goes back; a refused burn is named and keeps the admission.
                var code = createResult.ErrorCode ?? "FINISHED_GOOD_WRITE_FAILED";
                var burnRefusal = await AbandonCreationAsync(gsku.Id, reservation, identityId, commandId,
                    admissionFingerprint, code, cancellationToken);
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(burnRefusal ?? code,
                    burnRefusal == CodeReservationBurnCodes.TransactionUnavailable ? 503 : 409);
            }

            return await ConfirmAndCompleteAsync(createResult.FinishedGood, gsku.Id, gsku.CanonicalCode, reservation,
                commandId, admissionFingerprint, cancellationToken);
        }
        catch (Exception)
        {
            using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await ReleaseAdmissionAsync(gsku.Id, commandId, admissionFingerprint, bound.Token); }
            catch (Exception) { /* the operator repair sweeps what could not be given back here */ }
            throw;
        }
    }

    // FG FIX1 items 2 / 3 (the LSKU ConfirmAndMapAsync / CompleteReplayAsync shape): the code is confirmed for the row,
    // then the admission is completed — also on a replay that finds the code still pending, and also when the scope is
    // refused at the end (the row exists either way: the admission must not outlive it).
    private async Task<Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>> ConfirmAndCompleteAsync(
        FinishedGood finishedGood,
        Guid gskuId,
        string gskuCanonicalCode,
        CodeReservation reservation,
        string commandId,
        string admissionFingerprint,
        CancellationToken cancellationToken)
    {
        var confirmation = await _reservations.ConfirmIdentityBindingAsync(
            reservation.Id,
            finishedGood.Id,
            reservation.Version,
            commandId + ":confirm",
            _actorContext.ActorId,
            commandId,
            cancellationToken);
        if (!confirmation.Succeeded)
        {
            var actual = await _reservations.GetByIdAsync(reservation.Id, cancellationToken);
            if (actual?.ConsumedEntityId != finishedGood.Id || actual.ReservedCode != finishedGood.CanonicalCode)
            {
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "FINISHED_GOOD_BINDING_INVARIANT_VIOLATION",
                    500);
            }

            if (actual.BindingState == CodeReservationBindingState.PendingIdentityWrite)
            {
                return KeepKey(await EvaluateGskuScopeAsync(gskuId, cancellationToken))
                    ?? Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Success(
                        BuildDto(finishedGood, gskuCanonicalCode, actual.BindingState, true),
                        202);
            }

            if (actual.BindingState != CodeReservationBindingState.Confirmed)
            {
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "FINISHED_GOOD_BINDING_INVARIANT_VIOLATION",
                    500);
            }
        }

        var completed = await CompleteAdmissionAndMapAsync(
            finishedGood, gskuCanonicalCode, commandId, admissionFingerprint, cancellationToken);
        return KeepKey(await EvaluateGskuScopeAsync(gskuId, cancellationToken)) ?? completed;
    }

    public const string BurnRefused = "FINISHED_GOOD_CODE_BURN_REFUSED";

    // FG FIX2 items 4 / K3: added to a refusal the SAME key can still finish — the row of this command is written (a
    // late scope refusal, its creation unfinished), or its code is still reserved for it after a transient refusal.
    // The screen keeps the key; a new key could create a second Finished Good or orphan the code. Never shown alone.
    public const string RetrySameKey = "FINISHED_GOOD_CREATION_RETRY_SAME_KEY";

    // FG FIX2 item 4: consume refusals that pass by themselves (another write moved the code's version; its repeat
    // reads it again). Definite ones (identity / key conflicts, a terminal code, a full audit list) are not here.
    private static readonly HashSet<string> TransientConsumeRefusals = new(StringComparer.Ordinal) { "CONCURRENCY_CONFLICT" };

    private static Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>? KeepKey(
        Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>? refusal) =>
        refusal is null || refusal.Errors.Contains(RetrySameKey)
            ? refusal
            : Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail([.. refusal.Errors, RetrySameKey], refusal.StatusCode);
    // FG FIX1 item 7: the key the store cannot accept (the validator's code — one name for one cause).
    public const string CreationKeyInvalid = "IDEMPOTENCY_KEY_INVALID";

    // Returns null when the code is burned (or kept by a row bound to it) and the admission released; a refused burn
    // is named and the admission stays with the live code.
    private async Task<string?> AbandonCreationAsync(
        Guid gskuId,
        CodeReservation reservation,
        Guid identityId,
        string commandId,
        string admissionFingerprint,
        string reason,
        CancellationToken cancellationToken)
    {
        // FG FIX1 part B (the LSKU shape): a row bound to this reservation keeps its code; a code already burned (an
        // overlapping burn won against the transactional insert) needs no second burn.
        if (await _finishedGoods.GetByReservationIdAsync(reservation.Id, cancellationToken) is null
            && (await _reservations.GetByIdAsync(reservation.Id, cancellationToken))?.BindingState
                != CodeReservationBindingState.Burned)
        {
            var burned = await _reservations.BurnIdentityBindingAsync(
                reservation.Id,
                identityId,
                reservation.Version,
                commandId + ":burn",
                _actorContext.ActorId,
                commandId,
                reason,
                cancellationToken);
            if (!burned.Succeeded)
                return burned.ErrorCode == CodeReservationBurnCodes.TransactionUnavailable
                    ? CodeReservationBurnCodes.TransactionUnavailable : BurnRefused;
        }

        await ReleaseAdmissionAsync(gskuId, commandId, admissionFingerprint, cancellationToken);
        return null;
    }

    private Task ReleaseAdmissionAsync(
        Guid gskuId,
        string commandId,
        string admissionFingerprint,
        CancellationToken cancellationToken) =>
        _gskus.ReleaseChildCreationAdmissionAsync(
            gskuId, GskuChildIdentityKind.FinishedGood, commandId, admissionFingerprint, cancellationToken);

    private async Task<Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>> CompleteAdmissionAndMapAsync(
        FinishedGood finishedGood,
        string gskuCanonicalCode,
        string commandId,
        string admissionFingerprint,
        CancellationToken cancellationToken)
    {
        var completion = await _gskus.CompleteChildCreationAdmissionAsync(
            finishedGood.GskuId,
            GskuChildIdentityKind.FinishedGood,
            commandId,
            admissionFingerprint,
            cancellationToken);
        return completion.Succeeded
            ? Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Success(
                BuildDto(finishedGood, gskuCanonicalCode, CodeReservationBindingState.Confirmed, false), 201)
            // FG FIX1 part B (found by the Web review): the row and its code are written — a refused completion is the
            // same command's to finish (its repeat completes it), never a refusal of the request: 202 reconciliation,
            // so the screen keeps the same key (a 409 told the user to reload, and a reload's NEW key could create a
            // second Finished Good).
            : Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                completion.ErrorCode is { } cause
                    ? ["FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED", cause]
                    : ["FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED"],
                202);
    }

    private static int AdmissionStatus(string? code) => code switch
    {
        "GSKU_NOT_REFERENCEABLE" or "GSKU_NOT_FOUND" => 404,
        "GSKU_CHILD_ADMISSION_CAPACITY_EXCEEDED" => 409,
        _ => 409
    };

    private async Task<Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>?> EvaluateGskuScopeAsync(
        Guid gskuId,
        ProductLegalEntityScopeConsumerContext scopeContext,
        CancellationToken cancellationToken)
    {
        var gsku = await _gskus.GetByIdAsync(gskuId, cancellationToken);
        var revision = gsku is null
            ? null
            : await _revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null
            ? null
            : await _globalProducts.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (gsku is null || revision is null || product is null)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                "GSKU_NOT_REFERENCEABLE",
                404);
        }
        var decision = await _scopeGuard.EvaluateAsync(scopeContext, product.Id, cancellationToken);
        return decision.Allowed
            ? null
            : Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail("GSKU_NOT_REFERENCEABLE", 404);
    }

    private async Task<Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>?> EvaluateGskuScopeAsync(
        Guid gskuId,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.ResolveContextAsync("mdm.finished-goods.create", cancellationToken);
        return scope.IsSuccessful
            ? await EvaluateGskuScopeAsync(gskuId, scope.Context!, cancellationToken)
            : Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
    }

    private FinishedGood BuildFinishedGood(Guid gskuId, CodeReservation reservation, Guid identityId, string commandId)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var evidence = $"{_tenantContext.TenantId:N}|{identityId:N}|{gskuId:N}|{reservation.Id:N}|{reservation.ReservedCode}|DRAFT";
        var intent = new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            AggregateType = AuditAggregateType.FinishedGood,
            AggregateId = identityId,
            PreVersion = -1,
            PostVersion = 0,
            Operation = ProductAuditOperation.FinishedGoodDraftCreated,
            ActorId = _actorContext.ActorId,
            CorrelationId = commandId,
            CausationId = commandId,
            CommandId = commandId,
            Sequence = 1,
            TimestampUtc = timestamp,
            TimestampUtcTicksV1 = timestamp.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))),
            SnapshotReference = $"FinishedGood/{identityId:N}/0",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = commandId + ":finished-good-create"
        };

        return new FinishedGood
        {
            Id = identityId,
            TenantId = _tenantContext.TenantId,
            GskuId = gskuId,
            CanonicalCode = reservation.ReservedCode,
            CodeReservationId = reservation.Id,
            CreationCommandId = commandId,
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
            Version = 0,
            AuditIntents = [intent]
        };
    }

    private static ProductItemSkuMasterModels.FinishedGoodDraftDto BuildDto(
        FinishedGood finishedGood,
        string gskuCanonicalCode,
        CodeReservationBindingState bindingState,
        bool reconciliationRequired)
        => new(
            finishedGood.Id,
            finishedGood.CanonicalCode,
            finishedGood.GskuId,
            gskuCanonicalCode,
            finishedGood.LifecycleStatus,
            finishedGood.Version,
            bindingState,
            reconciliationRequired);
}
