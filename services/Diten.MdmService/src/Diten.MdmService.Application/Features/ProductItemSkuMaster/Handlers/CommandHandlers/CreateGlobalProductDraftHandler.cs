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

public sealed class CreateGlobalProductDraftHandler
    : IRequestHandler<CreateGlobalProductDraftCommand, Response<ProductItemSkuMasterModels.GlobalProductDraftDto>>
{
    private readonly ICodeReservationRepository _reservations;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ITenantContext _tenantContext;
    private readonly IProductIdentityActorContext _actorContext;

    public CreateGlobalProductDraftHandler(
        ICodeReservationRepository reservations,
        IGlobalProductRepository globalProducts,
        ITenantContext tenantContext,
        IProductIdentityActorContext actorContext)
    {
        _reservations = reservations;
        _globalProducts = globalProducts;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
    }

    public async Task<Response<ProductItemSkuMasterModels.GlobalProductDraftDto>> Handle(
        CreateGlobalProductDraftCommand request,
        CancellationToken cancellationToken)
    {
        var command = request.Request;
        var visibleName = GlobalProductNameRules.CleanVisible(command.GlobalProductName!);
        var normalizedName = GlobalProductNameRules.NormalizeDuplicateKey(visibleName);
        if (await _globalProducts.NameExistsAsync(normalizedName, cancellationToken))
        {
            var existingForReservation = await _globalProducts.GetByReservationIdAsync(
                command.ReservationId,
                cancellationToken);
            if (existingForReservation is null
                || existingForReservation.GlobalProductNameNormalized != normalizedName)
            {
                return Response<ProductItemSkuMasterModels.GlobalProductDraftDto>.Fail(
                    "GLOBAL_PRODUCT_NAME_DUPLICATE",
                    409);
            }
        }

        var requestedIdentityId = Guid.NewGuid();
        var consume = await _reservations.ConsumeForIdentityAsync(
            command.ReservationId,
            CodeBearingEntityType.GlobalProduct,
            requestedIdentityId,
            command.ExpectedReservationVersion,
            command.IdempotencyKey,
            _actorContext.ActorId,
            command.IdempotencyKey,
            cancellationToken);

        if (!consume.Succeeded || consume.Reservation?.ConsumedEntityId is not { } identityId)
        {
            return Response<ProductItemSkuMasterModels.GlobalProductDraftDto>.Fail(
                consume.ErrorCode ?? "CODE_RESERVATION_REQUIRED",
                409);
        }

        var reservation = consume.Reservation;
        if (reservation.BindingState == CodeReservationBindingState.Burned)
        {
            return Response<ProductItemSkuMasterModels.GlobalProductDraftDto>.Fail("CODE_RESERVATION_BURNED", 409);
        }

        var globalProduct = BuildGlobalProduct(
            reservation,
            identityId,
            command.IdempotencyKey,
            visibleName,
            normalizedName);
        GlobalProductCreateResult createResult;

        try
        {
            createResult = await _globalProducts.CreateDraftAsync(globalProduct, cancellationToken);
        }
        catch (Exception)
        {
            GlobalProduct? persisted;
            try
            {
                persisted = await _globalProducts.GetByReservationIdAsync(reservation.Id, cancellationToken);
            }
            catch
            {
                persisted = null;
            }

            if (persisted is not null
                && persisted.Id == identityId
                && persisted.CanonicalCode == reservation.ReservedCode)
            {
                createResult = new GlobalProductCreateResult(true, persisted);
            }
            else
            {
                return Response<ProductItemSkuMasterModels.GlobalProductDraftDto>.Fail(
                    "GLOBAL_PRODUCT_BINDING_RECONCILIATION_REQUIRED",
                    202);
            }
        }

        if (!createResult.Succeeded || createResult.GlobalProduct is null)
        {
            return Response<ProductItemSkuMasterModels.GlobalProductDraftDto>.Fail(
                createResult.ErrorCode ?? "GLOBAL_PRODUCT_WRITE_FAILED",
                409);
        }

        var confirmation = await _reservations.ConfirmIdentityBindingAsync(
            reservation.Id,
            identityId,
            reservation.Version,
            command.IdempotencyKey + ":confirm",
            _actorContext.ActorId,
            command.IdempotencyKey,
            cancellationToken);

        if (!confirmation.Succeeded)
        {
            var actualReservation = await _reservations.GetByIdAsync(reservation.Id, cancellationToken);
            if (actualReservation?.ConsumedEntityId != identityId)
            {
                return Response<ProductItemSkuMasterModels.GlobalProductDraftDto>.Fail(
                    "GLOBAL_PRODUCT_BINDING_INVARIANT_VIOLATION",
                    500);
            }

            if (actualReservation.BindingState == CodeReservationBindingState.Confirmed)
            {
                return Response<ProductItemSkuMasterModels.GlobalProductDraftDto>.Success(
                    BuildDto(
                        createResult.GlobalProduct,
                        CodeReservationBindingState.Confirmed,
                        bindingReconciliationRequired: false),
                    201);
            }

            if (actualReservation.BindingState == CodeReservationBindingState.PendingIdentityWrite)
            {
                return Response<ProductItemSkuMasterModels.GlobalProductDraftDto>.Success(
                    BuildDto(
                        createResult.GlobalProduct,
                        CodeReservationBindingState.PendingIdentityWrite,
                        bindingReconciliationRequired: true),
                    202);
            }

            return Response<ProductItemSkuMasterModels.GlobalProductDraftDto>.Fail(
                "GLOBAL_PRODUCT_BINDING_INVARIANT_VIOLATION",
                500);
        }

        return Response<ProductItemSkuMasterModels.GlobalProductDraftDto>.Success(
            BuildDto(
                createResult.GlobalProduct,
                CodeReservationBindingState.Confirmed,
                bindingReconciliationRequired: false),
            201);
    }

    private static ProductItemSkuMasterModels.GlobalProductDraftDto BuildDto(
        GlobalProduct created,
        CodeReservationBindingState bindingState,
        bool bindingReconciliationRequired)
        => new(
            created.Id,
            created.CanonicalCode,
            created.GlobalProductName,
            created.CodeReservationId,
            created.LifecycleStatus,
            created.Version,
            bindingState,
            bindingReconciliationRequired);

    private GlobalProduct BuildGlobalProduct(
        CodeReservation reservation,
        Guid identityId,
        string commandId,
        string visibleName,
        string normalizedName)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var evidence = $"{_tenantContext.TenantId:N}|{identityId:N}|{reservation.Id:N}|{reservation.ReservedCode}|DRAFT";
        var intent = new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            AggregateType = AuditAggregateType.GlobalProduct,
            AggregateId = identityId,
            PreVersion = -1,
            PostVersion = 0,
            Operation = ProductAuditOperation.GlobalProductDraftCreated,
            ActorId = _actorContext.ActorId,
            CorrelationId = commandId,
            CausationId = commandId,
            CommandId = commandId,
            Sequence = 1,
            TimestampUtc = timestamp,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))),
            SnapshotReference = $"GlobalProduct/{identityId:N}/0",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = commandId + ":global-product-create"
        };

        return new GlobalProduct
        {
            Id = identityId,
            TenantId = _tenantContext.TenantId,
            CanonicalCode = reservation.ReservedCode,
            GlobalProductName = visibleName,
            GlobalProductNameNormalized = normalizedName,
            CodeReservationId = reservation.Id,
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
            Version = 0,
            AuditIntents = [intent]
        };
    }
}
