using System.Globalization;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Entities.RecordCodes;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.RecordCodes;

/// <summary>
/// MOD-0040-FU02 §21.3 — the record-code audit records, said as INTENTS on the in-transaction door
/// (<c>CanonicalTransactionalAuditOutboxWriter</c>): the canonical payload and the actor are built there. One method per
/// table — A (allocation), B (allocation refusal), C (group-prefix alignment), D (alignment refusal). Public because the
/// trusted executor in the API layer writes the platform-actor refusal too (pack §8.5 (a)). No outbox key ever derives
/// from the client's idempotency key: an allocation's from the allocation id, a refusal's from a new Guid.
/// </summary>
public static class RecordCodeAuditIntent
{
    public const string SourceModule = "MOD-0040-FU02";
    public const string AllocateRequestType = "AllocateRecordCodeCommand";
    public const string AllocationEntityType = "RecordCodeAllocation";
    public const string AllocatedEventName = "platform.record-code.allocated";
    public const string AllocationRefusedEventName = "platform.record-code.allocation-refused";

    public const string AlignRequestType = "AlignRecordCodeGroupPrefixCommand";
    public const string GroupPrefixEntityType = "RecordCodeGroupPrefix";
    public const string GroupPrefixAlignedEventName = "platform.record-code.group-prefix.aligned";
    public const string GroupPrefixAlignRefusedEventName = "platform.record-code.group-prefix.align-refused";

    /// <summary>Table A — a new allocation, in the allocation's own transaction.</summary>
    public static Task<bool> EnqueueAsync(
        ITransactionalAuditOutboxWriter writer,
        IPlatformTransactionSession session,
        RecordCodeAllocation allocation,
        string callerService,
        bool groupPrefixLockedNow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(allocation);
        return writer.TryEnqueueAsync(session, new AuditOutboxWriteRequest
        {
            TenantId = allocation.TenantId,
            CorrelationId = Guid.NewGuid(),
            IdempotencyKey = $"record-code:allocated:{allocation.Id:N}",
            RequestType = AllocateRequestType,
            Operation = AuditOperation.Create,
            EntityType = AllocationEntityType,
            EntityId = allocation.Id,
            Intent = new TransactionOwnedAuditIntent
            {
                Category = AuditCategory.PlatformConfiguration,
                TargetTenantId = allocation.TenantId,
                SourceService = callerService,
                SourceModule = SourceModule,
                AfterState = new Dictionary<string, object?>
                {
                    ["Code"] = allocation.Code,
                    ["Sequence"] = allocation.Sequence,
                    ["RecordType"] = allocation.RecordType,
                    ["PrefixScope"] = allocation.PrefixScope.ToString(),
                    ["Prefix"] = allocation.Prefix,
                    ["LegalEntityId"] = allocation.LegalEntityId?.ToString("D")
                },
                Metadata = new Dictionary<string, object?>
                {
                    ["EventName"] = AllocatedEventName,
                    ["IdempotencyKey"] = allocation.IdempotencyKey,
                    ["GroupPrefixLockedNow"] = groupPrefixLockedNow
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Table B — a refused allocation, in its OWN short transaction (never the business transaction). Body keys are written
    /// only when the body was parsed (steps 6–13), <c>Prefix</c> only when a prefix was resolved; no code, no sequence.
    /// </summary>
    public static Task<bool> EnqueueRefusalAsync(
        ITransactionalAuditRefusalWriter writer,
        IPlatformTransactionExecutor transactions,
        RecordCodeAllocationRefusal refusal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(refusal);

        var metadata = new Dictionary<string, object?>
        {
            ["EventName"] = AllocationRefusedEventName,
            ["ReasonCode"] = refusal.ReasonCode,
            ["EvaluationStep"] = refusal.EvaluationStep
        };
        if (refusal.Body is { } body)
        {
            metadata["IdempotencyKey"] = body.IdempotencyKey;
            metadata["RecordType"] = body.RecordType;
            metadata["PrefixScope"] = body.PrefixScope.ToString();
            if (body.LegalEntityId is { } legalEntityId)
            {
                metadata["LegalEntityId"] = legalEntityId.ToString("D");
            }
        }

        if (refusal.Prefix is not null)
        {
            metadata["Prefix"] = refusal.Prefix;
        }

        var request = new AuditOutboxWriteRequest
        {
            TenantId = refusal.RecordTenantId,
            CorrelationId = Guid.NewGuid(),
            IdempotencyKey = $"record-code:refused:{Guid.NewGuid():N}",
            RequestType = AllocateRequestType,
            Operation = AuditOperation.Create,
            EntityType = AllocationEntityType,
            EntityId = null,
            Intent = new TransactionOwnedAuditIntent
            {
                Category = AuditCategory.PlatformConfiguration,
                TargetTenantId = refusal.TargetTenantId,
                SourceService = refusal.SourceService,
                SourceModule = SourceModule,
                Metadata = metadata
            }
        };
        return transactions.ExecuteAsync(
            (session, token) => writer.TryEnqueueRefusalAsync(session, request, refusal.Outcome, token),
            cancellationToken);
    }

    /// <summary>
    /// Table C — a group-prefix alignment that changed the setting, in the setting write's transaction. The first alignment
    /// is a Create with no before-state; every later one is an Update whose before-state names both keys.
    /// </summary>
    public static Task<bool> EnqueueGroupPrefixAlignedAsync(
        ITransactionalAuditOutboxWriter writer,
        IPlatformTransactionSession session,
        Guid tenantId,
        Guid settingId,
        RecordCodeGroupPrefix? before,
        string afterGroupPrefix,
        DateTime? afterLockedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return writer.TryEnqueueAsync(session, new AuditOutboxWriteRequest
        {
            TenantId = tenantId,
            CorrelationId = Guid.NewGuid(),
            IdempotencyKey = $"record-code:group-prefix-aligned:{Guid.NewGuid():N}",
            RequestType = AlignRequestType,
            Operation = before is null ? AuditOperation.Create : AuditOperation.Update,
            EntityType = GroupPrefixEntityType,
            EntityId = settingId,
            Intent = new TransactionOwnedAuditIntent
            {
                Category = AuditCategory.PlatformConfiguration,
                TargetTenantId = tenantId,
                SourceModule = SourceModule,
                BeforeState = before is null
                    ? null
                    : new Dictionary<string, object?>
                    {
                        ["GroupPrefix"] = before.GroupPrefix,
                        ["LockedAtUtc"] = Iso(before.LockedAtUtc)
                    },
                AfterState = new Dictionary<string, object?>
                {
                    ["GroupPrefix"] = afterGroupPrefix,
                    ["LockedAtUtc"] = Iso(afterLockedAtUtc)
                },
                Metadata = new Dictionary<string, object?> { ["EventName"] = GroupPrefixAlignedEventName }
            }
        }, cancellationToken);
    }

    /// <summary>Table D — a refused alignment, in its OWN short transaction; always <c>Failed</c>.</summary>
    public static Task<bool> EnqueueGroupPrefixAlignRefusalAsync(
        ITransactionalAuditRefusalWriter writer,
        IPlatformTransactionExecutor transactions,
        Guid recordTenantId,
        Guid? targetTenantId,
        AuditOperation operation,
        Guid? settingId,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(transactions);
        var values = new Dictionary<string, object?> { ["EventName"] = GroupPrefixAlignRefusedEventName };
        foreach (var pair in metadata)
        {
            values[pair.Key] = pair.Value;
        }

        var request = new AuditOutboxWriteRequest
        {
            TenantId = recordTenantId,
            CorrelationId = Guid.NewGuid(),
            IdempotencyKey = $"record-code:group-prefix-align-refused:{Guid.NewGuid():N}",
            RequestType = AlignRequestType,
            Operation = operation,
            EntityType = GroupPrefixEntityType,
            EntityId = settingId,
            Intent = new TransactionOwnedAuditIntent
            {
                Category = AuditCategory.PlatformConfiguration,
                TargetTenantId = targetTenantId,
                SourceModule = SourceModule,
                Metadata = values
            }
        };
        return transactions.ExecuteAsync(
            (session, token) => writer.TryEnqueueRefusalAsync(session, request, AuditOutcome.Failed, token),
            cancellationToken);
    }

    private static string? Iso(DateTime? value) =>
        value is { } utc ? DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture) : null;
}

/// <summary>
/// One refused allocation as Table B records it. <see cref="RecordTenantId"/> is where the record is filed (the token
/// tenant; the platform audit tenant for a platform actor or an absent tenant); <see cref="Body"/> is null before step 6.
/// </summary>
public sealed record RecordCodeAllocationRefusal(
    Guid RecordTenantId,
    Guid? TargetTenantId,
    string? SourceService,
    AuditOutcome Outcome,
    string ReasonCode,
    int EvaluationStep,
    AllocateRecordCodeRequest? Body,
    string? Prefix);
