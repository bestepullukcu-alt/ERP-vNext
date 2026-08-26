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

        ProductAbbreviationRegisterWriteResult result;
        ProductAbbreviationHistoryEventType eventType;
        if (entry.ReplacesEntryId is { } formerId)
        {
            if (!command.ExpectedFormerVersion.HasValue)
            {
                return Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                    "ABBREVIATION_FORMER_EXPECTED_VERSION_REQUIRED");
            }

            result = await _register.ReconcileCorrectionApprovalAsync(
                formerId,
                command.ExpectedFormerVersion.Value,
                entry.Id,
                command.ExpectedVersion,
                _actor.CanonicalHumanSubjectId,
                command.IdempotencyKey,
                command.Reason,
                DateTimeOffset.UtcNow,
                cancellationToken);
            eventType = ProductAbbreviationHistoryEventType.CORRECTION_APPROVED;
        }
        else
        {
            result = await _register.TransitionAsync(
                entry.Id,
                command.ExpectedVersion,
                ProductAbbreviationLifecycleStatus.REQUESTED,
                ProductAbbreviationLifecycleStatus.ACTIVE,
                _actor.CanonicalHumanSubjectId,
                command.IdempotencyKey,
                command.Reason,
                DateTimeOffset.UtcNow,
                cancellationToken);
            eventType = ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED;
        }

        if (!result.Succeeded || result.Entry is null)
        {
            return result.ReconciliationRequired
                ? Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                    "ABBREVIATION_CORRECTION_RECONCILIATION_REQUIRED",
                    202)
                : WriteFailure(result);
        }

        var completed = await CompleteTransitionAsync(
            result.Entry,
            eventType,
            ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE,
            command.IdempotencyKey,
            command.Reason,
            cancellationToken);
        if (completed.IsSuccessful && entry.ReplacesEntryId is { } replacedId)
        {
            var former = await _register.GetByIdAsync(replacedId, cancellationToken);
            if (former is null || !await AppendHistoryAsync(
                    former,
                    ProductAbbreviationHistoryEventType.CORRECTION_APPROVED,
                    ProductAbbreviationLifecycleStatus.ACTIVE,
                    ProductAbbreviationLifecycleStatus.RETIRED,
                    command.IdempotencyKey + ":former",
                    command.Reason,
                    cancellationToken))
            {
                return Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                    "ABBREVIATION_EVIDENCE_RECONCILIATION_REQUIRED",
                    202);
            }
        }

        return completed;
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

        var result = await _register.RequestRetirementAsync(
            command.RegisterEntryId,
            command.ExpectedVersion,
            command.IdempotencyKey,
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
        var entryId = DeterministicGuid($"{_actor.TenantId:N}|register|{idempotencyKey}");
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
            return Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>.Fail(
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

    private Task<bool> AppendHistoryAsync(
        ProductAbbreviationRegisterEntry entry,
        ProductAbbreviationHistoryEventType eventType,
        ProductAbbreviationLifecycleStatus? before,
        ProductAbbreviationLifecycleStatus? after,
        string idempotencyKey,
        string? reason,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var evidence = Hash(
            $"{_actor.TenantId:N}|{entry.Id:N}|{entry.GlobalProductId:N}|{entry.NormalizedAbbreviation}|{eventType}|{before}|{after}|{_actor.CanonicalHumanSubjectId}|{idempotencyKey}|{reason}");
        return _history.AppendIfAbsentAsync(
            new ProductAbbreviationHistoryEntry
            {
                Id = DeterministicGuid($"{_actor.TenantId:N}|history|{entry.Id:N}|{eventType}|{idempotencyKey}"),
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
                OccurredAtUtc = timestamp
            },
            cancellationToken);
    }

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
            result.ErrorCode == "ABBREVIATION_NOT_FOUND" ? 404 : 409);

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
