using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;

public sealed class ProductAbbreviationWorkflow
{
    private readonly IProductAbbreviationRegisterRepository _register;
    private readonly IProductAbbreviationAllocationLedgerRepository _ledger;
    private readonly IProductAbbreviationHistoryRepository _history;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly IProductAbbreviationActorContext _actor;
    private readonly ProductAbbreviationAuthorization _authorization;

    public ProductAbbreviationWorkflow(
        IProductAbbreviationRegisterRepository register,
        IProductAbbreviationAllocationLedgerRepository ledger,
        IProductAbbreviationHistoryRepository history,
        IGlobalProductRepository globalProducts,
        IProductAbbreviationActorContext actor,
        ProductAbbreviationAuthorization authorization)
    {
        _register = register;
        _ledger = ledger;
        _history = history;
        _globalProducts = globalProducts;
        _actor = actor;
        _authorization = authorization;
    }

    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>> RequestAsync(
        RequestProductAbbreviationAllocationCommand command,
        CancellationToken cancellationToken)
    {
        if (!ProductAbbreviationNormalizer.TryNormalize(command.Abbreviation, out var normalized))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                "ABBREVIATION_GRAMMAR_INVALID");
        }

        var denied = Demand<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>(
            ProductAbbreviationPermissions.Request);
        if (denied is not null)
        {
            return denied;
        }

        if (await _globalProducts.GetByIdAsync(command.GlobalProductId, cancellationToken) is null)
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                "GLOBAL_PRODUCT_NOT_FOUND",
                404);
        }

        // FIX1 S2: a product holds one code. While it has an active or a pending one, a new request is refused here
        // — before the code is consumed in the ledger for good — instead of failing at approval much later.
        if (await _register.HasOpenEntryForProductAsync(
                command.GlobalProductId,
                EntryIdFor(command.IdempotencyKey),
                cancellationToken))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                "ABBREVIATION_PRODUCT_ALREADY_HAS_ENTRY",
                409);
        }

        return await CreateRequestedEntryAsync(
            command.GlobalProductId,
            normalized,
            command.IdempotencyKey,
            replacesEntryId: null,
            ProductAbbreviationHistoryEventType.ALLOCATION_REQUESTED,
            reason: null,
            cancellationToken);
    }

    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>>
        InitiateCorrectionAsync(
            InitiateProductAbbreviationCorrectionCommand command,
            CancellationToken cancellationToken)
    {
        if (!ProductAbbreviationNormalizer.TryNormalize(command.ReplacementAbbreviation, out var normalized))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                "ABBREVIATION_GRAMMAR_INVALID");
        }

        var denied = Demand<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>(
            ProductAbbreviationPermissions.Correct);
        if (denied is not null)
        {
            return denied;
        }

        var former = await _register.GetByIdAsync(command.ActiveRegisterEntryId, cancellationToken);
        if (former is null)
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                "ABBREVIATION_NOT_FOUND",
                404);
        }

        if (former.Version != command.ExpectedVersion)
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                "CONCURRENCY_CONFLICT",
                409);
        }

        if (former.LifecycleStatus != ProductAbbreviationLifecycleStatus.ACTIVE)
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                "ABBREVIATION_CORRECTION_REQUIRES_ACTIVE",
                409);
        }

        if (former.NormalizedAbbreviation == normalized)
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                "ABBREVIATION_CORRECTION_VALUE_UNCHANGED",
                409);
        }

        // FIX1 B3: one pending change per code. A correction while a retirement waits — or a second correction while
        // the first waits — would leave one of them an orphan the moment the other is approved.
        if (!string.IsNullOrWhiteSpace(former.RetirementRequestId))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                "ABBREVIATION_RETIREMENT_PENDING",
                409);
        }

        var correctionEntryId = EntryIdFor(command.IdempotencyKey);
        if (former.PendingCorrectionEntryId is { } claimed && claimed != correctionEntryId
            && await IsPendingAsync(claimed, cancellationToken))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                "ABBREVIATION_CORRECTION_PENDING",
                409);
        }

        if (!await _register.ClaimPendingCorrectionAsync(
                former.Id,
                former.PendingCorrectionEntryId,
                correctionEntryId,
                cancellationToken))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                "CONCURRENCY_CONFLICT",
                409);
        }

        return await CreateRequestedEntryAsync(
            former.GlobalProductId,
            normalized,
            command.IdempotencyKey,
            former.Id,
            ProductAbbreviationHistoryEventType.CORRECTION_REQUESTED,
            command.Reason,
            cancellationToken);
    }

    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>> CancelAsync(
        CancelProductAbbreviationAllocationCommand command,
        CancellationToken cancellationToken)
    {
        var denied = Demand<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
            ProductAbbreviationPermissions.Cancel);
        if (denied is not null)
        {
            return denied;
        }

        var entry = await _register.GetByIdAsync(command.RegisterEntryId, cancellationToken);
        if (entry is null)
        {
            return NotFoundEntry();
        }

        if (!string.Equals(
                entry.RequestedByCanonicalSubjectId,
                _actor.CanonicalHumanSubjectId,
                StringComparison.Ordinal))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                "ABBREVIATION_CANCEL_NOT_REQUEST_OWNER",
                403);
        }

        if (await ClaimKeyAsync<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
                command.IdempotencyKey, entry.Id, cancellationToken) is { } cancelReused)
        {
            return cancelReused;
        }

        var result = await _register.TransitionAsync(
            entry.Id,
            command.ExpectedVersion,
            ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.CANCELLED,
            _actor.CanonicalHumanSubjectId,
            command.IdempotencyKey,
            command.Reason,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!result.Succeeded || result.Entry is null)
        {
            return WriteFailure(result);
        }

        var eventType = entry.ReplacesEntryId.HasValue
            ? ProductAbbreviationHistoryEventType.CORRECTION_CANCELLED
            : ProductAbbreviationHistoryEventType.ALLOCATION_CANCELLED;
        return await CompleteTransitionAsync(
            result.Entry,
            eventType,
            ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.CANCELLED,
            command.IdempotencyKey,
            command.Reason,
            cancellationToken);
    }

    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>> ApproveAsync(
        ApproveProductAbbreviationAllocationCommand command,
        CancellationToken cancellationToken)
    {
        var denied = Demand<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
            ProductAbbreviationPermissions.Approve);
        if (denied is not null)
        {
            return denied;
        }

        var entry = await _register.GetByIdAsync(command.RegisterEntryId, cancellationToken);
        if (entry is null)
        {
            return NotFoundEntry();
        }

        if (SameSubject(entry.RequestedByCanonicalSubjectId))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                "ABBREVIATION_MAKER_CHECKER_VIOLATION",
                403);
        }

        if (await ClaimKeyAsync<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
                command.IdempotencyKey, entry.Id, cancellationToken) is { } reused)
        {
            return reused;
        }

        if (entry.ReplacesEntryId is { } formerId)
        {
            if (!command.ExpectedFormerVersion.HasValue)
            {
                return Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                    "ABBREVIATION_FORMER_EXPECTED_VERSION_REQUIRED");
            }

            // FIX1 B1: retire-and-activate is ONE transaction with both history rows and both audit intents, and it
            // runs only while the correction is still requested (checked inside, against the stored document).
            var decidedAtUtc = DateTimeOffset.UtcNow;
            var correction = await _register.ApproveCorrectionAsync(
                new ProductAbbreviationCorrectionApproval(
                    formerId,
                    command.ExpectedFormerVersion.Value,
                    entry.Id,
                    command.ExpectedVersion,
                    _actor.CanonicalHumanSubjectId,
                    command.IdempotencyKey,
                    command.Reason,
                    decidedAtUtc,
                    (formerPost, replacementPost) =>
                    {
                        var formerHistory = BuildHistory(
                            formerPost,
                            ProductAbbreviationHistoryEventType.CORRECTION_APPROVED,
                            ProductAbbreviationLifecycleStatus.ACTIVE,
                            ProductAbbreviationLifecycleStatus.RETIRED,
                            command.IdempotencyKey + ":former",
                            command.Reason,
                            decidedAtUtc);
                        var replacementHistory = BuildHistory(
                            replacementPost,
                            ProductAbbreviationHistoryEventType.CORRECTION_APPROVED,
                            ProductAbbreviationLifecycleStatus.REQUESTED,
                            ProductAbbreviationLifecycleStatus.ACTIVE,
                            command.IdempotencyKey,
                            command.Reason,
                            decidedAtUtc);
                        return new ProductAbbreviationCorrectionEvidence(
                            formerHistory,
                            ProductAbbreviationAuditIntentFactory.Create(formerPost, formerHistory),
                            replacementHistory,
                            ProductAbbreviationAuditIntentFactory.Create(replacementPost, replacementHistory));
                    }),
                cancellationToken);
            return correction is { Succeeded: true, Entry: not null }
                ? Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Success(
                    ToDto(correction.Entry))
                : WriteFailure(correction);
        }

        var result = await _register.TransitionAsync(
            entry.Id,
            command.ExpectedVersion,
            ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE,
            _actor.CanonicalHumanSubjectId,
            command.IdempotencyKey,
            command.Reason,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!result.Succeeded || result.Entry is null)
        {
            return WriteFailure(result);
        }

        return await CompleteTransitionAsync(
            result.Entry,
            ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED,
            ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE,
            command.IdempotencyKey,
            command.Reason,
            cancellationToken);
    }

    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>> RejectAsync(
        RejectProductAbbreviationAllocationCommand command,
        CancellationToken cancellationToken)
    {
        var denied = Demand<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
            ProductAbbreviationPermissions.Reject);
        if (denied is not null)
        {
            return denied;
        }

        var entry = await _register.GetByIdAsync(command.RegisterEntryId, cancellationToken);
        if (entry is null)
        {
            return NotFoundEntry();
        }

        if (SameSubject(entry.RequestedByCanonicalSubjectId))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                "ABBREVIATION_MAKER_CHECKER_VIOLATION",
                403);
        }

        if (await ClaimKeyAsync<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
                command.IdempotencyKey, entry.Id, cancellationToken) is { } rejectReused)
        {
            return rejectReused;
        }

        var result = await _register.TransitionAsync(
            entry.Id,
            command.ExpectedVersion,
            ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.REJECTED,
            _actor.CanonicalHumanSubjectId,
            command.IdempotencyKey,
            command.Reason,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!result.Succeeded || result.Entry is null)
        {
            return WriteFailure(result);
        }

        return await CompleteTransitionAsync(
            result.Entry,
            entry.ReplacesEntryId.HasValue
                ? ProductAbbreviationHistoryEventType.CORRECTION_REJECTED
                : ProductAbbreviationHistoryEventType.ALLOCATION_REJECTED,
            ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.REJECTED,
            command.IdempotencyKey,
            command.Reason,
            cancellationToken);
    }

    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>
        RequestRetirementAsync(
            RequestProductAbbreviationRetirementCommand command,
            CancellationToken cancellationToken)
    {
        var denied = Demand<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
            ProductAbbreviationPermissions.Retire);
        if (denied is not null)
        {
            return denied;
        }

        var entry = await _register.GetByIdAsync(command.RegisterEntryId, cancellationToken);
        if (entry is null)
        {
            return NotFoundEntry();
        }

        // FIX1 B3: no retirement while a correction of this code waits (the reverse is refused in InitiateCorrection).
        if (entry.PendingCorrectionEntryId is { } claimed && await IsPendingAsync(claimed, cancellationToken))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                "ABBREVIATION_CORRECTION_PENDING",
                409);
        }

        if (await ClaimKeyAsync<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
                command.IdempotencyKey, entry.Id, cancellationToken) is { } reused)
        {
            return reused;
        }

        var result = await _register.RequestRetirementAsync(
            command.RegisterEntryId,
            command.ExpectedVersion,
            command.IdempotencyKey,
            _actor.CanonicalHumanSubjectId,
            command.IdempotencyKey,
            command.Reason,
            DateTimeOffset.UtcNow,
            cancellationToken,
            observedPendingCorrection: entry.PendingCorrectionEntryId);
        if (!result.Succeeded || result.Entry is null)
        {
            return WriteFailure(result);
        }

        return await CompleteTransitionAsync(
            result.Entry,
            ProductAbbreviationHistoryEventType.RETIREMENT_REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE,
            ProductAbbreviationLifecycleStatus.ACTIVE,
            command.IdempotencyKey,
            command.Reason,
            cancellationToken);
    }

    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>
        ApproveRetirementAsync(
            ApproveProductAbbreviationRetirementCommand command,
            CancellationToken cancellationToken)
    {
        var denied = Demand<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
            ProductAbbreviationPermissions.Approve);
        if (denied is not null)
        {
            return denied;
        }

        var entry = await _register.GetByIdAsync(command.RegisterEntryId, cancellationToken);
        if (entry is null)
        {
            return NotFoundEntry();
        }

        if (entry.RetirementRequestId != command.RetirementRequestId)
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                "ABBREVIATION_RETIREMENT_REQUEST_NOT_FOUND",
                409);
        }

        if (SameSubject(entry.RetirementRequestedByCanonicalSubjectId))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                "ABBREVIATION_MAKER_CHECKER_VIOLATION",
                403);
        }

        if (await ClaimKeyAsync<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
                command.IdempotencyKey, entry.Id, cancellationToken) is { } retireReused)
        {
            return retireReused;
        }

        var result = await _register.TransitionAsync(
            entry.Id,
            command.ExpectedVersion,
            ProductAbbreviationLifecycleStatus.ACTIVE,
            ProductAbbreviationLifecycleStatus.RETIRED,
            _actor.CanonicalHumanSubjectId,
            command.IdempotencyKey,
            command.Reason,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!result.Succeeded || result.Entry is null)
        {
            return WriteFailure(result);
        }

        return await CompleteTransitionAsync(
            result.Entry,
            ProductAbbreviationHistoryEventType.RETIREMENT_APPROVED,
            ProductAbbreviationLifecycleStatus.ACTIVE,
            ProductAbbreviationLifecycleStatus.RETIRED,
            command.IdempotencyKey,
            command.Reason,
            cancellationToken);
    }

    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>
        RejectRetirementAsync(
            RejectProductAbbreviationRetirementCommand command,
            CancellationToken cancellationToken)
    {
        var denied = Demand<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
            ProductAbbreviationPermissions.Reject);
        if (denied is not null)
        {
            return denied;
        }

        var entry = await _register.GetByIdAsync(command.RegisterEntryId, cancellationToken);
        if (entry is null)
        {
            return NotFoundEntry();
        }

        if (entry.RetirementRequestId != command.RetirementRequestId)
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                "ABBREVIATION_RETIREMENT_REQUEST_NOT_FOUND",
                409);
        }

        if (SameSubject(entry.RetirementRequestedByCanonicalSubjectId))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                "ABBREVIATION_MAKER_CHECKER_VIOLATION",
                403);
        }

        if (await ClaimKeyAsync<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
                command.IdempotencyKey, entry.Id, cancellationToken) is { } keepReused)
        {
            return keepReused;
        }

        var result = await _register.ClearRetirementRequestAsync(
            entry.Id,
            command.ExpectedVersion,
            command.RetirementRequestId,
            _actor.CanonicalHumanSubjectId,
            command.IdempotencyKey,
            command.Reason,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!result.Succeeded || result.Entry is null)
        {
            return WriteFailure(result);
        }

        return await CompleteTransitionAsync(
            result.Entry,
            ProductAbbreviationHistoryEventType.RETIREMENT_REJECTED,
            ProductAbbreviationLifecycleStatus.ACTIVE,
            ProductAbbreviationLifecycleStatus.ACTIVE,
            command.IdempotencyKey,
            command.Reason,
            cancellationToken);
    }

    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>
        GetByGlobalProductAsync(Guid globalProductId, CancellationToken cancellationToken)
    {
        var denied = Demand<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>(
            ProductAbbreviationPermissions.Read);
        if (denied is not null)
        {
            return denied;
        }

        var entry = await _register.GetActiveByGlobalProductIdAsync(globalProductId, cancellationToken);
        return entry is null
            ? NotFoundEntry()
            : Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Success(ToDto(entry));
    }

    public async Task<Response<IReadOnlyList<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterListItemDto>>>
        ListByGlobalProductAsync(Guid globalProductId, CancellationToken cancellationToken)
    {
        var denied = Demand<IReadOnlyList<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterListItemDto>>(
            ProductAbbreviationPermissions.Read);
        if (denied is not null)
        {
            return denied;
        }

        var entries = await _register.GetByGlobalProductIdAsync(globalProductId, cancellationToken);
        var pendingCorrectionOf = entries
            .Where(x => x.ReplacesEntryId.HasValue && x.LifecycleStatus == ProductAbbreviationLifecycleStatus.REQUESTED)
            .Select(x => x.ReplacesEntryId!.Value)
            .ToHashSet();
        IReadOnlyList<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterListItemDto> rows = entries
            .Select(entry => new ProductAbbreviationRegisterModels.ProductAbbreviationRegisterListItemDto(
                entry.Id,
                entry.GlobalProductId,
                entry.NormalizedAbbreviation,
                entry.LifecycleStatus,
                entry.Version,
                entry.ReplacesEntryId,
                entry.RetirementRequestId is not null,
                pendingCorrectionOf.Contains(entry.Id),
                SameSubject(entry.RequestedByCanonicalSubjectId),
                SameSubject(entry.RetirementRequestedByCanonicalSubjectId)))
            .ToList();
        return Response<IReadOnlyList<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterListItemDto>>
            .Success(rows);
    }

    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationResolutionDto>> ResolveAsync(
        string abbreviation,
        CancellationToken cancellationToken)
    {
        if (!ProductAbbreviationNormalizer.TryNormalize(abbreviation, out var normalized))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationResolutionDto>.Fail(
                "ABBREVIATION_GRAMMAR_INVALID");
        }

        var denied = Demand<ProductAbbreviationRegisterModels.ProductAbbreviationResolutionDto>(
            ProductAbbreviationPermissions.Read);
        if (denied is not null)
        {
            return denied;
        }

        var entry = await _register.ResolveActiveAsync(normalized, cancellationToken);
        return entry is null
            ? Response<ProductAbbreviationRegisterModels.ProductAbbreviationResolutionDto>.Fail(
                "ABBREVIATION_NOT_FOUND",
                404)
            : Response<ProductAbbreviationRegisterModels.ProductAbbreviationResolutionDto>.Success(
                new(entry.Id, entry.GlobalProductId, entry.NormalizedAbbreviation, entry.Version));
    }

    public async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationEvidenceDto>>
        GetEvidenceAsync(Guid registerEntryId, CancellationToken cancellationToken)
    {
        var readDenied = Demand<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationEvidenceDto>(
            ProductAbbreviationPermissions.Read);
        if (readDenied is not null)
        {
            return readDenied;
        }

        var auditDenied = Demand<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationEvidenceDto>(
            ProductAbbreviationPermissions.Audit);
        if (auditDenied is not null)
        {
            return auditDenied;
        }

        var entry = await _register.GetByIdAsync(registerEntryId, cancellationToken);
        if (entry is null)
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationEvidenceDto>.Fail(
                "ABBREVIATION_NOT_FOUND",
                404);
        }

        var ledger = await _ledger.GetByIdAsync(entry.AllocationLedgerId, cancellationToken);
        if (ledger is null)
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationEvidenceDto>.Fail(
                "ABBREVIATION_LEDGER_INVARIANT_VIOLATION",
                500);
        }

        var history = await _history.GetForRegisterEntryAsync(entry.Id, cancellationToken);
        return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationEvidenceDto>.Success(
            new(
                entry.Id,
                ledger.Id,
                entry.NormalizedAbbreviation,
                ledger.AllocationState,
                history.Select(item => new ProductAbbreviationRegisterModels.ProductAbbreviationEvidenceItemDto(
                        item.EventType,
                        item.BeforeStatus,
                        item.AfterStatus,
                        item.CanonicalHumanSubjectId,
                        item.IdempotencyKey,
                        item.CorrelationId,
                        item.Reason,
                        item.EvidenceHash,
                        item.OccurredAtUtc))
                    .ToList()));
    }

    private async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>>
        CreateRequestedEntryAsync(
            Guid globalProductId,
            string normalized,
            string idempotencyKey,
            Guid? replacesEntryId,
            ProductAbbreviationHistoryEventType eventType,
            string? reason,
            CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var entryId = EntryIdFor(idempotencyKey);
        if (await ClaimKeyAsync<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>(
                idempotencyKey, entryId, cancellationToken) is { } reused)
        {
            return reused;
        }

        var ledgerId = DeterministicGuid($"{_actor.TenantId:N}|ledger|{idempotencyKey}");
        var payloadHash = Hash($"{_actor.TenantId:N}|{normalized}|{globalProductId:N}|{entryId:N}|{replacesEntryId:N}");
        var allocation = new ProductAbbreviationAllocationLedger
        {
            Id = ledgerId,
            NormalizedAbbreviation = normalized,
            GlobalProductId = globalProductId,
            RegisterEntryId = entryId,
            IdempotencyKey = idempotencyKey,
            PayloadHash = payloadHash,
            AllocationState = ProductAbbreviationAllocationState.DURABLY_ALLOCATED,
            AllocatedByCanonicalSubjectId = _actor.CanonicalHumanSubjectId,
            AllocatedAtUtc = now
        };
        var ledgerResult = await _ledger.AllocateAsync(allocation, cancellationToken);
        if (!ledgerResult.Succeeded || ledgerResult.Ledger is null)
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                ledgerResult.ErrorCode ?? "ABBREVIATION_ALLOCATION_FAILED",
                409);
        }

        var entry = new ProductAbbreviationRegisterEntry
        {
            Id = entryId,
            NormalizedAbbreviation = normalized,
            GlobalProductId = globalProductId,
            AllocationLedgerId = ledgerResult.Ledger.Id,
            AllocationIdempotencyKey = idempotencyKey,
            LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
            RequestedByCanonicalSubjectId = _actor.CanonicalHumanSubjectId,
            RequestedAtUtc = now,
            ReplacesEntryId = replacesEntryId
        };
        var registerResult = await _register.InsertRequestedAsync(entry, cancellationToken);
        if (!registerResult.Succeeded || registerResult.Entry is null)
        {
            // FIX1 S4: a refusal is a refusal (409, with its own sentence on the screen); only an outcome that is not
            // known is "repeat the same request to finish it" (202).
            return registerResult.ErrorCode is "ABBREVIATION_REQUEST_ALREADY_CLOSED"
                or "ABBREVIATION_IDEMPOTENCY_CONFLICT"
                or "ABBREVIATION_REGISTER_CONFLICT"
                ? Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                    registerResult.ErrorCode,
                    409)
                : Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                    "ABBREVIATION_REGISTER_RECONCILIATION_REQUIRED",
                    202);
        }

        if (!await AppendHistoryAsync(
                registerResult.Entry,
                eventType,
                null,
                ProductAbbreviationLifecycleStatus.REQUESTED,
                idempotencyKey,
                reason,
                cancellationToken))
        {
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
                "ABBREVIATION_EVIDENCE_RECONCILIATION_REQUIRED",
                202);
        }

        return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Success(
            new(
                ToDto(registerResult.Entry),
                ledgerResult.IsReplay || registerResult.IsReplay,
                false),
            registerResult.IsReplay ? 200 : 201);
    }

    private async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>
        CompleteTransitionAsync(
            ProductAbbreviationRegisterEntry entry,
            ProductAbbreviationHistoryEventType eventType,
            ProductAbbreviationLifecycleStatus before,
            ProductAbbreviationLifecycleStatus after,
            string idempotencyKey,
            string? reason,
            CancellationToken cancellationToken)
        => await AppendHistoryAsync(entry, eventType, before, after, idempotencyKey, reason, cancellationToken)
            ? Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Success(ToDto(entry))
            : Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                "ABBREVIATION_EVIDENCE_RECONCILIATION_REQUIRED",
                202);

    private async Task<bool> AppendHistoryAsync(
        ProductAbbreviationRegisterEntry entry,
        ProductAbbreviationHistoryEventType eventType,
        ProductAbbreviationLifecycleStatus? before,
        ProductAbbreviationLifecycleStatus? after,
        string idempotencyKey,
        string? reason,
        CancellationToken cancellationToken)
    {
        var history = BuildHistory(entry, eventType, before, after, idempotencyKey, reason, DateTimeOffset.UtcNow);
        if (!await _history.AppendIfAbsentAsync(history, cancellationToken))
        {
            return false;
        }

        var persisted = (await _history.GetForRegisterEntryAsync(entry.Id, cancellationToken))
            .Where(candidate => candidate.EventType == eventType
                && candidate.IdempotencyKey == idempotencyKey
                && candidate.EvidenceHash == history.EvidenceHash).Take(2).ToArray();
        if (persisted.Length != 1)
        {
            return false;
        }
        var intent = ProductAbbreviationAuditIntentFactory.Create(entry, persisted[0]);
        return await _register.AppendAuditIntentIfAbsentAsync(entry.Id, intent, cancellationToken);
    }

    private ProductAbbreviationHistoryEntry BuildHistory(
        ProductAbbreviationRegisterEntry entry,
        ProductAbbreviationHistoryEventType eventType,
        ProductAbbreviationLifecycleStatus? before,
        ProductAbbreviationLifecycleStatus? after,
        string idempotencyKey,
        string? reason,
        DateTimeOffset timestamp)
    {
        var evidence = Hash(
            $"{_actor.TenantId:N}|{entry.Id:N}|{entry.GlobalProductId:N}|{entry.NormalizedAbbreviation}|{eventType}|{before}|{after}|{_actor.CanonicalHumanSubjectId}|{idempotencyKey}|{reason}");
        return new ProductAbbreviationHistoryEntry
        {
            Id = DeterministicGuid($"{_actor.TenantId:N}|history|{entry.Id:N}|{eventType}|{idempotencyKey}"),
            TenantId = _actor.TenantId,
            RegisterEntryId = entry.Id,
            GlobalProductId = entry.GlobalProductId,
            NormalizedAbbreviation = entry.NormalizedAbbreviation,
            EventType = eventType,
            BeforeStatus = before,
            AfterStatus = after,
            CanonicalHumanSubjectId = _actor.CanonicalHumanSubjectId,
            ActorType = _actor.ActorType,
            IdempotencyKey = idempotencyKey,
            CorrelationId = string.IsNullOrWhiteSpace(_actor.CorrelationId)
                ? idempotencyKey
                : _actor.CorrelationId,
            Reason = reason,
            EvidenceHash = evidence,
            OccurredAtUtc = timestamp,
            PostVersion = entry.Version
        };
    }

    /// <summary>FIX1 S3 — binds the key to this entry before anything is written under it.</summary>
    private async Task<Response<T>?> ClaimKeyAsync<T>(
        string idempotencyKey,
        Guid registerEntryId,
        CancellationToken cancellationToken)
    {
        var refused = await _history.ClaimCommandKeyAsync(idempotencyKey, registerEntryId, cancellationToken);
        return refused is null ? null : Response<T>.Fail(refused, 409);
    }

    /// <summary>A pending-change claim counts only while the entry it names is still waiting for a decision.</summary>
    private async Task<bool> IsPendingAsync(Guid entryId, CancellationToken cancellationToken)
        => (await _register.GetByIdAsync(entryId, cancellationToken))?.LifecycleStatus
           == ProductAbbreviationLifecycleStatus.REQUESTED;

    private Guid EntryIdFor(string idempotencyKey)
        => DeterministicGuid($"{_actor.TenantId:N}|register|{idempotencyKey}");

    private Response<T>? Demand<T>(string permission)
    {
        var result = _authorization.Demand(permission);
        return result.Succeeded ? null : Response<T>.Fail(result.ErrorCode!, result.StatusCode);
    }

    private bool SameSubject(string? canonicalSubjectId)
        => !string.IsNullOrEmpty(canonicalSubjectId)
           && string.Equals(
               canonicalSubjectId,
               _actor.CanonicalHumanSubjectId,
               StringComparison.Ordinal);

    private static Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto> NotFoundEntry()
        => Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
            "ABBREVIATION_NOT_FOUND",
            404);

    private static Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto> WriteFailure(
        ProductAbbreviationRegisterWriteResult result)
        => Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
            result.ErrorCode ?? "ABBREVIATION_WRITE_FAILED",
            result.ErrorCode switch
            {
                "ABBREVIATION_NOT_FOUND" => 404,
                "ABBREVIATION_PERSISTENCE_UNAVAILABLE" => 503,
                _ => 409
            });

    private static ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto ToDto(
        ProductAbbreviationRegisterEntry entry)
        => new(
            entry.Id,
            entry.GlobalProductId,
            entry.NormalizedAbbreviation,
            entry.LifecycleStatus,
            entry.Version,
            entry.ReplacesEntryId,
            entry.RetirementRequestId is not null);

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
