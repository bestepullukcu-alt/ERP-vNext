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

public sealed class CreateFirstGskuDraftHandler
    : IRequestHandler<CreateFirstGskuDraftCommand, Response<ProductItemSkuMasterModels.FirstGskuDraftDto>>
{
    private const string PackApplicability = "SCALAR_QUANTITY_APPLIES";
    private readonly IGlobalProductRepository _globalProducts;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGskuRepository _gskus;
    private readonly ICodeReservationRepository _reservations;
    private readonly IVerifiedGskuReferenceResolver _resolver;
    private readonly ITenantContext _tenantContext;
    private readonly IProductIdentityActorContext _actorContext;

    public CreateFirstGskuDraftHandler(
        IGlobalProductRepository globalProducts,
        IProductDefinitionRevisionRepository revisions,
        IGskuRepository gskus,
        ICodeReservationRepository reservations,
        IVerifiedGskuReferenceResolver resolver,
        ITenantContext tenantContext,
        IProductIdentityActorContext actorContext)
    {
        _globalProducts = globalProducts;
        _revisions = revisions;
        _gskus = gskus;
        _reservations = reservations;
        _resolver = resolver;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
    }

    public async Task<Response<ProductItemSkuMasterModels.FirstGskuDraftDto>> Handle(
        CreateFirstGskuDraftCommand request,
        CancellationToken cancellationToken)
    {
        var input = request.Request;
        var commandId = NormalizeCommandId(input.CreationCommandId);
        var existingRevision = await _revisions.GetByCreationCommandIdAsync(commandId, cancellationToken);
        var existingGsku = await _gskus.GetByCreationCommandIdAsync(commandId, cancellationToken);
        if (existingRevision is not null && existingGsku is not null)
        {
            return await BuildReplayAsync(existingRevision, existingGsku, input, commandId, cancellationToken);
        }

        if (existingRevision is not null && existingRevision.GlobalProductId != input.GlobalProductId
            || existingGsku is not null && (existingGsku.CodeReservationId != input.GskuReservationId
                                             || existingGsku.CreationCommandId != commandId))
        {
            return Fail("CREATION_COMMAND_PAIR_CONFLICT", 409);
        }

        var parent = await _globalProducts.GetByIdAsync(input.GlobalProductId, cancellationToken);
        if (parent is null)
        {
            return Fail("PARENT_NOT_FOUND", 404);
        }

        if (parent.LifecycleStatus == ProductIdentityLifecycleStatus.Retired)
        {
            return Fail("PARENT_RETIRED_NOT_REFERENCEABLE", 409);
        }

        var selections = await ResolveSelectionsAsync(input.PackUomCode, cancellationToken);
        if (!selections.Succeeded)
        {
            return Fail(selections.ErrorCode!, selections.StatusCode);
        }

        FirstGskuPairAllocationResult allocation;
        try
        {
            allocation = await _revisions.AllocateForFirstGskuAsync(parent.Id, commandId, cancellationToken);
        }
        catch
        {
            return Fail("REVISION_ORDINAL_CONFLICT", 409);
        }

        var consume = await _reservations.ConsumeForIdentityAsync(
            input.GskuReservationId,
            CodeBearingEntityType.Gsku,
            allocation.GskuId,
            input.ExpectedReservationVersion,
            commandId + ":gsku-consume",
            _actorContext.ActorId,
            commandId,
            cancellationToken);
        if (!consume.Succeeded || consume.Reservation?.ConsumedEntityId != allocation.GskuId)
        {
            return Fail(consume.ErrorCode ?? "CODE_RESERVATION_REQUIRED", 409);
        }

        var reservation = consume.Reservation;
        if (reservation.BindingState == CodeReservationBindingState.Burned)
        {
            return Fail("CODE_RESERVATION_BURNED", 409);
        }

        var revision = BuildRevision(allocation, parent.Id, commandId);
        ProductDefinitionRevisionCreateResult revisionResult;
        try
        {
            revisionResult = await _revisions.CreateForFirstGskuAsync(revision, cancellationToken);
        }
        catch
        {
            return Fail("FIRST_GSKU_DRAFT_RECONCILIATION_REQUIRED", 202);
        }
        if (!revisionResult.Succeeded || revisionResult.Revision is null)
        {
            return Fail(revisionResult.ErrorCode ?? "FIRST_GSKU_DRAFT_RECONCILIATION_REQUIRED",
                revisionResult.ErrorCode == "CREATION_COMMAND_PAIR_CONFLICT" ? 409 : 202);
        }

        var gsku = BuildGsku(
            allocation.GskuId,
            revisionResult.Revision.Id,
            reservation,
            commandId,
            input.PackQuantity,
            input.PackUomCode,
            selections.Applicability!,
            selections.Uom!);
        GskuCreateResult gskuResult;
        try
        {
            gskuResult = await _gskus.CreateDraftAsync(gsku, cancellationToken);
        }
        catch
        {
            return Fail("FIRST_GSKU_DRAFT_RECONCILIATION_REQUIRED", 202);
        }

        if (!gskuResult.Succeeded || gskuResult.Gsku is null)
        {
            return Fail(gskuResult.ErrorCode ?? "FIRST_GSKU_DRAFT_RECONCILIATION_REQUIRED",
                gskuResult.ErrorCode == "CREATION_COMMAND_PAIR_CONFLICT" ? 409 : 202);
        }

        var confirmation = await _reservations.ConfirmIdentityBindingAsync(
            reservation.Id,
            allocation.GskuId,
            reservation.Version,
            commandId + ":gsku-confirm",
            _actorContext.ActorId,
            commandId,
            cancellationToken);
        var actualReservation = confirmation.Reservation ?? await _reservations.GetByIdAsync(reservation.Id, cancellationToken);
        if (actualReservation?.ConsumedEntityId != allocation.GskuId)
        {
            return Fail("CREATION_COMMAND_PAIR_CONFLICT", 409);
        }

        if (actualReservation.BindingState != CodeReservationBindingState.Confirmed)
        {
            return Fail("FIRST_GSKU_DRAFT_RECONCILIATION_REQUIRED", 202);
        }

        return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Success(
            BuildDto(revisionResult.Revision, gskuResult.Gsku, actualReservation.BindingState, false),
            201);
    }

    private async Task<Response<ProductItemSkuMasterModels.FirstGskuDraftDto>> BuildReplayAsync(
        ProductDefinitionRevision revision,
        Gsku gsku,
        ProductItemSkuMasterModels.CreateFirstGskuDraftRequest input,
        string commandId,
        CancellationToken cancellationToken)
    {
        var reservation = await _reservations.GetByIdAsync(input.GskuReservationId, cancellationToken);
        if (revision.GlobalProductId != input.GlobalProductId
            || revision.CreationCommandId != commandId
            || gsku.ProductDefinitionRevisionId != revision.Id
            || gsku.CreationCommandId != commandId
            || gsku.CodeReservationId != input.GskuReservationId
            || gsku.PackQuantity != input.PackQuantity
            || gsku.PackUomCode != input.PackUomCode
            || reservation?.ConsumedEntityId != gsku.Id
            || reservation.ReservedCode != gsku.CanonicalCode)
        {
            return Fail("CREATION_COMMAND_PAIR_CONFLICT", 409);
        }

        if (reservation.BindingState != CodeReservationBindingState.Confirmed)
        {
            return Fail("FIRST_GSKU_DRAFT_RECONCILIATION_REQUIRED", 202);
        }

        return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Success(
            BuildDto(revision, gsku, reservation.BindingState, false));
    }

    private async Task<SelectionResolution> ResolveSelectionsAsync(string uomCode, CancellationToken cancellationToken)
    {
        VerifiedGskuReferenceResolveResult result;
        try
        {
            result = await _resolver.ResolveLatestAsync(PackApplicability, uomCode, cancellationToken);
        }
        catch
        {
            return SelectionResolution.Fail(503, "REFERENCE_DATA_CONTRACT_UNAVAILABLE");
        }

        if (!result.IsSuccessful)
        {
            return SelectionResolution.Fail(result.StatusCode, result.FailureCode ?? "REFERENCE_DATA_CONTRACT_UNAVAILABLE");
        }

        var applicability = ToSelection(result.Selections, "pack-applicability", PackApplicability);
        var uom = ToSelection(result.Selections, "uom", uomCode);
        return applicability is null || uom is null || result.Selections.Count != 2
            ? SelectionResolution.Fail(503, "REFERENCE_DATA_CONTRACT_UNAVAILABLE")
            : SelectionResolution.Success(applicability, uom);
    }

    private static ReferenceCatalogSelection? ToSelection(
        IReadOnlyList<VerifiedGskuReferenceSelection> source,
        string setCode,
        string valueCode)
    {
        var item = source.SingleOrDefault(x => x.SetCode == setCode && x.ValueCode == valueCode);
        return item is null || item.CatalogVersionId == Guid.Empty || item.CatalogVersionNumber <= 0
               || item.ResolutionMode != "LATEST" || item.ResolvedAtUtc == default
               || item.IsRetired || !item.SelectableForNew
            ? null
            : new ReferenceCatalogSelection
            {
                SetCode = item.SetCode,
                ValueCode = item.ValueCode,
                CatalogVersionId = item.CatalogVersionId,
                CatalogVersionNumber = item.CatalogVersionNumber,
                ResolutionMode = ReferenceCatalogResolutionMode.Latest,
                ResolvedAtUtc = item.ResolvedAtUtc
            };
    }

    private ProductDefinitionRevision BuildRevision(
        FirstGskuPairAllocationResult allocation,
        Guid globalProductId,
        string commandId)
    {
        var revision = new ProductDefinitionRevision
        {
            Id = allocation.RevisionId,
            TenantId = _tenantContext.TenantId,
            GlobalProductId = globalProductId,
            RevisionIdentifier = allocation.RevisionIdentifier,
            CreationCommandId = commandId,
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
            Version = 0
        };
        revision.AuditIntents.Add(CreateIntent(
            AuditAggregateType.ProductDefinitionRevision,
            revision.Id,
            ProductAuditOperation.ProductDefinitionRevisionDraftCreated,
            commandId,
            $"{globalProductId:N}|{revision.RevisionIdentifier}"));
        return revision;
    }

    private Gsku BuildGsku(
        Guid id,
        Guid revisionId,
        CodeReservation reservation,
        string commandId,
        decimal quantity,
        string uomCode,
        ReferenceCatalogSelection applicability,
        ReferenceCatalogSelection uom)
    {
        var gsku = new Gsku
        {
            Id = id,
            TenantId = _tenantContext.TenantId,
            ProductDefinitionRevisionId = revisionId,
            CanonicalCode = reservation.ReservedCode,
            CodeReservationId = reservation.Id,
            CreationCommandId = commandId,
            PackApplicabilityCode = PackApplicability,
            PackQuantity = quantity,
            PackUomCode = uomCode,
            PackApplicabilitySelection = applicability,
            PackUomSelection = uom,
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
            Version = 0
        };
        gsku.AuditIntents.Add(CreateIntent(
            AuditAggregateType.Gsku,
            gsku.Id,
            ProductAuditOperation.GskuDraftCreated,
            commandId,
            $"{revisionId:N}|{reservation.Id:N}|{reservation.ReservedCode}|{quantity}|{uomCode}"));
        return gsku;
    }

    private LocalAuditIntent CreateIntent(
        AuditAggregateType aggregateType,
        Guid aggregateId,
        ProductAuditOperation operation,
        string commandId,
        string evidence)
        => new()
        {
            IntentId = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            PreVersion = -1,
            PostVersion = 0,
            Operation = operation,
            ActorId = _actorContext.ActorId,
            CorrelationId = commandId,
            CausationId = commandId,
            CommandId = commandId,
            Sequence = 1,
            TimestampUtc = DateTimeOffset.UtcNow,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))),
            SnapshotReference = $"{aggregateType}/{aggregateId:N}/0",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = $"{commandId}:{aggregateType}-create"
        };

    internal static ProductItemSkuMasterModels.FirstGskuDraftDto BuildDto(
        ProductDefinitionRevision revision,
        Gsku gsku,
        CodeReservationBindingState bindingState,
        bool reconciliationRequired)
        => new(
            revision.Id,
            revision.RevisionIdentifier,
            gsku.Id,
            gsku.CanonicalCode,
            gsku.CodeReservationId,
            gsku.CreationCommandId,
            gsku.PackQuantity,
            gsku.PackUomCode,
            ToDto(gsku.PackApplicabilitySelection),
            ToDto(gsku.PackUomSelection),
            gsku.Version,
            bindingState,
            reconciliationRequired);

    private static ProductItemSkuMasterModels.ReferenceCatalogSelectionDto ToDto(ReferenceCatalogSelection value)
        => new(value.SetCode, value.ValueCode, value.CatalogVersionId, value.CatalogVersionNumber,
            value.ResolutionMode, value.ResolvedAtUtc);

    private static string NormalizeCommandId(string value) => value.Trim().ToUpperInvariant();
    private static Response<ProductItemSkuMasterModels.FirstGskuDraftDto> Fail(string code, int status)
        => Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail(code, status);

    private sealed record SelectionResolution(
        bool Succeeded,
        int StatusCode,
        string? ErrorCode,
        ReferenceCatalogSelection? Applicability,
        ReferenceCatalogSelection? Uom)
    {
        public static SelectionResolution Success(ReferenceCatalogSelection applicability, ReferenceCatalogSelection uom)
            => new(true, 200, null, applicability, uom);
        public static SelectionResolution Fail(int statusCode, string errorCode)
            => new(false, statusCode, errorCode, null, null);
    }
}
