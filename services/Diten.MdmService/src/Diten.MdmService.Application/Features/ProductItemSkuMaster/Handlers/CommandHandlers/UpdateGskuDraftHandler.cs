using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
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
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ICodeReservationRepository _reservations;
    private readonly IVerifiedGskuReferenceResolver _resolver;
    private readonly IProductIdentityActorContext _actorContext;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public UpdateGskuDraftHandler(
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        ICodeReservationRepository reservations,
        IVerifiedGskuReferenceResolver resolver,
        IProductIdentityActorContext actorContext,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _gskus = gskus;
        _revisions = revisions;
        _globalProducts = globalProducts;
        _reservations = reservations;
        _resolver = resolver;
        _actorContext = actorContext;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.FirstGskuDraftDto>> Handle(
        UpdateGskuDraftCommand request,
        CancellationToken cancellationToken)
    {
        var input = request.Request;
        var scope = await _scopeGuard.ResolveContextAsync(
            FirstGskuIdentityLifecyclePermissions.Update,
            cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
        }

        var current = await _gskus.GetByIdAsync(input.GskuId, cancellationToken);
        if (current is null)
        {
            return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail("GSKU_NOT_FOUND", 404);
        }

        var operationKey = request.OperationId == Guid.Empty
            ? $"{current.CreationCommandId}:UPDATE:{input.ExpectedVersion + 1}"
            : request.OperationId.ToString("D");
        var requestEvidenceHash = DraftEditEvidenceHash(
            current.TenantId,
            current.Id,
            input.ExpectedVersion,
            input.PackQuantity,
            input.PackUomCode);
        var priorIntent = current.AuditIntents.SingleOrDefault(intent =>
            string.Equals(intent.IdempotencyKey, operationKey, StringComparison.Ordinal));
        if (priorIntent is not null)
        {
            if (request.OperationId == Guid.Empty
                || priorIntent.Operation != ProductAuditOperation.GskuDraftUpdated
                || !string.Equals(priorIntent.EvidenceHash, requestEvidenceHash, StringComparison.Ordinal)
                || current.Version != input.ExpectedVersion + 1
                || current.LifecycleStatus != ProductIdentityLifecycleStatus.Draft
                || current.PackQuantity != input.PackQuantity
                || !string.Equals(current.PackUomCode, input.PackUomCode, StringComparison.Ordinal))
            {
                return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail(
                    request.OperationId == Guid.Empty
                        ? "CONCURRENCY_CONFLICT"
                        : "GSKU_DRAFT_UPDATE_IDEMPOTENCY_CONFLICT",
                    409);
            }

            return await BuildVerifiedResultAsync(current, cancellationToken);
        }

        var revisionBeforeWrite = await _revisions.GetByIdAsync(
            current.ProductDefinitionRevisionId,
            cancellationToken);
        var product = revisionBeforeWrite is null
            ? null
            : await _globalProducts.GetByIdAsync(revisionBeforeWrite.GlobalProductId, cancellationToken);
        if (revisionBeforeWrite is null || product is null)
        {
            return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail("GSKU_NOT_FOUND", 404);
        }

        var decision = await _scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed)
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
        catch (OperationCanceledException)
        {
            throw;
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
        var commandId = operationKey;
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
            TimestampUtcTicksV1 = now.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = requestEvidenceHash,
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

        return await BuildVerifiedResultAsync(update.Gsku, cancellationToken);
    }

    private async Task<Response<ProductItemSkuMasterModels.FirstGskuDraftDto>> BuildVerifiedResultAsync(
        Gsku gsku,
        CancellationToken cancellationToken)
    {
        var revision = await _revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var reservation = await _reservations.GetByIdAsync(gsku.CodeReservationId, cancellationToken);
        if (revision is null || reservation is null || reservation.ConsumedEntityId != gsku.Id)
        {
            return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail(
                "CREATION_COMMAND_PAIR_CONFLICT", 409);
        }

        var scopeFailure = await EvaluateParentScopeAsync(revision.GlobalProductId, cancellationToken);
        if (scopeFailure is not null)
        {
            return scopeFailure;
        }

        return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Success(
            CreateFirstGskuDraftHandler.BuildDto(revision, gsku, reservation.BindingState, false));
    }

    private async Task<Response<ProductItemSkuMasterModels.FirstGskuDraftDto>?> EvaluateParentScopeAsync(
        Guid globalProductId,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.ResolveContextAsync(
            FirstGskuIdentityLifecyclePermissions.Update,
            cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
        }
        var decision = await _scopeGuard.EvaluateAsync(scope.Context!, globalProductId, cancellationToken);
        return decision.Allowed
            ? null
            : Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail("GSKU_NOT_FOUND", 404);
    }

    private static string DraftEditEvidenceHash(
        Guid tenantId,
        Guid gskuId,
        int expectedVersion,
        decimal packQuantity,
        string packUomCode)
    {
        var evidence = string.Join('|',
            tenantId.ToString("D"),
            gskuId.ToString("D"),
            expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            packQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            packUomCode);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
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
