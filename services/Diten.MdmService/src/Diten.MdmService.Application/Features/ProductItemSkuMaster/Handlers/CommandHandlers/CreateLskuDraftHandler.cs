using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;

public sealed class CreateLskuDraftHandler
    : IRequestHandler<CreateLskuDraftCommand, Response<ProductItemSkuMasterModels.LskuDraftDto>>
{
    private readonly ICodeReservationRepository _reservations;
    private readonly ILskuRepository _lskus;
    private readonly IGskuRepository _gskus;
    private readonly IVerifiedMarketReferenceResolver _markets;
    private readonly ITenantContext _tenantContext;
    private readonly IProductIdentityActorContext _actorContext;

    public CreateLskuDraftHandler(
        ICodeReservationRepository reservations,
        ILskuRepository lskus,
        IGskuRepository gskus,
        IVerifiedMarketReferenceResolver markets,
        ITenantContext tenantContext,
        IProductIdentityActorContext actorContext)
    {
        _reservations = reservations;
        _lskus = lskus;
        _gskus = gskus;
        _markets = markets;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
    }

    public async Task<Response<ProductItemSkuMasterModels.LskuDraftDto>> Handle(
        CreateLskuDraftCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Request);
        var command = request.Request;
        var commandId = command.IdempotencyKey.Trim().ToUpperInvariant();
        var replay = await _lskus.GetByCreationCommandIdAsync(commandId, cancellationToken);
        if (replay is not null)
        {
            if (replay.IsDeleted)
            {
                return Fail("CREATION_COMMAND_TOMBSTONED", 409);
            }

            if (replay.GskuId != command.GskuId
                || !string.Equals(replay.MarketCode, command.MarketCode, StringComparison.Ordinal))
            {
                return Fail("IDEMPOTENCY_KEY_CONFLICT", 409);
            }

            return await CompleteReplayAsync(replay, commandId, cancellationToken);
        }

        var gsku = await _gskus.GetReferenceableByIdAsync(command.GskuId, cancellationToken);
        if (gsku is null)
        {
            return Fail("GSKU_NOT_REFERENCEABLE", 404);
        }

        var resolution = await _markets.ResolveLatestAsync(command.MarketCode, cancellationToken);
        if (!resolution.IsSuccessful || resolution.Selection is null)
        {
            return Fail(
                resolution.FailureCode ?? "REFERENCE_PROVIDER_UNAVAILABLE",
                resolution.StatusCode is 404 or 503 or 504 ? resolution.StatusCode : 503);
        }

        CodeReservation reservation;
        try
        {
            reservation = await _reservations.ReserveAsync(
                CodeBearingEntityType.Lsku,
                commandId,
                _actorContext.ActorId,
                commandId,
                cancellationToken);
        }
        catch (InvalidOperationException exception)
            when (exception.Message is "IDEMPOTENCY_KEY_CONFLICT" or "RESERVATION_IDEMPOTENCY_KEY_TOMBSTONED")
        {
            return Fail(exception.Message, 409);
        }

        var requestedIdentityId = Guid.NewGuid();
        var consume = await _reservations.ConsumeForIdentityAsync(
            reservation.Id,
            CodeBearingEntityType.Lsku,
            requestedIdentityId,
            reservation.Version,
            commandId,
            _actorContext.ActorId,
            commandId,
            cancellationToken);
        if (!consume.Succeeded || consume.Reservation?.ConsumedEntityId is not { } identityId)
        {
            return Fail(consume.ErrorCode ?? "CODE_RESERVATION_REQUIRED", 409);
        }

        reservation = consume.Reservation;
        if (reservation.BindingState == CodeReservationBindingState.Burned)
        {
            return Fail("CODE_RESERVATION_BURNED", 409);
        }

        var lsku = BuildLsku(gsku.Id, command.MarketCode, resolution.Selection, reservation, identityId, commandId);
        var createResult = await _lskus.CreateDraftAsync(lsku, cancellationToken);
        if (createResult.WriteOutcomeAmbiguous)
        {
            var persisted = await _lskus.GetByReservationIdAsync(reservation.Id, cancellationToken);
            if (persisted is not null && SameFacts(persisted, lsku))
            {
                createResult = new(true, persisted);
            }
            else
            {
                return Fail("LSKU_BINDING_RECONCILIATION_REQUIRED", 202);
            }
        }

        if (createResult.ConflictKind == LskuCreateConflictKind.IdentityKey)
        {
            return Fail("LSKU_BINDING_RECONCILIATION_REQUIRED", 202);
        }

        if (!createResult.Succeeded || createResult.Lsku is null)
        {
            return Fail(createResult.ErrorCode ?? "LSKU_WRITE_FAILED", 409);
        }

        return await ConfirmAndMapAsync(createResult.Lsku, reservation, commandId, cancellationToken);
    }

    private async Task<Response<ProductItemSkuMasterModels.LskuDraftDto>> CompleteReplayAsync(
        Lsku replay,
        string commandId,
        CancellationToken cancellationToken)
    {
        var reservation = await _reservations.GetByIdAsync(replay.CodeReservationId, cancellationToken);
        if (!MatchesBinding(reservation, replay))
        {
            return Fail("LSKU_BINDING_INVARIANT_VIOLATION", 500);
        }

        if (reservation!.BindingState == CodeReservationBindingState.Confirmed)
        {
            return Success(replay, reservation.BindingState, false, 201);
        }

        if (reservation.BindingState != CodeReservationBindingState.PendingIdentityWrite)
        {
            return Fail("LSKU_BINDING_INVARIANT_VIOLATION", 500);
        }

        return await ConfirmAndMapAsync(replay, reservation, commandId, cancellationToken);
    }

    private async Task<Response<ProductItemSkuMasterModels.LskuDraftDto>> ConfirmAndMapAsync(
        Lsku lsku,
        CodeReservation reservation,
        string commandId,
        CancellationToken cancellationToken)
    {
        var confirmation = await _reservations.ConfirmIdentityBindingAsync(
            reservation.Id,
            lsku.Id,
            reservation.Version,
            commandId + ":confirm",
            _actorContext.ActorId,
            commandId,
            cancellationToken);
        if (confirmation.Succeeded && confirmation.Reservation is not null)
        {
            return Success(lsku, CodeReservationBindingState.Confirmed, false, 201);
        }

        var actual = await _reservations.GetByIdAsync(reservation.Id, cancellationToken);
        if (!MatchesBinding(actual, lsku))
        {
            return Fail("LSKU_BINDING_INVARIANT_VIOLATION", 500);
        }

        return actual!.BindingState switch
        {
            CodeReservationBindingState.Confirmed => Success(lsku, actual.BindingState, false, 201),
            CodeReservationBindingState.PendingIdentityWrite => Success(lsku, actual.BindingState, true, 202),
            _ => Fail("LSKU_BINDING_INVARIANT_VIOLATION", 500)
        };
    }

    private Lsku BuildLsku(
        Guid gskuId,
        string marketCode,
        VerifiedMarketReferenceSelection selection,
        CodeReservation reservation,
        Guid identityId,
        string commandId)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var evidence = $"{_tenantContext.TenantId:N}|{identityId:N}|{gskuId:N}|{marketCode}|{reservation.Id:N}|{reservation.ReservedCode}|DRAFT";
        var intent = new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            AggregateType = AuditAggregateType.Lsku,
            AggregateId = identityId,
            PreVersion = -1,
            PostVersion = 0,
            Operation = ProductAuditOperation.LskuDraftCreated,
            ActorId = _actorContext.ActorId,
            CorrelationId = commandId,
            CausationId = commandId,
            CommandId = commandId,
            Sequence = 1,
            TimestampUtc = timestamp,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))),
            SnapshotReference = $"Lsku/{identityId:N}/0",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = commandId + ":lsku-create"
        };

        return new Lsku
        {
            Id = identityId,
            TenantId = _tenantContext.TenantId,
            GskuId = gskuId,
            CanonicalCode = reservation.ReservedCode,
            CodeReservationId = reservation.Id,
            CreationCommandId = commandId,
            MarketCode = marketCode,
            MarketSelection = new ReferenceCatalogSelection
            {
                SetCode = selection.SetCode,
                ValueCode = selection.ValueCode,
                CatalogVersionId = selection.CatalogVersionId,
                CatalogVersionNumber = selection.CatalogVersionNumber,
                ResolutionMode = ReferenceCatalogResolutionMode.Latest,
                ResolvedAtUtc = selection.ResolvedAtUtc
            },
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
            Version = 0,
            AuditIntents = [intent]
        };
    }

    private static bool MatchesBinding(CodeReservation? reservation, Lsku lsku) =>
        reservation?.ConsumedEntityId == lsku.Id
        && reservation.EntityType == CodeBearingEntityType.Lsku
        && reservation.ReservationState == CodeReservationState.Consumed
        && string.Equals(reservation.ReservedCode, lsku.CanonicalCode, StringComparison.Ordinal);

    private static bool SameFacts(Lsku left, Lsku right) =>
        left.Id == right.Id
        && left.GskuId == right.GskuId
        && left.CodeReservationId == right.CodeReservationId
        && string.Equals(left.CanonicalCode, right.CanonicalCode, StringComparison.Ordinal)
        && string.Equals(left.CreationCommandId, right.CreationCommandId, StringComparison.Ordinal)
        && string.Equals(left.MarketCode, right.MarketCode, StringComparison.Ordinal)
        && SameSelection(left.MarketSelection, right.MarketSelection);

    private static bool SameSelection(ReferenceCatalogSelection left, ReferenceCatalogSelection right) =>
        string.Equals(left.SetCode, right.SetCode, StringComparison.Ordinal)
        && string.Equals(left.ValueCode, right.ValueCode, StringComparison.Ordinal)
        && left.CatalogVersionId == right.CatalogVersionId
        && left.CatalogVersionNumber == right.CatalogVersionNumber
        && left.ResolutionMode == right.ResolutionMode
        && left.ResolvedAtUtc == right.ResolvedAtUtc;

    private static Response<ProductItemSkuMasterModels.LskuDraftDto> Success(
        Lsku lsku,
        CodeReservationBindingState bindingState,
        bool reconciliationRequired,
        int statusCode) =>
        Response<ProductItemSkuMasterModels.LskuDraftDto>.Success(
            new ProductItemSkuMasterModels.LskuDraftDto(
                lsku.Id,
                lsku.CanonicalCode,
                lsku.GskuId,
                lsku.MarketCode,
                new ProductItemSkuMasterModels.ReferenceCatalogSelectionDto(
                    lsku.MarketSelection.SetCode,
                    lsku.MarketSelection.ValueCode,
                    lsku.MarketSelection.CatalogVersionId,
                    lsku.MarketSelection.CatalogVersionNumber,
                    lsku.MarketSelection.ResolutionMode,
                    lsku.MarketSelection.ResolvedAtUtc),
                lsku.LifecycleStatus,
                lsku.Version,
                bindingState,
                reconciliationRequired),
            statusCode);

    private static Response<ProductItemSkuMasterModels.LskuDraftDto> Fail(string code, int statusCode) =>
        Response<ProductItemSkuMasterModels.LskuDraftDto>.Fail(code, statusCode);
}
