using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.ReferenceData;
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
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public CreateFirstGskuDraftHandler(
        IGlobalProductRepository globalProducts,
        IProductDefinitionRevisionRepository revisions,
        IGskuRepository gskus,
        ICodeReservationRepository reservations,
        IVerifiedGskuReferenceResolver resolver,
        ITenantContext tenantContext,
        IProductIdentityActorContext actorContext,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates)
    {
        _globalProducts = globalProducts;
        _revisions = revisions;
        _gskus = gskus;
        _reservations = reservations;
        _resolver = resolver;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.FirstGskuDraftDto>> Handle(
        CreateFirstGskuDraftCommand request,
        CancellationToken cancellationToken)
    {
        var input = request.Request;
        var commandId = NormalizeCommandId(input.CreationCommandId);
        var scope = await _scopeGuard.ResolveContextAsync("mdm.gskus.create", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Fail(scope.FailureCode!, scope.StatusCode);
        }

        var decision = await _scopeGuard.EvaluateAsync(scope.Context!, input.GlobalProductId, cancellationToken);
        if (!decision.Allowed)
        {
            return Fail("PARENT_NOT_FOUND", 404);
        }

        var parent = await _globalProducts.GetByIdAsync(input.GlobalProductId, cancellationToken);
        if (parent is null)
        {
            return Fail("PARENT_NOT_FOUND", 404);
        }

        var existingRevision = await _revisions.GetByCreationCommandIdAsync(commandId, cancellationToken);
        var existingGsku = await _gskus.GetByCreationCommandIdAsync(commandId, cancellationToken);
        var existingScopeFailure = await EvaluateExistingPairScopeAsync(
            existingRevision,
            existingGsku,
            scope.Context!,
            cancellationToken);
        if (existingScopeFailure is not null)
        {
            return existingScopeFailure;
        }

        if (existingRevision is not null && existingRevision.GlobalProductId != input.GlobalProductId
            || existingGsku is not null && (existingGsku.CodeReservationId != input.GskuReservationId
                                             || existingGsku.CreationCommandId != commandId))
        {
            return Fail("CREATION_COMMAND_PAIR_CONFLICT", 409);
        }

        var admissionFingerprint = ComputeAdmissionFingerprint(input, commandId);
        if (existingRevision is not null && existingGsku is not null)
        {
            var replay = await BuildReplayAsync(existingRevision, existingGsku, input, commandId, cancellationToken);
            if (!replay.IsSuccessful)
            {
                return replay;
            }

            var replayCompletion = await _globalProducts.CompleteChildCreationAdmissionAsync(
                parent.Id, commandId, admissionFingerprint, cancellationToken);
            return replayCompletion.Succeeded
                ? replay
                : Fail(replayCompletion.ErrorCode ?? "FIRST_GSKU_DRAFT_RECONCILIATION_REQUIRED",
                    replayCompletion.ErrorCode == "PRODUCT_CHILD_ADMISSION_CONFLICT" ? 409 : 202);
        }

        if (parent.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
        {
            return Fail("PARENT_NOT_IDENTITY_APPROVED", 409);
        }

        var selections = await ResolveSelectionsAsync(input.PackUomCode, cancellationToken);
        if (!selections.Succeeded)
        {
            return Fail(selections.ErrorCode!, selections.StatusCode);
        }

        var admission = await _globalProducts.AcquireChildCreationAdmissionAsync(
            parent.Id,
            commandId,
            admissionFingerprint,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!admission.Succeeded)
        {
            return Fail(admission.ErrorCode ?? "PRODUCT_CHILD_ADMISSION_CONFLICT",
                AdmissionStatus(admission.ErrorCode));
        }

        FirstGskuPairAllocationResult allocation;
        try
        {
            allocation = await _revisions.AllocateForFirstGskuAsync(parent.Id, commandId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
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
        catch (OperationCanceledException)
        {
            throw;
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
        catch (OperationCanceledException)
        {
            throw;
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

        var admissionCompletion = await _globalProducts.CompleteChildCreationAdmissionAsync(
            parent.Id,
            commandId,
            admissionFingerprint,
            cancellationToken);
        if (!admissionCompletion.Succeeded)
        {
            return Fail(
                admissionCompletion.ErrorCode ?? "FIRST_GSKU_DRAFT_RECONCILIATION_REQUIRED",
                admissionCompletion.ErrorCode == "PRODUCT_CHILD_ADMISSION_CONFLICT" ? 409 : 202);
        }

        return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Success(
            BuildDto(revisionResult.Revision, gskuResult.Gsku, actualReservation.BindingState, false),
            201);
    }

    private async Task<Response<ProductItemSkuMasterModels.FirstGskuDraftDto>?> EvaluateParentScopeAsync(
        Guid globalProductId,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.ResolveContextAsync("mdm.gskus.create", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Fail(scope.FailureCode!, scope.StatusCode);
        }

        var decision = await _scopeGuard.EvaluateAsync(scope.Context!, globalProductId, cancellationToken);
        return decision.Allowed ? null : Fail("PARENT_NOT_FOUND", 404);
    }

    private async Task<Response<ProductItemSkuMasterModels.FirstGskuDraftDto>?> EvaluateExistingPairScopeAsync(
        ProductDefinitionRevision? revision,
        Gsku? gsku,
        ProductLegalEntityScopeConsumerContext scopeContext,
        CancellationToken cancellationToken)
    {
        if (revision is null && gsku is null)
        {
            return null;
        }

        if (revision?.IsDeleted == true || gsku?.IsDeleted == true)
        {
            return Fail("PARENT_NOT_FOUND", 404);
        }

        if (revision is not null)
        {
            var revisionScopeFailure = await EvaluateExistingRevisionScopeAsync(
                revision,
                scopeContext,
                cancellationToken);
            if (revisionScopeFailure is not null)
            {
                return revisionScopeFailure;
            }
        }

        ProductDefinitionRevision? gskuRevision = null;
        if (gsku is not null)
        {
            gskuRevision = await _revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
            if (gskuRevision is null)
            {
                return Fail("PARENT_NOT_FOUND", 404);
            }

            var gskuScopeFailure = await EvaluateExistingRevisionScopeAsync(
                gskuRevision,
                scopeContext,
                cancellationToken);
            if (gskuScopeFailure is not null)
            {
                return gskuScopeFailure;
            }
        }

        return revision is not null && gskuRevision is not null && revision.Id != gskuRevision.Id
            ? Fail("CREATION_COMMAND_PAIR_CONFLICT", 409)
            : null;
    }

    private async Task<Response<ProductItemSkuMasterModels.FirstGskuDraftDto>?> EvaluateExistingRevisionScopeAsync(
        ProductDefinitionRevision revision,
        ProductLegalEntityScopeConsumerContext scopeContext,
        CancellationToken cancellationToken)
    {
        if (revision.IsDeleted)
        {
            return Fail("PARENT_NOT_FOUND", 404);
        }

        var product = await _globalProducts.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (product is null)
        {
            return Fail("PARENT_NOT_FOUND", 404);
        }

        var decision = await _scopeGuard.EvaluateAsync(scopeContext, product.Id, cancellationToken);
        return decision.Allowed ? null : Fail("PARENT_NOT_FOUND", 404);
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
        catch (OperationCanceledException)
        {
            throw;
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
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new LocalAuditIntent
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
            TimestampUtc = timestamp,
            TimestampUtcTicksV1 = timestamp.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))),
            SnapshotReference = $"{aggregateType}/{aggregateId:N}/0",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = $"{commandId}:{aggregateType}-create"
        };
    }

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
    internal static string ComputeAdmissionFingerprint(
        ProductItemSkuMasterModels.CreateFirstGskuDraftRequest input,
        string normalizedCommandId) => ProductChildCreationAdmission.ComputeRequestFingerprint(
            input.GlobalProductId,
            normalizedCommandId,
            input.GskuReservationId,
            input.PackQuantity,
            input.PackUomCode);

    private static int AdmissionStatus(string? code) => code switch
    {
        "PRODUCT_IDENTITY_NOT_FOUND" => 404,
        "PRODUCT_CHILD_ADMISSION_CONTRACT_INVALID" => 400,
        _ => 409
    };

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
