using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;

public sealed class CreateFirstGskuDraftFacadeHandler
    : IRequestHandler<CreateFirstGskuDraftFacadeCommand, Response<ProductItemSkuMasterModels.GskuDraftResponse>>
{
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ICodeReservationRepository _reservations;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGskuRepository _gskus;
    private readonly IVerifiedGskuReferenceResolver _resolver;
    private readonly IProductIdentityActorContext _actorContext;
    private readonly IMediator _mediator;

    public CreateFirstGskuDraftFacadeHandler(
        IGlobalProductRepository globalProducts,
        ICodeReservationRepository reservations,
        IProductDefinitionRevisionRepository revisions,
        IGskuRepository gskus,
        IVerifiedGskuReferenceResolver resolver,
        IProductIdentityActorContext actorContext,
        IMediator mediator)
    {
        _globalProducts = globalProducts;
        _reservations = reservations;
        _revisions = revisions;
        _gskus = gskus;
        _resolver = resolver;
        _actorContext = actorContext;
        _mediator = mediator;
    }

    public async Task<Response<ProductItemSkuMasterModels.GskuDraftResponse>> Handle(
        CreateFirstGskuDraftFacadeCommand request,
        CancellationToken cancellationToken)
    {
        var input = request.Request;
        var operationId = request.OperationId.Trim().ToUpperInvariant();
        var commandId = $"GSKU:{operationId}";
        var parent = await _globalProducts.GetByIdAsync(input.GlobalProductId, cancellationToken);
        if (parent is null)
        {
            return Fail("PARENT_NOT_FOUND", 404);
        }

        if (parent.LifecycleStatus is not ProductIdentityLifecycleStatus.Draft
            and not ProductIdentityLifecycleStatus.IdentityApproved)
        {
            return Fail("PARENT_NOT_REFERENCEABLE", 409);
        }

        var enumeration = await _resolver.EnumerateUomsAsync(cancellationToken);
        if (!enumeration.IsSuccessful)
        {
            return Fail(
                enumeration.FailureCode ?? "REFERENCE_PROVIDER_UNAVAILABLE",
                GskuCreateOptionsFacade.NormalizeProviderStatus(enumeration.StatusCode));
        }

        var uom = enumeration.Uoms.SingleOrDefault(x =>
            string.Equals(x.Code, input.PackUomCode, StringComparison.Ordinal));
        if (uom is null)
        {
            return Fail("PACK_UOM_INVALID", 400);
        }

        if (DecimalScale(input.PackQuantity) > uom.MaximumDecimalPrecision)
        {
            return Fail("PACK_QUANTITY_PRECISION_EXCEEDED", 400);
        }

        CodeReservation reservation;
        try
        {
            reservation = await _reservations.ReserveAsync(
                CodeBearingEntityType.Gsku,
                commandId + ":RESERVE",
                _actorContext.ActorId,
                commandId,
                cancellationToken);
        }
        catch (InvalidOperationException exception)
            when (exception.Message is "IDEMPOTENCY_KEY_CONFLICT" or "RESERVATION_IDEMPOTENCY_KEY_TOMBSTONED")
        {
            return Fail(exception.Message, 409);
        }

        var internalRequest = new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
        {
            GlobalProductId = input.GlobalProductId,
            GskuReservationId = reservation.Id,
            ExpectedReservationVersion = reservation.Version,
            CreationCommandId = commandId,
            PackQuantity = input.PackQuantity,
            PackUomCode = input.PackUomCode
        };

        var result = await _mediator.Send(new CreateFirstGskuDraftCommand(internalRequest), cancellationToken);
        if (result.StatusCode == 202)
        {
            var current = await _reservations.GetByIdAsync(reservation.Id, cancellationToken);
            internalRequest = new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
            {
                GlobalProductId = input.GlobalProductId,
                GskuReservationId = reservation.Id,
                ExpectedReservationVersion = current?.Version ?? reservation.Version,
                CreationCommandId = commandId,
                PackQuantity = input.PackQuantity,
                PackUomCode = input.PackUomCode
            };
            result = await _mediator.Send(new CreateFirstGskuDraftCommand(internalRequest), cancellationToken);
        }

        if (result.IsSuccessful && result.Data is not null)
        {
            return Response<ProductItemSkuMasterModels.GskuDraftResponse>.Success(
                Map(result.Data, input.GlobalProductId),
                201);
        }

        if (result.StatusCode == 202)
        {
            var reconciled = await TryConfirmBindingAsync(
                input,
                commandId,
                reservation.Id,
                cancellationToken);
            if (reconciled is not null)
            {
                return reconciled;
            }
        }

        var status = result.StatusCode is 401 or 403 ? 503 : result.StatusCode;
        return result.Errors.Count == 0
            ? Fail("FIRST_GSKU_DRAFT_RECONCILIATION_REQUIRED", status)
            : Response<ProductItemSkuMasterModels.GskuDraftResponse>.Fail(result.Errors, status);
    }

    private async Task<Response<ProductItemSkuMasterModels.GskuDraftResponse>?> TryConfirmBindingAsync(
        ProductItemSkuMasterModels.CreateFirstGskuDraftFacadeRequest input,
        string commandId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var revision = await _revisions.GetByCreationCommandIdAsync(commandId, cancellationToken);
        var gsku = await _gskus.GetByCreationCommandIdAsync(commandId, cancellationToken);
        var reservation = await _reservations.GetByIdAsync(reservationId, cancellationToken);
        if (revision is null || gsku is null || reservation is null)
        {
            return null;
        }

        if (revision.GlobalProductId != input.GlobalProductId
            || gsku.ProductDefinitionRevisionId != revision.Id
            || gsku.CodeReservationId != reservation.Id
            || gsku.CanonicalCode != reservation.ReservedCode
            || gsku.PackQuantity != input.PackQuantity
            || gsku.PackUomCode != input.PackUomCode
            || reservation.ConsumedEntityId != gsku.Id)
        {
            return Fail("CREATION_COMMAND_PAIR_CONFLICT", 409);
        }

        if (reservation.BindingState != CodeReservationBindingState.Confirmed)
        {
            var confirmation = await _reservations.ConfirmIdentityBindingAsync(
                reservation.Id,
                gsku.Id,
                reservation.Version,
                commandId + ":GSKU-CONFIRM",
                _actorContext.ActorId,
                commandId,
                cancellationToken);
            reservation = confirmation.Reservation
                          ?? await _reservations.GetByIdAsync(reservation.Id, cancellationToken);
        }

        return reservation?.BindingState == CodeReservationBindingState.Confirmed
            ? Response<ProductItemSkuMasterModels.GskuDraftResponse>.Success(
                Map(revision, gsku),
                201)
            : null;
    }

    private static ProductItemSkuMasterModels.GskuDraftResponse Map(
        ProductItemSkuMasterModels.FirstGskuDraftDto value,
        Guid globalProductId) => new(
        value.GskuId,
        value.CanonicalCode,
        globalProductId,
        value.ProductDefinitionRevisionId,
        value.RevisionIdentifier,
        value.PackQuantity,
        value.PackUomCode,
        ProductIdentityLifecycleStatus.Draft,
        value.Version);

    private static ProductItemSkuMasterModels.GskuDraftResponse Map(
        ProductDefinitionRevision revision,
        Gsku gsku) => new(
        gsku.Id,
        gsku.CanonicalCode,
        revision.GlobalProductId,
        revision.Id,
        revision.RevisionIdentifier,
        gsku.PackQuantity,
        gsku.PackUomCode,
        gsku.LifecycleStatus,
        gsku.Version);

    private static int DecimalScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0x7F;

    private static Response<ProductItemSkuMasterModels.GskuDraftResponse> Fail(string code, int statusCode) =>
        Response<ProductItemSkuMasterModels.GskuDraftResponse>.Fail(code, statusCode);
}
