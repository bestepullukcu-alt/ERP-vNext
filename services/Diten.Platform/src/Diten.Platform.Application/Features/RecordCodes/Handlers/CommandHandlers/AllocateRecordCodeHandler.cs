using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.RecordCodes;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers;
using Diten.Platform.Application.Features.RecordCodes.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.RecordCodes;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.RecordCodes;
using Diten.Platform.Domain.Features.RecordCodes;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.RecordCodes.Handlers.CommandHandlers;

/// <summary>
/// MOD-0040-FU02 §8.5 steps 4–13 — one record code, in the pack's order:
/// 4 tenant Active → 5 body → 6 catalogue → 7 type allowed to this credential → 8 user permission → 9 idempotency →
/// 10 scope → 11 prefix (Group setting / MDM short code + interim usage lock) → 12 counter <c>$inc</c> (outside the
/// transaction) → 13 ONE transaction: re-read the key, ledger row, Group lock, audit row (Table A).
///
/// <para>Every refusal answers in the <see cref="Response{T}"/> envelope and is recorded in its own short transaction
/// (Table B; best effort — a refusal record that cannot be written is logged and never changes the answer). The audit
/// door's own refusals on the success path (<c>TransactionOwnedAuditRefusedException</c>,
/// <c>TransactionOwnedAuditIntentInvalidException</c>) are NOT caught: the transaction aborts, nothing is issued, and the
/// Platform's exception handler answers them (§8.6).</para>
/// </summary>
public sealed class AllocateRecordCodeHandler : IRequestHandler<AllocateRecordCodeCommand, Response<RecordCodeAllocationResult>>
{
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _auditWriter;
    private readonly ITransactionalAuditRefusalWriter _refusalWriter;
    private readonly ITenantRegistryRepository _tenants;
    private readonly IRecordCodeCounterRepository _counters;
    private readonly IRecordCodeAllocationRepository _allocations;
    private readonly IRecordCodeGroupPrefixRepository _groupPrefixes;
    private readonly ILegalEntityShortCodeReader _shortCodes;
    private readonly IOptions<DocumentCodingOptions> _documentCoding;
    private readonly ILogger<AllocateRecordCodeHandler> _logger;

    public AllocateRecordCodeHandler(
        IPlatformTransactionExecutor transactions,
        ITransactionalAuditOutboxWriter auditWriter,
        ITransactionalAuditRefusalWriter refusalWriter,
        ITenantRegistryRepository tenants,
        IRecordCodeCounterRepository counters,
        IRecordCodeAllocationRepository allocations,
        IRecordCodeGroupPrefixRepository groupPrefixes,
        ILegalEntityShortCodeReader shortCodes,
        IOptions<DocumentCodingOptions> documentCoding,
        ILogger<AllocateRecordCodeHandler> logger)
    {
        _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        _auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
        _refusalWriter = refusalWriter ?? throw new ArgumentNullException(nameof(refusalWriter));
        _tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));
        _counters = counters ?? throw new ArgumentNullException(nameof(counters));
        _allocations = allocations ?? throw new ArgumentNullException(nameof(allocations));
        _groupPrefixes = groupPrefixes ?? throw new ArgumentNullException(nameof(groupPrefixes));
        _shortCodes = shortCodes ?? throw new ArgumentNullException(nameof(shortCodes));
        _documentCoding = documentCoding ?? throw new ArgumentNullException(nameof(documentCoding));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Response<RecordCodeAllocationResult>> Handle(
        AllocateRecordCodeCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = command.TenantId;

        // Step 4 — the tenant is Active (GetTenantStatusQueryHandler's rule; a deleted or absent tenant reads as null).
        var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null || tenant.Status != TenantStatus.Active)
        {
            return await RefuseAsync(command, 403, RecordCodeReasonCodes.TenantNotActive, AuditOutcome.Denied, 4, body: null,
                recordTenantId: tenant is null ? AuditTenantIds.PlatformSystemTenantId : tenantId,
                cancellationToken: cancellationToken);
        }

        // Step 5 — the body, as the API parser read it.
        if (command.Request is not { } request)
        {
            return await RefuseAsync(command, 400, RecordCodeReasonCodes.RequestInvalid, AuditOutcome.Failed, 5, body: null,
                errors: command.RequestErrors, cancellationToken: cancellationToken);
        }

        // Step 6 — the catalogue.
        if (!RecordCodeTypeCatalog.TryGet(request.RecordType, out var definition))
        {
            return await RefuseAsync(command, 422, RecordCodeReasonCodes.RecordTypeUnknown, AuditOutcome.Failed, 6, request,
                cancellationToken: cancellationToken);
        }

        // Step 7 — the type is one this credential may ask for.
        if (!command.AllowedRecordTypes.Contains(request.RecordType))
        {
            return await RefuseAsync(command, 403, RecordCodeReasonCodes.Forbidden, AuditOutcome.Denied, 7, request,
                cancellationToken: cancellationToken);
        }

        // Step 8 — the user holds the type's create permission (evaluated exactly by the executor).
        if (!command.HasRequiredPermission)
        {
            return await RefuseAsync(command, 403, RecordCodeReasonCodes.PermissionMissing, AuditOutcome.Denied, 8, request,
                cancellationToken: cancellationToken);
        }

        // Step 9 — idempotency (tenant, caller, key): a committed allocation answers again, nothing else is touched.
        var existing = await _allocations.FindByIdempotencyKeyAsync(
            tenantId, command.CallerService, request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayAsync(command, request, existing, cancellationToken);
        }

        // Step 10 — scope.
        if (!IsScopeValid(definition, request))
        {
            return await RefuseAsync(command, 422, RecordCodeReasonCodes.ScopeInvalid, AuditOutcome.Failed, 10, request,
                cancellationToken: cancellationToken);
        }

        // Step 11 — the prefix.
        string prefix;
        if (request.PrefixScope == RecordCodePrefixScope.Group)
        {
            var setting = await _groupPrefixes.GetAsync(tenantId, cancellationToken);
            if (setting is null)
            {
                return await RefuseAsync(command, 409, RecordCodeReasonCodes.GroupPrefixMissing, AuditOutcome.Failed, 11, request,
                    cancellationToken: cancellationToken);
            }

            var orgPrefix = _documentCoding.Value.OrgPrefix;
            if (!string.Equals(setting.GroupPrefix, orgPrefix, StringComparison.Ordinal))
            {
                _logger.LogError(
                    "Record-code group prefix {GroupPrefix} of tenant {TenantId} differs from the effective document OrgPrefix {OrgPrefix}; no code issued.",
                    setting.GroupPrefix, tenantId, orgPrefix);
                return await RefuseAsync(command, 409, RecordCodeReasonCodes.GroupPrefixMismatch, AuditOutcome.Failed, 11, request,
                    prefix: orgPrefix, cancellationToken: cancellationToken);
            }

            prefix = orgPrefix;
        }
        else
        {
            var legalEntityId = request.LegalEntityId!.Value;
            var shortCode = await _shortCodes.ReadAsync(tenantId, legalEntityId, cancellationToken);
            switch (shortCode.Outcome)
            {
                case LegalEntityShortCodeOutcome.Found:
                    break;
                case LegalEntityShortCodeOutcome.NotFound:
                    return await RefuseAsync(command, 404, RecordCodeReasonCodes.LegalEntityNotFound, AuditOutcome.Denied, 11, request,
                        cancellationToken: cancellationToken);
                case LegalEntityShortCodeOutcome.ReadPermissionMissing:
                    return await RefuseAsync(command, 403, RecordCodeReasonCodes.LegalEntityReadPermissionMissing, AuditOutcome.Denied, 11,
                        request, cancellationToken: cancellationToken);
                case LegalEntityShortCodeOutcome.NotEligible:
                    return await RefuseAsync(command, 422, RecordCodeReasonCodes.LegalEntityNotEligible, AuditOutcome.Failed, 11, request,
                        cancellationToken: cancellationToken);
                case LegalEntityShortCodeOutcome.ShortCodeInvalid:
                    return await RefuseAsync(command, 409, RecordCodeReasonCodes.LegalEntityShortCodeMissing, AuditOutcome.Failed, 11,
                        request, cancellationToken: cancellationToken);
                default:
                    return await RefuseAsync(command, 503, RecordCodeReasonCodes.LegalEntitySourceUnavailable, AuditOutcome.Failed, 11,
                        request, cancellationToken: cancellationToken);
            }

            prefix = shortCode.Code!;

            // §8.13 — interim usage lock: a short code that changed after use, or moved to another company, issues nothing.
            var latestPrefix = await _allocations.FindLatestPrefixForLegalEntityAsync(tenantId, legalEntityId, cancellationToken);
            if ((latestPrefix is not null && !string.Equals(latestPrefix, prefix, StringComparison.Ordinal))
                || await _allocations.ExistsPrefixForOtherLegalEntityAsync(tenantId, prefix, legalEntityId, cancellationToken))
            {
                _logger.LogError(
                    "Record-code short code {Prefix} of legal entity {LegalEntityId} in tenant {TenantId} changed after use or moved to another company; no code issued.",
                    prefix, legalEntityId, tenantId);
                return await RefuseAsync(command, 409, RecordCodeReasonCodes.LegalEntityShortCodeChanged, AuditOutcome.Failed, 11,
                    request, prefix: prefix, cancellationToken: cancellationToken);
            }
        }

        // Step 12 — the counter, outside the transaction: a number taken by a request that then fails stays a gap (§5.8).
        long sequence;
        try
        {
            sequence = await _counters.NextAsync(tenantId, prefix, request.RecordType, cancellationToken);
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or RecordCodeTenantPredicateViolationException))
        {
            _logger.LogWarning(exception, "Record-code counter for tenant {TenantId} could not be advanced.", tenantId);
            return await RefuseAsync(command, 503, RecordCodeReasonCodes.Unavailable, AuditOutcome.Failed, 12, request,
                prefix: prefix, cancellationToken: cancellationToken);
        }

        // Step 13 — one transaction. Its first act re-reads the key: a winner that committed meanwhile is answered, not
        // written again (§8.6 cases 2 and 3).
        var code = RecordCodeFormat.Format(prefix, request.RecordType, sequence);
        Step13 outcome;
        try
        {
            outcome = await _transactions.ExecuteAsync(
                async (session, token) =>
                {
                    var winner = await _allocations.FindByIdempotencyKeyAsync(
                        session, tenantId, command.CallerService, request.IdempotencyKey, token);
                    if (winner is not null)
                    {
                        return new Step13(winner, Created: false);
                    }

                    var now = DateTime.UtcNow;
                    var allocation = new RecordCodeAllocation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        CallerService = command.CallerService,
                        IdempotencyKey = request.IdempotencyKey,
                        RecordType = request.RecordType,
                        PrefixScope = request.PrefixScope,
                        LegalEntityId = request.PrefixScope == RecordCodePrefixScope.LegalEntity ? request.LegalEntityId : null,
                        Prefix = prefix,
                        Sequence = sequence,
                        Code = code,
                        OnBehalfOfUserId = command.OnBehalfOfUserId,
                        CorrelationId = command.CorrelationId,
                        AllocatedAtUtc = now,
                        CreatedBy = command.CallerService
                    };
                    await _allocations.InsertAsync(session, tenantId, allocation, token);

                    var lockedNow = false;
                    if (request.PrefixScope == RecordCodePrefixScope.Group)
                    {
                        var lockResult = await _groupPrefixes.LockAsync(session, tenantId, prefix, now, token);
                        if (!lockResult.Matched)
                        {
                            throw new RecordCodeGroupPrefixChangedException(prefix);
                        }

                        lockedNow = lockResult.LockedNow;
                    }

                    if (!await RecordCodeAuditIntent.EnqueueAsync(_auditWriter, session, allocation, command.CallerService, lockedNow, token))
                    {
                        throw new RecordCodeAuditOutboxInvariantException();
                    }

                    return new Step13(allocation, Created: true);
                },
                cancellationToken);
        }
        catch (RecordCodeIdempotencyConflictException)
        {
            // §8.6 case 4 — defence: the re-read missed a winner and the ledger's unique key caught it. Answer the winner.
            var winner = await _allocations.FindByIdempotencyKeyAsync(
                tenantId, command.CallerService, request.IdempotencyKey, cancellationToken);
            if (winner is not null)
            {
                return await ReplayAsync(command, request, winner, cancellationToken);
            }

            return await RefuseAsync(command, 503, RecordCodeReasonCodes.Unavailable, AuditOutcome.Failed, 13, request,
                prefix: prefix, cancellationToken: cancellationToken);
        }
        catch (RecordCodeGroupPrefixChangedException)
        {
            _logger.LogError(
                "Record-code group prefix of tenant {TenantId} was re-aligned away from {Prefix} during an allocation; number {Sequence} stays a gap.",
                tenantId, prefix, sequence);
            return await RefuseAsync(command, 409, RecordCodeReasonCodes.GroupPrefixMismatch, AuditOutcome.Failed, 13, request,
                prefix: prefix, cancellationToken: cancellationToken);
        }
        catch (RecordCodeAuditOutboxInvariantException)
        {
            _logger.LogError(
                "Record-code audit outbox row for tenant {TenantId} was not inserted; the allocation was rolled back, number {Sequence} stays a gap.",
                tenantId, sequence);
            return await RefuseAsync(command, 503, RecordCodeReasonCodes.Unavailable, AuditOutcome.Failed, 13, request,
                prefix: prefix, cancellationToken: cancellationToken);
        }
        catch (PlatformTransactionUnavailableException exception)
        {
            _logger.LogWarning(exception,
                "Record-code transaction for tenant {TenantId} could not complete; number {Sequence} stays a gap.", tenantId, sequence);
            return await RefuseAsync(command, 503, RecordCodeReasonCodes.Unavailable, AuditOutcome.Failed, 13, request,
                prefix: prefix, cancellationToken: cancellationToken);
        }

        return outcome.Created
            ? Response<RecordCodeAllocationResult>.Success(
                new RecordCodeAllocationResult(outcome.Allocation.Code, outcome.Allocation.Sequence), 200, command.CorrelationId)
            : await ReplayAsync(command, request, outcome.Allocation, cancellationToken);
    }

    private static bool IsScopeValid(RecordCodeTypeDefinition definition, AllocateRecordCodeRequest request) =>
        request.PrefixScope switch
        {
            // K-11 — defined in the contract, opened in DL-4.
            RecordCodePrefixScope.Platform => false,
            RecordCodePrefixScope.Group => definition.Allows(RecordCodePrefixScope.Group) && request.LegalEntityId is null,
            RecordCodePrefixScope.LegalEntity => definition.Allows(RecordCodePrefixScope.LegalEntity)
                && request.LegalEntityId is { } legalEntityId
                && legalEntityId != Guid.Empty,
            _ => false
        };

    /// <summary>"The same body" = the three stored fields compared one by one, the default scope already applied (§4.2).</summary>
    private Task<Response<RecordCodeAllocationResult>> ReplayAsync(
        AllocateRecordCodeCommand command,
        AllocateRecordCodeRequest request,
        RecordCodeAllocation stored,
        CancellationToken cancellationToken)
    {
        var same = string.Equals(stored.RecordType, request.RecordType, StringComparison.Ordinal)
            && stored.PrefixScope == request.PrefixScope
            && stored.LegalEntityId == request.LegalEntityId;
        return same
            ? Task.FromResult(Response<RecordCodeAllocationResult>.Success(
                new RecordCodeAllocationResult(stored.Code, stored.Sequence), 200, command.CorrelationId))
            : RefuseAsync(command, 400, RecordCodeReasonCodes.IdempotencyKeyReused, AuditOutcome.Failed, 9, request,
                cancellationToken: cancellationToken);
    }

    private async Task<Response<RecordCodeAllocationResult>> RefuseAsync(
        AllocateRecordCodeCommand command,
        int statusCode,
        string reasonCode,
        AuditOutcome outcome,
        int step,
        AllocateRecordCodeRequest? body,
        string? prefix = null,
        Guid? recordTenantId = null,
        IReadOnlyList<string>? errors = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Record-code allocation refused: {ReasonCode} at step {Step}; source service {SourceService}, tenant {TenantId}, correlation {CorrelationId}.",
            reasonCode, step, command.CallerService, command.TenantId, command.CorrelationId);
        try
        {
            var written = await RecordCodeAuditIntent.EnqueueRefusalAsync(
                _refusalWriter,
                _transactions,
                new RecordCodeAllocationRefusal(
                    recordTenantId ?? command.TenantId,
                    command.TenantId,
                    command.CallerService,
                    outcome,
                    reasonCode,
                    step,
                    body,
                    prefix),
                cancellationToken);
            if (!written)
            {
                _logger.LogWarning("Record-code refusal record ({ReasonCode}) was not inserted.", reasonCode);
            }
        }
        catch (Exception exception) when (exception is TransactionOwnedAuditRefusedException or PlatformTransactionUnavailableException)
        {
            _logger.LogWarning(exception, "Record-code refusal record ({ReasonCode}) could not be written; the answer stands.", reasonCode);
        }

        return Response<RecordCodeAllocationResult>.Fail(
            errors is { Count: > 0 } ? errors : [Describe(reasonCode)],
            statusCode,
            reasonCode,
            command.CorrelationId);
    }

    private static string Describe(string reasonCode) => $"The record code was not allocated: {reasonCode}.";

    private sealed record Step13(RecordCodeAllocation Allocation, bool Created);
}
