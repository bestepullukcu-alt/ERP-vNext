using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers;
using Diten.Platform.Application.Features.RecordCodes.Commands;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.RecordCodes;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Features.RecordCodes;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.RecordCodes.Handlers.CommandHandlers;

/// <summary>
/// MOD-0040-FU02 §4 "Yönetici ucu" — copy the effective document <c>OrgPrefix</c> into the tenant's group-prefix setting.
/// (i) the route tenant exists (checked in the platform context) → (ii) no body → INSIDE <see cref="TenantScope.Begin"/> for
/// the route tenant, one transaction: read the previous setting, (iii) the OrgPrefix matches <c>^[A-Z]{2,5}$</c>, (iv) a
/// locked setting is never changed, write, and the Table C record in the same transaction. Every refusal is recorded in
/// its own short transaction (Table D, <c>Failed</c>); a refused record is logged, never changes the answer.
/// </summary>
public sealed class AlignRecordCodeGroupPrefixHandler
    : IRequestHandler<AlignRecordCodeGroupPrefixCommand, Response<RecordCodeGroupPrefixDto>>
{
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _auditWriter;
    private readonly ITransactionalAuditRefusalWriter _refusalWriter;
    private readonly ITenantRegistryRepository _tenants;
    private readonly IRecordCodeGroupPrefixRepository _groupPrefixes;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IOptions<DocumentCodingOptions> _documentCoding;
    private readonly ILogger<AlignRecordCodeGroupPrefixHandler> _logger;

    public AlignRecordCodeGroupPrefixHandler(
        IPlatformTransactionExecutor transactions,
        ITransactionalAuditOutboxWriter auditWriter,
        ITransactionalAuditRefusalWriter refusalWriter,
        ITenantRegistryRepository tenants,
        IRecordCodeGroupPrefixRepository groupPrefixes,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        IOptions<DocumentCodingOptions> documentCoding,
        ILogger<AlignRecordCodeGroupPrefixHandler> logger)
    {
        _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        _auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
        _refusalWriter = refusalWriter ?? throw new ArgumentNullException(nameof(refusalWriter));
        _tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));
        _groupPrefixes = groupPrefixes ?? throw new ArgumentNullException(nameof(groupPrefixes));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _documentCoding = documentCoding ?? throw new ArgumentNullException(nameof(documentCoding));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Response<RecordCodeGroupPrefixDto>> Handle(
        AlignRecordCodeGroupPrefixCommand command,
        CancellationToken cancellationToken)
    {
        var routeTenantId = command.TenantId;

        // (i) — no record is ever filed under a tenant that does not exist: the platform audit tenant holds it.
        if (routeTenantId == Guid.Empty || await _tenants.GetByIdAsync(routeTenantId, cancellationToken) is null)
        {
            var metadata = new Dictionary<string, object?>
            {
                ["ReasonCode"] = RecordCodeReasonCodes.TenantNotFound,
                ["HttpStatus"] = 404
            };
            if (routeTenantId == Guid.Empty)
            {
                metadata["RouteTenantId"] = Guid.Empty.ToString("D");
            }

            await RecordRefusalAsync(AuditTenantIds.PlatformSystemTenantId, routeTenantId == Guid.Empty ? null : routeTenantId,
                AuditOperation.Update, null, metadata, cancellationToken);
            return Response<RecordCodeGroupPrefixDto>.Fail("The tenant was not found.", 404, RecordCodeReasonCodes.TenantNotFound);
        }

        var orgPrefix = _documentCoding.Value.OrgPrefix;
        Response<RecordCodeGroupPrefixDto> answer;
        (AuditOperation Operation, Guid? SettingId, Dictionary<string, object?> Metadata)? refusal = null;
        using (TenantScope.Begin(_tenantContext, routeTenantId))
        {
            // (ii) — bodyless.
            if (command.BodyPresent)
            {
                var current = await _groupPrefixes.GetAsync(routeTenantId, cancellationToken);
                refusal = (current is null ? AuditOperation.Create : AuditOperation.Update, current?.Id,
                    new Dictionary<string, object?> { ["ReasonCode"] = RecordCodeReasonCodes.RequestInvalid, ["HttpStatus"] = 400 });
                answer = Response<RecordCodeGroupPrefixDto>.Fail(
                    "The alignment takes no body.", 400, RecordCodeReasonCodes.RequestInvalid);
            }
            else
            {
                try
                {
                    var outcome = await _transactions.ExecuteAsync(
                        (session, token) => AlignAsync(session, routeTenantId, orgPrefix, token),
                        cancellationToken);
                    refusal = outcome.Refusal;
                    answer = outcome.Answer;
                }
                catch (Exception exception) when (exception is PlatformTransactionUnavailableException or RecordCodeAuditOutboxInvariantException)
                {
                    _logger.LogWarning(exception, "Record-code group-prefix alignment for tenant {TenantId} could not complete.", routeTenantId);
                    refusal = (AuditOperation.Update, null, new Dictionary<string, object?>
                    {
                        ["ReasonCode"] = RecordCodeReasonCodes.Unavailable,
                        ["HttpStatus"] = 503
                    });
                    answer = Response<RecordCodeGroupPrefixDto>.Fail(
                        "The alignment could not be completed.", 503, RecordCodeReasonCodes.Unavailable);
                }
            }
        }

        if (refusal is { } refused)
        {
            await RecordRefusalAsync(routeTenantId, routeTenantId, refused.Operation, refused.SettingId, refused.Metadata, cancellationToken);
        }

        return answer;
    }

    private async Task<AlignOutcome> AlignAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        string orgPrefix,
        CancellationToken cancellationToken)
    {
        var before = await _groupPrefixes.GetAsync(session, tenantId, cancellationToken);

        // (iii) — the source value itself must be a prefix; nothing is written otherwise.
        if (!RecordCodeFormat.IsValidPrefix(orgPrefix))
        {
            return Refused(before, 409, RecordCodeReasonCodes.GroupPrefixSourceInvalid,
                new Dictionary<string, object?> { ["EffectiveOrgPrefix"] = orgPrefix });
        }

        // (iv) — a locked setting holds its value.
        if (before?.LockedAtUtc is not null && !string.Equals(before.GroupPrefix, orgPrefix, StringComparison.Ordinal))
        {
            return Refused(before, 409, RecordCodeReasonCodes.GroupPrefixLocked, LockedMetadata(before, orgPrefix));
        }

        var alignedBy = _currentUser.UserId != Guid.Empty ? _currentUser.UserId.ToString("D") : "platform-administrator";
        var alignment = await _groupPrefixes.AlignIfUnlockedAsync(
            session, tenantId, orgPrefix, alignedBy, DateTime.UtcNow, cancellationToken);
        switch (alignment)
        {
            case RecordCodeGroupPrefixAlignment.Unchanged:
                return new AlignOutcome(Response<RecordCodeGroupPrefixDto>.Success(Dto(before!, orgPrefix)), null);
            case RecordCodeGroupPrefixAlignment.Locked:
                return Refused(before, 409, RecordCodeReasonCodes.GroupPrefixLocked, LockedMetadata(before, orgPrefix));
        }

        var after = await _groupPrefixes.GetAsync(session, tenantId, cancellationToken)
            ?? throw new RecordCodeAuditOutboxInvariantException();
        if (!await RecordCodeAuditIntent.EnqueueGroupPrefixAlignedAsync(
                _auditWriter, session, tenantId, after.Id, before, after.GroupPrefix, after.LockedAtUtc, cancellationToken))
        {
            throw new RecordCodeAuditOutboxInvariantException();
        }

        return new AlignOutcome(Response<RecordCodeGroupPrefixDto>.Success(Dto(after, orgPrefix)), null);
    }

    private static AlignOutcome Refused(
        RecordCodeGroupPrefix? before,
        int statusCode,
        string reasonCode,
        Dictionary<string, object?> extra)
    {
        var metadata = new Dictionary<string, object?> { ["ReasonCode"] = reasonCode, ["HttpStatus"] = statusCode };
        foreach (var pair in extra)
        {
            metadata[pair.Key] = pair.Value;
        }

        return new AlignOutcome(
            Response<RecordCodeGroupPrefixDto>.Fail($"The group prefix was not aligned: {reasonCode}.", statusCode, reasonCode),
            (before is null ? AuditOperation.Create : AuditOperation.Update, before?.Id, metadata));
    }

    private static Dictionary<string, object?> LockedMetadata(RecordCodeGroupPrefix? before, string orgPrefix) => new()
    {
        ["LockedGroupPrefix"] = before?.GroupPrefix,
        ["EffectiveOrgPrefix"] = orgPrefix
    };

    private static RecordCodeGroupPrefixDto Dto(RecordCodeGroupPrefix setting, string orgPrefix) =>
        new(setting.TenantId, setting.GroupPrefix, setting.AlignedAtUtc, setting.LockedAtUtc, orgPrefix);

    private async Task RecordRefusalAsync(
        Guid recordTenantId,
        Guid? targetTenantId,
        AuditOperation operation,
        Guid? settingId,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Record-code group-prefix alignment refused: {ReasonCode}; tenant {TenantId}.",
            metadata.TryGetValue("ReasonCode", out var reason) ? reason : null, targetTenantId);
        try
        {
            if (!await RecordCodeAuditIntent.EnqueueGroupPrefixAlignRefusalAsync(
                    _refusalWriter, _transactions, recordTenantId, targetTenantId, operation, settingId, metadata, cancellationToken))
            {
                _logger.LogWarning("Record-code alignment refusal record was not inserted.");
            }
        }
        catch (Exception exception) when (exception is TransactionOwnedAuditRefusedException or PlatformTransactionUnavailableException)
        {
            _logger.LogWarning(exception, "Record-code alignment refusal record could not be written; the answer stands.");
        }
    }

    private sealed record AlignOutcome(
        Response<RecordCodeGroupPrefixDto> Answer,
        (AuditOperation Operation, Guid? SettingId, Dictionary<string, object?> Metadata)? Refusal);
}
