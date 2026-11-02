using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.RecordCodes;

/// <summary>
/// MOD-0040-FU02 §21 — the record-code audit records, said as INTENTS on the in-transaction door
/// (<c>CanonicalTransactionalAuditOutboxWriter</c>): the canonical payload and the actor are built there. Public because the
/// trusted executor in the API layer writes the platform-actor refusal too (pack §8.5 (a)).
/// </summary>
public static class RecordCodeAuditIntent
{
    public const string SourceModule = "MOD-0040-FU02";
    public const string AllocateRequestType = "AllocateRecordCodeCommand";
    public const string AllocationEntityType = "RecordCodeAllocation";
    public const string AllocatedEventName = "platform.record-code.allocated";

    /// <summary>
    /// Table A — a new allocation, in the allocation's transaction. The outbox key derives from the allocation id (a new
    /// Guid per attempt), never from the client's idempotency key (pack §8.6).
    /// </summary>
    public static Task<bool> EnqueueAsync(
        ITransactionalAuditOutboxWriter writer,
        IPlatformTransactionSession session,
        Guid tenantId,
        Guid allocationId,
        IReadOnlyDictionary<string, object?> afterState,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var eventMetadata = new Dictionary<string, object?>(StringComparer.Ordinal) { ["EventName"] = AllocatedEventName };
        foreach (var pair in metadata)
        {
            eventMetadata[pair.Key] = pair.Value;
        }

        return writer.TryEnqueueAsync(session, new AuditOutboxWriteRequest
        {
            TenantId = tenantId,
            CorrelationId = Guid.NewGuid(),
            IdempotencyKey = $"record-code:allocated:{allocationId:N}",
            RequestType = AllocateRequestType,
            Operation = AuditOperation.Create,
            EntityType = AllocationEntityType,
            EntityId = allocationId,
            Intent = new TransactionOwnedAuditIntent
            {
                Category = AuditCategory.PlatformConfiguration,
                TargetTenantId = tenantId,
                AfterState = afterState,
                Metadata = eventMetadata,
                SourceModule = SourceModule
            }
        }, cancellationToken);
    }
}
