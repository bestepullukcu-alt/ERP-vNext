using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

// MOD-0290-FU04 S1 (§22, RCS-001 §4): an item draft takes its code from the product identity family's ledger — reserve
// (IT, the family's one counter) → consume for the new identity → the row goes in with its code still live (one
// transaction, ItemRepository.InsertBoundToLiveCodeAsync) → confirm. A write the store definitely refused burns the
// code (never handed out again); an unknown outcome keeps it pending (202) and the same key finishes it.
public sealed class CreateItemHandler : IRequestHandler<CreateItemCommand, Response<Guid>>
{
    private readonly IItemRepository _items;
    private readonly ICodeReservationRepository _reservations;
    private readonly IItemReferenceResolver _references;
    private readonly IProductIdentityActorContext _actor;

    public CreateItemHandler(
        IItemRepository items,
        ICodeReservationRepository reservations,
        IItemReferenceResolver references,
        IProductIdentityActorContext actor)
    {
        _items = items;
        _reservations = reservations;
        _references = references;
        _actor = actor;
    }

    public async Task<Response<Guid>> Handle(CreateItemCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var commandId = request.IdempotencyKey!.Trim().ToUpperInvariant();
        var reservationCommandId = ItemRules.ReservationCommandPrefix + commandId;
        if (ItemRules.Read(request) is not { } values)
            return Response<Guid>.Fail([ItemErrors.ReferenceValueInvalid, "material-type"], 422);
        var fingerprint = ItemRules.CreationFingerprint(values);

        // The same key again: the first answer (and a creation it left unfinished is finished), or a conflict.
        if (await _items.GetByCreationCommandIdAsync(commandId, cancellationToken) is { } existing)
        {
            return existing.CreationFingerprint != fingerprint
                ? Response<Guid>.Fail(ItemErrors.IdempotencyKeyConflict, 409)
                : await CompleteAsync(existing, reservationCommandId, commandId, 200, cancellationToken);
        }

        if (ItemRules.ConversionShapeFailure(values.UomConversions) is { } shape)
            return Response<Guid>.Fail([ItemErrors.UomConversionInvalid, shape], 422);
        var references = await ItemReferenceCheck.ResolveAsync(_references, values, null, cancellationToken);
        if (references.Proof is not { } proof)
            return Fail(references.ErrorCode!, references.StatusCode, references.Detail);
        if (ItemRules.StockableIncomplete(values))
            return Response<Guid>.Fail(ItemErrors.StockBehaviourIncomplete, 422);
        var normalizedName = ItemRules.NormalizeName(values.Name);
        var nameKey = ItemRules.NameKey(values.MaterialType, normalizedName);
        if (await _items.NameKeyTakenAsync(nameKey, null, cancellationToken))
            return Response<Guid>.Fail(ItemErrors.NameConflict, 409);

        CodeReservation reservation;
        try
        {
            reservation = await _reservations.ReserveAsync(
                CodeBearingEntityType.Item, reservationCommandId, _actor.ActorId, commandId, cancellationToken);
        }
        catch (InvalidOperationException exception)
            when (exception.Message is "IDEMPOTENCY_KEY_CONFLICT" or "RESERVATION_IDEMPOTENCY_KEY_TOMBSTONED")
        {
            return Response<Guid>.Fail(exception.Message, 409);
        }

        var consume = await _reservations.ConsumeForIdentityAsync(
            reservation.Id, CodeBearingEntityType.Item, Guid.NewGuid(), reservation.Version, reservationCommandId,
            _actor.ActorId, commandId, cancellationToken);
        if (!consume.Succeeded || consume.Reservation?.ConsumedEntityId is not { } identityId)
        {
            // The code stays reserved for this key; its repeat consumes it again.
            return Response<Guid>.Fail(consume.ErrorCode ?? "CODE_RESERVATION_REQUIRED", 409);
        }

        reservation = consume.Reservation;
        if (reservation.BindingState == CodeReservationBindingState.Burned)
            return Response<Guid>.Fail("CODE_RESERVATION_BURNED", 409);

        var now = DateTimeOffset.UtcNow;
        var item = new Item
        {
            Id = identityId,
            CanonicalCode = reservation.ReservedCode,
            CodeReservationId = reservation.Id,
            CreationCommandId = commandId,
            CreationFingerprint = fingerprint,
            Name = values.Name,
            NameNormalized = normalizedName,
            NameKey = nameKey,
            Description = values.Description,
            MaterialType = values.MaterialType,
            MaterialTypeSelection = proof.MaterialType,
            BaseUomCode = values.BaseUomCode,
            BaseUomSelection = proof.BaseUom,
            UomConversions = values.UomConversions.ToList(),
            StorageConditionCodes = values.StorageConditionCodes.ToList(),
            StorageConditionSelections = proof.StorageConditions.ToList(),
            LotControlled = values.LotControlled,
            Stockable = values.Stockable,
            ShelfLifeDays = values.ShelfLifeDays,
            RetestDays = values.RetestDays,
            MinRemainingShelfLifeDays = values.MinRemainingShelfLifeDays,
            LifecycleStatus = ItemLifecycleStatus.Draft,
            CreatedBy = _actor.ActorId,
            UpdatedBy = _actor.ActorId,
            CreatedAt = now,
            UpdatedAt = now
        };

        var created = await _items.CreateDraftAsync(item, cancellationToken);
        if (created.WriteOutcomeAmbiguous)
        {
            var persisted = await _items.GetByReservationIdAsync(reservation.Id, cancellationToken);
            if (persisted is null || persisted.Id != identityId || persisted.CreationCommandId != commandId
                || persisted.CanonicalCode != reservation.ReservedCode)
            {
                // Unknown: the row may yet appear. The code stays pending; the same key finishes it.
                return Response<Guid>.Fail(ItemErrors.BindingReconciliationRequired, 202);
            }
            created = new ItemWriteResult(true, persisted);
        }

        if (!created.Succeeded || created.Item is null)
        {
            var refusal = created.ErrorCode ?? "ITEM_WRITE_FAILED";
            var burnRefusal = await AbandonAsync(reservation, identityId, reservationCommandId, commandId, refusal,
                cancellationToken);
            if (burnRefusal is not null)
                return Response<Guid>.Fail([burnRefusal, refusal],
                    burnRefusal == CodeReservationBurnCodes.TransactionUnavailable ? 503 : 409);
            return Response<Guid>.Fail(refusal, 409);
        }

        return await CompleteAsync(created.Item, reservationCommandId, commandId, 201, cancellationToken);
    }

    // The row exists: its code is confirmed (also on a replay that finds it still pending).
    private async Task<Response<Guid>> CompleteAsync(Item item, string reservationCommandId, string commandId,
        int successStatus, CancellationToken cancellationToken)
    {
        var reservation = await _reservations.GetByIdAsync(item.CodeReservationId, cancellationToken);
        if (reservation is null || reservation.ConsumedEntityId != item.Id || reservation.ReservedCode != item.CanonicalCode)
            return Response<Guid>.Fail("ITEM_BINDING_INVARIANT_VIOLATION", 500);
        if (reservation.BindingState == CodeReservationBindingState.Confirmed)
            return Response<Guid>.Success(item.Id, successStatus);
        if (reservation.BindingState != CodeReservationBindingState.PendingIdentityWrite)
            return Response<Guid>.Fail("ITEM_BINDING_INVARIANT_VIOLATION", 500);

        var confirmation = await _reservations.ConfirmIdentityBindingAsync(
            reservation.Id, item.Id, reservation.Version, reservationCommandId + ":confirm", _actor.ActorId, commandId,
            cancellationToken);
        if (confirmation.Succeeded) return Response<Guid>.Success(item.Id, successStatus);

        var actual = await _reservations.GetByIdAsync(reservation.Id, cancellationToken);
        return actual?.BindingState switch
        {
            CodeReservationBindingState.Confirmed => Response<Guid>.Success(item.Id, successStatus),
            // The row is written, its code still pending: accepted, the same key confirms it.
            CodeReservationBindingState.PendingIdentityWrite => Response<Guid>.Success(item.Id, 202),
            _ => Response<Guid>.Fail("ITEM_BINDING_INVARIANT_VIOLATION", 500)
        };
    }

    // No row of this command: its consumed code is burned (the gate reads "no live item bound" in the burn's own
    // transaction). Null when burned (or already burned); otherwise the named refusal.
    private async Task<string?> AbandonAsync(CodeReservation reservation, Guid identityId, string reservationCommandId,
        string commandId, string reason, CancellationToken cancellationToken)
    {
        if (await _items.GetByReservationIdAsync(reservation.Id, cancellationToken) is not null) return null;
        if ((await _reservations.GetByIdAsync(reservation.Id, cancellationToken))?.BindingState
            == CodeReservationBindingState.Burned) return null;
        var burned = await _reservations.BurnIdentityBindingAsync(
            reservation.Id, identityId, reservation.Version, reservationCommandId + ":burn", _actor.ActorId, commandId,
            reason, cancellationToken);
        if (burned.Succeeded) return null;
        return burned.ErrorCode == CodeReservationBurnCodes.TransactionUnavailable
            ? CodeReservationBurnCodes.TransactionUnavailable
            : ItemErrors.CodeBurnRefused;
    }

    private static Response<Guid> Fail(string code, int status, string? detail) =>
        detail is null ? Response<Guid>.Fail(code, status) : Response<Guid>.Fail([code, detail], status);
}
