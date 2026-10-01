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
            if (replayScopeFailure is not null)
            {
                return replayScopeFailure;
            }

            if (replay.IsDeleted)
            {
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "CREATION_COMMAND_TOMBSTONED",
                    409);
            }

            var replayGsku = await _gskus.GetByIdAsync(replay.GskuId, cancellationToken);
            if (replayGsku is null)
            {
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "FINISHED_GOOD_BINDING_INVARIANT_VIOLATION",
                    500);
            }

            var replayReservation = await _reservations.GetByIdAsync(replay.CodeReservationId, cancellationToken);
            if (replayReservation?.ConsumedEntityId != replay.Id
                || replayReservation.ReservedCode != replay.CanonicalCode)
            {
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "FINISHED_GOOD_BINDING_INVARIANT_VIOLATION",
                    500);
            }

            if (replayReservation.BindingState == CodeReservationBindingState.Confirmed)
            {
                return await CompleteAdmissionAndMapAsync(
                    replay, replayGsku.CanonicalCode, commandId, admissionFingerprint, cancellationToken);
            }
            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Success(
                BuildDto(replay, replayGsku.CanonicalCode, replayReservation.BindingState, true), 202);
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
            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                consume.ErrorCode ?? "CODE_RESERVATION_REQUIRED",
                409);
        }

        reservation = consume.Reservation;
        if (reservation.BindingState == CodeReservationBindingState.Burned)
        {
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
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED",
                    202);
            }
        }

        if (!createResult.Succeeded || createResult.FinishedGood is null)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                createResult.ErrorCode ?? "FINISHED_GOOD_WRITE_FAILED",
                409);
        }

        var confirmation = await _reservations.ConfirmIdentityBindingAsync(
            reservation.Id,
            identityId,
            reservation.Version,
            commandId + ":confirm",
            _actorContext.ActorId,
            commandId,
            cancellationToken);
        if (!confirmation.Succeeded)
        {
            var actual = await _reservations.GetByIdAsync(reservation.Id, cancellationToken);
            if (actual?.ConsumedEntityId != identityId || actual.ReservedCode != createResult.FinishedGood.CanonicalCode)
            {
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "FINISHED_GOOD_BINDING_INVARIANT_VIOLATION",
                    500);
            }

            if (actual.BindingState == CodeReservationBindingState.Confirmed)
            {
                scopeFailure = await EvaluateGskuScopeAsync(gsku.Id, cancellationToken);
                if (scopeFailure is not null)
                {
                    return scopeFailure;
                }
                return await CompleteAdmissionAndMapAsync(
                    createResult.FinishedGood, gsku.CanonicalCode, commandId,
                    admissionFingerprint, cancellationToken);
            }

            if (actual.BindingState == CodeReservationBindingState.PendingIdentityWrite)
            {
                scopeFailure = await EvaluateGskuScopeAsync(gsku.Id, cancellationToken);
                if (scopeFailure is not null)
                {
                    return scopeFailure;
                }
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Success(
                    BuildDto(createResult.FinishedGood, gsku.CanonicalCode, actual.BindingState, true),
                    202);
            }

            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                "FINISHED_GOOD_BINDING_INVARIANT_VIOLATION",
                500);
        }

        scopeFailure = await EvaluateGskuScopeAsync(gsku.Id, cancellationToken);
        if (scopeFailure is not null)
        {
            return scopeFailure;
        }

        return await CompleteAdmissionAndMapAsync(
            createResult.FinishedGood, gsku.CanonicalCode, commandId,
            admissionFingerprint, cancellationToken);
    }

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
            : Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                completion.ErrorCode ?? "FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED",
                completion.ErrorCode == "GSKU_CHILD_ADMISSION_CONFLICT" ? 409 : 202);
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
