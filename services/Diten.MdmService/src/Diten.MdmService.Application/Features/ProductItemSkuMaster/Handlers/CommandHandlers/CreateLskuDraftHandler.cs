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

public sealed class CreateLskuDraftHandler
    : IRequestHandler<CreateLskuDraftCommand, Response<ProductItemSkuMasterModels.LskuDraftDto>>
{
    private readonly ICodeReservationRepository _reservations;
    private readonly ILskuRepository _lskus;
    private readonly IGskuRepository _gskus;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly IVerifiedMarketReferenceResolver _markets;
    private readonly ITenantContext _tenantContext;
    private readonly IProductIdentityActorContext _actorContext;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public CreateLskuDraftHandler(
        ICodeReservationRepository reservations,
        ILskuRepository lskus,
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        IVerifiedMarketReferenceResolver markets,
        ITenantContext tenantContext,
        IProductIdentityActorContext actorContext,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates)
    {
        _reservations = reservations;
        _lskus = lskus;
        _gskus = gskus;
        _revisions = revisions;
        _globalProducts = globalProducts;
        _markets = markets;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.LskuDraftDto>> Handle(
        CreateLskuDraftCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Request);
        var command = request.Request;
        var commandId = command.IdempotencyKey.Trim().ToUpperInvariant();
        var admissionFingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
            command.GskuId, GskuChildIdentityKind.Lsku, commandId, command.MarketCode);
        var scope = await _scopeGuard.ResolveContextAsync("mdm.lskus.create", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Fail(scope.FailureCode!, scope.StatusCode);
        }

        var replay = await _lskus.GetByCreationCommandIdAsync(commandId, cancellationToken);
        if (replay is not null)
        {
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
                return Fail("CREATION_COMMAND_TOMBSTONED", 409);
            }

            if (replay.GskuId != command.GskuId
                || !string.Equals(replay.MarketCode, command.MarketCode, StringComparison.Ordinal))
            {
                return Fail("IDEMPOTENCY_KEY_CONFLICT", 409);
            }

            return await CompleteReplayAsync(
                replay, commandId, admissionFingerprint, cancellationToken);
        }

        // LSKU FIX2 item 5: a REPEAT of a command that left traces (its consumed code, its admission) and is refused
        // early now cleans them up first — the accepted way out ("the same command repeats") reaches its cleanup.
        var scopeFailure = await EvaluateGskuScopeAsync(command.GskuId, scope.Context!, cancellationToken);
        if (scopeFailure is not null)
        {
            return await CleanUpThenAsync(command, commandId, admissionFingerprint, scopeFailure, definite: true,
                cancellationToken);
        }

        var gsku = await _gskus.GetReferenceableByIdAsync(command.GskuId, cancellationToken);
        if (gsku is null)
        {
            return await CleanUpThenAsync(command, commandId, admissionFingerprint,
                Fail("GSKU_NOT_REFERENCEABLE", 404), definite: true, cancellationToken);
        }

        // LSKU FIX1 B1 (a): a market the GSKU already has is refused before an admission is taken, so a duplicate
        // never leaves anything behind on the parent.
        var sameMarket = await _lskus.GetByIdentityKeyAsync(gsku.Id, command.MarketCode, cancellationToken);
        if (sameMarket is not null)
        {
            return await CleanUpThenAsync(command, commandId, admissionFingerprint,
                Fail(sameMarket.IsDeleted ? "LSKU_IDENTITY_TOMBSTONED" : MarketAlreadyExists, 409), definite: true,
                cancellationToken);
        }

        var resolution = await _markets.ResolveLatestAsync(command.MarketCode, cancellationToken);
        if (!resolution.IsSuccessful || resolution.Selection is null)
        {
            // FIX3 item 5 (b): only the provider's definite "not offered" ends this command's code; a provider that
            // cannot answer now (503/504, anything else) never burns it — the command's repeat goes on with it.
            return await CleanUpThenAsync(command, commandId, admissionFingerprint, Fail(
                resolution.FailureCode ?? "REFERENCE_PROVIDER_UNAVAILABLE",
                resolution.StatusCode is 404 or 503 or 504 ? resolution.StatusCode : 503),
                // LSKU FIX5 K5: definite only on Platform's own "not offered" — the status AND its exact reason.
                definite: resolution.StatusCode == 404
                          && string.Equals(resolution.FailureCode, MarketNotOffered, StringComparison.Ordinal),
                cancellationToken);
        }

        var admission = await _gskus.AcquireChildCreationAdmissionAsync(
            gsku.Id,
            GskuChildIdentityKind.Lsku,
            commandId,
            admissionFingerprint,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!admission.Succeeded)
        {
            return Fail(admission.ErrorCode ?? "GSKU_CHILD_ADMISSION_CONFLICT",
                AdmissionStatus(admission.ErrorCode));
        }

        // LSKU FIX1 B1 (b): an exit by exception (a store error, a cancelled request) gives the admission back too —
        // the release itself refuses while a live row or a live reservation could still become the child.
        try
        {
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
                when (exception.Message is "IDEMPOTENCY_KEY_CONFLICT" or "RESERVATION_IDEMPOTENCY_KEY_TOMBSTONED"
                          or "CANONICAL_CODE_ALLOCATION_FAILED")
            {
                // LSKU FIX1 B1 (b): no code is ours here, so only the admission goes back.
                await ReleaseAdmissionAsync(gsku.Id, commandId, admissionFingerprint, cancellationToken);
                return Fail(exception.Message, exception.Message == "CANONICAL_CODE_ALLOCATION_FAILED" ? 503 : 409);
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
                // The code stays reserved for this command (its repeat consumes it again). The release below refuses while
                // that reservation is live (CT 2026-10-06): the admission stays with it, counted by the gauge if it lasts.
                await ReleaseAdmissionAsync(gsku.Id, commandId, admissionFingerprint, cancellationToken);
                return Fail(consume.ErrorCode ?? "CODE_RESERVATION_REQUIRED", 409);
            }

            reservation = consume.Reservation;
            if (reservation.BindingState == CodeReservationBindingState.Burned)
            {
                await ReleaseAdmissionAsync(gsku.Id, commandId, admissionFingerprint, cancellationToken);
                return Fail("CODE_RESERVATION_BURNED", 409);
            }

            var lsku = BuildLsku(gsku.Id, command.MarketCode, resolution.Selection, reservation, identityId, commandId);
            var createResult = await _lskus.CreateDraftWithAdmissionAsync(
                lsku, admissionFingerprint, cancellationToken);
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

            // LSKU FIX1 B1 (b): a write the store definitely refused leaves no row of ours: the consumed code is burned
            // (it is never handed out again) and the admission goes back. The market that another request won between
            // the check above and this write is the same refusal as the check — 409, no longer a reconcilable 202.
            if (createResult.ConflictKind == LskuCreateConflictKind.IdentityKey)
            {
                var burnRefusal = await AbandonCreationAsync(gsku.Id, reservation, identityId, commandId,
                    admissionFingerprint, MarketAlreadyExists, cancellationToken);
                return Fail(burnRefusal ?? MarketAlreadyExists, burnRefusal == CodeReservationBurnCodes.TransactionUnavailable ? 503 : 409);
            }

            if (!createResult.Succeeded || createResult.Lsku is null)
            {
                var code = createResult.ErrorCode ?? "LSKU_WRITE_FAILED";
                var burnRefusal = await AbandonCreationAsync(gsku.Id, reservation, identityId, commandId,
                    admissionFingerprint, code, cancellationToken);
                return Fail(burnRefusal ?? code, burnRefusal == CodeReservationBurnCodes.TransactionUnavailable ? 503 : 409);
            }

            return await ConfirmAndMapAsync(
                createResult.Lsku, reservation, commandId, admissionFingerprint, cancellationToken);
        }
        catch (Exception)
        {
            using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await ReleaseAdmissionAsync(gsku.Id, commandId, admissionFingerprint, bound.Token); }
            catch (Exception) { /* the operator repair sweeps what could not be given back here */ }
            throw;
        }
    }

    private async Task<Response<ProductItemSkuMasterModels.LskuDraftDto>?> EvaluateGskuScopeAsync(
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
            return Fail("GSKU_NOT_REFERENCEABLE", 404);
        }

        var decision = await _scopeGuard.EvaluateAsync(scopeContext, product.Id, cancellationToken);
        return decision.Allowed ? null : Fail("GSKU_NOT_REFERENCEABLE", 404);
    }

    private async Task<Response<ProductItemSkuMasterModels.LskuDraftDto>?> EvaluateGskuScopeAsync(
        Guid gskuId,
        CancellationToken cancellationToken)
    {
        var scope = await _scopeGuard.ResolveContextAsync("mdm.lskus.create", cancellationToken);
        return scope.IsSuccessful
            ? await EvaluateGskuScopeAsync(gskuId, scope.Context!, cancellationToken)
            : Fail(scope.FailureCode!, scope.StatusCode);
    }

    private async Task<Response<ProductItemSkuMasterModels.LskuDraftDto>> CompleteReplayAsync(
        Lsku replay,
        string commandId,
        string admissionFingerprint,
        CancellationToken cancellationToken)
    {
        var scopeFailure = await EvaluateGskuScopeAsync(replay.GskuId, cancellationToken);
        if (scopeFailure is not null)
        {
            return scopeFailure;
        }

        var reservation = await _reservations.GetByIdAsync(replay.CodeReservationId, cancellationToken);
        if (!MatchesBinding(reservation, replay))
        {
            return Fail("LSKU_BINDING_INVARIANT_VIOLATION", 500);
        }

        if (reservation!.BindingState == CodeReservationBindingState.Confirmed)
        {
            return await CompleteAdmissionAndMapAsync(
                replay, reservation.BindingState, commandId, admissionFingerprint, cancellationToken);
        }

        if (reservation.BindingState != CodeReservationBindingState.PendingIdentityWrite)
        {
            return Fail("LSKU_BINDING_INVARIANT_VIOLATION", 500);
        }

        return await ConfirmAndMapAsync(
            replay, reservation, commandId, admissionFingerprint, cancellationToken);
    }

    private async Task<Response<ProductItemSkuMasterModels.LskuDraftDto>> ConfirmAndMapAsync(
        Lsku lsku,
        CodeReservation reservation,
        string commandId,
        string admissionFingerprint,
        CancellationToken cancellationToken)
    {
        var scopeFailure = await EvaluateGskuScopeAsync(lsku.GskuId, cancellationToken);
        if (scopeFailure is not null)
        {
            return scopeFailure;
        }

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
            return await CompleteAdmissionAndMapAsync(
                lsku, CodeReservationBindingState.Confirmed, commandId, admissionFingerprint, cancellationToken);
        }

        var actual = await _reservations.GetByIdAsync(reservation.Id, cancellationToken);
        if (!MatchesBinding(actual, lsku))
        {
            return Fail("LSKU_BINDING_INVARIANT_VIOLATION", 500);
        }

        return actual!.BindingState switch
        {
            CodeReservationBindingState.Confirmed => await CompleteAdmissionAndMapAsync(
                lsku, actual.BindingState, commandId, admissionFingerprint, cancellationToken),
            CodeReservationBindingState.PendingIdentityWrite => Success(lsku, actual.BindingState, true, 202),
            _ => Fail("LSKU_BINDING_INVARIANT_VIOLATION", 500)
        };
    }

    private async Task<Response<ProductItemSkuMasterModels.LskuDraftDto>> CompleteAdmissionAndMapAsync(
        Lsku lsku,
        CodeReservationBindingState bindingState,
        string commandId,
        string admissionFingerprint,
        CancellationToken cancellationToken)
    {
        var completion = await _gskus.CompleteChildCreationAdmissionAsync(
            lsku.GskuId,
            GskuChildIdentityKind.Lsku,
            commandId,
            admissionFingerprint,
            cancellationToken);
        return completion.Succeeded
            ? Success(lsku, bindingState, false, 201)
            : Fail(completion.ErrorCode ?? "LSKU_BINDING_RECONCILIATION_REQUIRED",
                completion.ErrorCode == "GSKU_CHILD_ADMISSION_CONFLICT" ? 409 : 202);
    }

    private static int AdmissionStatus(string? code) => code switch
    {
        "GSKU_NOT_REFERENCEABLE" or "GSKU_NOT_FOUND" => 404,
        "GSKU_CHILD_ADMISSION_LIMIT_REACHED" => 409,
        _ => 409
    };

    public const string MarketAlreadyExists = "LSKU_MARKET_ALREADY_EXISTS";
    // The market resolver's exact "this market is not offered" reason (the clients' MarketNotOfferedReason).
    public const string MarketNotOffered = "REFERENCE_MARKET_NOT_FOUND";

    public const string BurnRefused = "LSKU_CODE_BURN_REFUSED";

    // Returns null when the code is burned (or kept by a row bound to it) and the admission released; a refused burn
    // is NOT ignored (FIX2 item 5): the admission stays with the live code and the answer names the refusal.
    private async Task<string?> AbandonCreationAsync(
        Guid gskuId,
        CodeReservation reservation,
        Guid identityId,
        string commandId,
        string admissionFingerprint,
        string reason,
        CancellationToken cancellationToken)
    {
        // A row bound to this reservation (a replay the store answered as a conflict) keeps its code; a code already
        // burned (an overlapping burn won, FIX3 5c) needs no second burn.
        if (await _lskus.GetByReservationIdAsync(reservation.Id, cancellationToken) is null
            && (await _reservations.GetByIdAsync(reservation.Id, cancellationToken))?.BindingState
                != CodeReservationBindingState.Burned)
        {
            var burned = await _reservations.BurnIdentityBindingAsync(
                reservation.Id,
                identityId,
                reservation.Version,
                commandId + ":burn",
                _actorContext.ActorId,
                commandId,
                reason,
                cancellationToken);
            if (!burned.Succeeded)
                return burned.ErrorCode == CodeReservationBurnCodes.TransactionUnavailable
                    ? CodeReservationBurnCodes.TransactionUnavailable : BurnRefused;
        }

        await ReleaseAdmissionAsync(gskuId, commandId, admissionFingerprint, cancellationToken);
        return null;
    }

    // LSKU FIX2 item 5: this command's own traces — a code it consumed for an identity whose row never came, and its
    // admission — are finished before an early refusal is answered. A refused burn answers with its own code.
    // FIX3 item 5 (a/b): only for THE SAME request. A live code of this key is ours only when this GSKU holds the
    // key's admission with this request's fingerprint (GSKU + market) and the code was reserved by this actor; any
    // other request with the key — another GSKU or market, another user — is IDEMPOTENCY_KEY_CONFLICT and touches
    // nothing. And only a DEFINITE refusal burns; a wait (a provider 503/504) leaves the code to the command's repeat.
    private async Task<Response<ProductItemSkuMasterModels.LskuDraftDto>> CleanUpThenAsync(
        ProductItemSkuMasterModels.CreateLskuDraftRequest command,
        string commandId,
        string admissionFingerprint,
        Response<ProductItemSkuMasterModels.LskuDraftDto> refusal,
        bool definite,
        CancellationToken cancellationToken)
    {
        var own = await _reservations.GetByReservationCommandAsync(CodeBearingEntityType.Lsku, commandId, cancellationToken);
        var live = own is { ReservationState: CodeReservationState.Reserved }
                   || own is { ReservationState: CodeReservationState.Consumed }
                      && own.BindingState != CodeReservationBindingState.Burned;
        if (live)
        {
            var parent = await _gskus.GetByIdAsync(command.GskuId, cancellationToken);
            var admission = parent?.ChildCreationAdmissions.SingleOrDefault(x =>
                x.ChildKind == GskuChildIdentityKind.Lsku && x.CreationCommandId == commandId);
            if (admission is null || admission.RequestFingerprint != admissionFingerprint
                || !string.Equals(own!.ReservedByActorId, _actorContext.ActorId, StringComparison.Ordinal))
                return Fail("IDEMPOTENCY_KEY_CONFLICT", 409);
        }

        if (!definite)
        {
            if (!live) await ReleaseAdmissionAsync(command.GskuId, commandId, admissionFingerprint, cancellationToken);
            return refusal;
        }
        if (own is { ReservationState: CodeReservationState.Consumed,
                     BindingState: CodeReservationBindingState.PendingIdentityWrite,
                     ConsumedEntityId: { } identityId }
            && await _lskus.GetByReservationIdAsync(own.Id, cancellationToken) is null)
        {
            var burned = await _reservations.BurnIdentityBindingAsync(own.Id, identityId, own.Version,
                commandId + ":burn", _actorContext.ActorId, commandId,
                refusal.Errors.FirstOrDefault() ?? "LSKU_CREATION_REFUSED", cancellationToken);
            if (!burned.Succeeded)
                return burned.ErrorCode == CodeReservationBurnCodes.TransactionUnavailable
                    ? Fail(CodeReservationBurnCodes.TransactionUnavailable, 503) : Fail(BurnRefused, 409);
        }

        await ReleaseAdmissionAsync(command.GskuId, commandId, admissionFingerprint, cancellationToken);
        return refusal;
    }

    // The release refuses on its own while a live row or a live reservation of this command exists, so it is safe on
    // every exit; it actually frees the admission only where nothing can still become the child.
    private Task ReleaseAdmissionAsync(
        Guid gskuId,
        string commandId,
        string admissionFingerprint,
        CancellationToken cancellationToken) =>
        _gskus.ReleaseChildCreationAdmissionAsync(
            gskuId, GskuChildIdentityKind.Lsku, commandId, admissionFingerprint, cancellationToken);

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
            TimestampUtcTicksV1 = timestamp.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
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

    // LSKU FIX5 item 4: the same command is recognised by its IDENTITY facts only — the market selection is resolved
    // again on every call (Platform stamps ResolvedAtUtc anew), so two concurrent requests of one key would otherwise
    // see each other as a conflict (409) instead of a replay.
    private static bool SameFacts(Lsku left, Lsku right) =>
        left.Id == right.Id
        && left.GskuId == right.GskuId
        && left.CodeReservationId == right.CodeReservationId
        && string.Equals(left.CanonicalCode, right.CanonicalCode, StringComparison.Ordinal)
        && string.Equals(left.CreationCommandId, right.CreationCommandId, StringComparison.Ordinal)
        && string.Equals(left.MarketCode, right.MarketCode, StringComparison.Ordinal);


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
