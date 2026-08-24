using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;

public sealed class CreateFinishedGoodDraftHandler
    : IRequestHandler<CreateFinishedGoodDraftCommand, Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>>
{
    private readonly ICodeReservationRepository _reservations;
    private readonly IFinishedGoodRepository _finishedGoods;
    private readonly IGskuRepository _gskus;
    private readonly ITenantContext _tenantContext;
    private readonly IProductIdentityActorContext _actorContext;

    public CreateFinishedGoodDraftHandler(
        ICodeReservationRepository reservations,
        IFinishedGoodRepository finishedGoods,
        IGskuRepository gskus,
        ITenantContext tenantContext,
        IProductIdentityActorContext actorContext)
    {
        _reservations = reservations;
        _finishedGoods = finishedGoods;
        _gskus = gskus;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
    }

    public async Task<Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>> Handle(
        CreateFinishedGoodDraftCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Request);
        var command = request.Request;
        var commandId = command.IdempotencyKey.Trim().ToUpperInvariant();
        var replay = await _finishedGoods.GetByCreationCommandIdAsync(commandId, cancellationToken);
        if (replay is not null)
        {
            if (replay.IsDeleted)
            {
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "CREATION_COMMAND_TOMBSTONED",
                    409);
            }

            if (replay.GskuId != command.GskuId)
            {
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                    "IDEMPOTENCY_KEY_CONFLICT",
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

            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Success(
                BuildDto(
                    replay,
                    replayGsku.CanonicalCode,
                    replayReservation.BindingState,
                    replayReservation.BindingState == CodeReservationBindingState.PendingIdentityWrite),
                replayReservation.BindingState == CodeReservationBindingState.Confirmed ? 201 : 202);
        }

        var gsku = await _gskus.GetReferenceableByIdAsync(command.GskuId, cancellationToken);
        if (gsku is null)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail("GSKU_NOT_REFERENCEABLE", 404);
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
        var createResult = await _finishedGoods.CreateDraftAsync(finishedGood, cancellationToken);
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
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Success(
                    BuildDto(createResult.FinishedGood, gsku.CanonicalCode, actual.BindingState, false),
                    201);
            }

            if (actual.BindingState == CodeReservationBindingState.PendingIdentityWrite)
            {
                return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Success(
                    BuildDto(createResult.FinishedGood, gsku.CanonicalCode, actual.BindingState, true),
                    202);
            }

            return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Fail(
                "FINISHED_GOOD_BINDING_INVARIANT_VIOLATION",
                500);
        }

        return Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>.Success(
            BuildDto(createResult.FinishedGood, gsku.CanonicalCode, CodeReservationBindingState.Confirmed, false),
            201);
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
