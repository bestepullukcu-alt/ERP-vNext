using System.Security.Cryptography;
using System.Text;
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

public sealed class UpdateGskuDraftHandler
    : IRequestHandler<UpdateGskuDraftCommand, Response<ProductItemSkuMasterModels.FirstGskuDraftDto>>
{
    private readonly IGskuRepository _gskus;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly ICodeReservationRepository _reservations;
    private readonly IVerifiedGskuReferenceResolver _resolver;
    private readonly IProductIdentityActorContext _actorContext;

    public UpdateGskuDraftHandler(
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        ICodeReservationRepository reservations,
        IVerifiedGskuReferenceResolver resolver,
        IProductIdentityActorContext actorContext)
    {
        _gskus = gskus;
        _revisions = revisions;
        _reservations = reservations;
        _resolver = resolver;
        _actorContext = actorContext;
    }

    public async Task<Response<ProductItemSkuMasterModels.FirstGskuDraftDto>> Handle(
        UpdateGskuDraftCommand request,
        CancellationToken cancellationToken)
    {
        var input = request.Request;
        var current = await _gskus.GetByIdAsync(input.GskuId, cancellationToken);
        if (current is null)
        {
            return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail("GSKU_NOT_FOUND", 404);
        }

        VerifiedGskuReferenceResolveResult resolved;
        try
        {
            resolved = await _resolver.ResolveLatestAsync(
                "SCALAR_QUANTITY_APPLIES",
                input.PackUomCode,
                cancellationToken);
        }
        catch
        {
            return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail(
                "REFERENCE_DATA_CONTRACT_UNAVAILABLE", 503);
        }

        if (!resolved.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail(
                resolved.FailureCode ?? "REFERENCE_DATA_CONTRACT_UNAVAILABLE",
                resolved.StatusCode);
        }

        var applicability = Map(resolved.Selections, "pack-applicability", "SCALAR_QUANTITY_APPLIES");
        var uom = Map(resolved.Selections, "uom", input.PackUomCode);
        if (resolved.Selections.Count != 2 || applicability is null || uom is null)
        {
            return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail(
                "REFERENCE_DATA_CONTRACT_UNAVAILABLE", 503);
        }

        var now = DateTimeOffset.UtcNow;
        var commandId = $"{current.CreationCommandId}:UPDATE:{input.ExpectedVersion + 1}";
        var evidence = $"{current.Id:N}|{input.PackQuantity}|{input.PackUomCode}|{input.ExpectedVersion + 1}";
        current.PackQuantity = input.PackQuantity;
        current.PackUomCode = input.PackUomCode;
        current.PackApplicabilitySelection = applicability;
        current.PackUomSelection = uom;
        current.AuditIntents.Add(new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(),
            TenantId = current.TenantId,
            AggregateType = AuditAggregateType.Gsku,
            AggregateId = current.Id,
            PreVersion = input.ExpectedVersion,
            PostVersion = input.ExpectedVersion + 1,
            Operation = ProductAuditOperation.GskuDraftUpdated,
            ActorId = _actorContext.ActorId,
            CorrelationId = commandId,
            CausationId = current.CreationCommandId,
            CommandId = commandId,
            Sequence = input.ExpectedVersion + 2L,
            TimestampUtc = now,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))),
            SnapshotReference = $"Gsku/{current.Id:N}/{input.ExpectedVersion + 1}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = commandId
        });

        var update = await _gskus.UpdateDraftAsync(current, input.ExpectedVersion, cancellationToken);
        if (!update.Succeeded || update.Gsku is null)
        {
            return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail(
                update.ErrorCode ?? "CONCURRENCY_CONFLICT", 409);
        }

        var revision = await _revisions.GetByIdAsync(update.Gsku.ProductDefinitionRevisionId, cancellationToken);
        var reservation = await _reservations.GetByIdAsync(update.Gsku.CodeReservationId, cancellationToken);
        if (revision is null || reservation is null || reservation.ConsumedEntityId != update.Gsku.Id)
        {
            return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail(
                "CREATION_COMMAND_PAIR_CONFLICT", 409);
        }

        return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Success(
            CreateFirstGskuDraftHandler.BuildDto(revision, update.Gsku, reservation.BindingState, false));
    }

    private static ReferenceCatalogSelection? Map(
        IReadOnlyList<VerifiedGskuReferenceSelection> selections,
        string setCode,
        string valueCode)
    {
        var item = selections.SingleOrDefault(x => x.SetCode == setCode && x.ValueCode == valueCode);
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
}
