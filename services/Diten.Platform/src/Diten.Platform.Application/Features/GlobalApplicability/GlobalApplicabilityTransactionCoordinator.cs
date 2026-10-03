using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.GlobalApplicability;

public interface IGlobalApplicabilityTransactionCoordinator
{
    Task<T> ExecuteAsync<T>(GlobalApplicabilityMutationDescriptor descriptor,
        Func<IPlatformTransactionSession, CancellationToken, Task<GlobalApplicabilityMutation<T>>> body,
        CancellationToken cancellationToken = default);

    Task<T> ExecuteBatchAsync<T>(
        Func<IPlatformTransactionSession, CancellationToken, Task<GlobalApplicabilityBatchMutation<T>>> body,
        CancellationToken cancellationToken = default);
}

/// <param name="SystemActor">
/// WP-PLATFORM-AUDIT-INTX-01 — the name of the unattended job allowed to make this change with no signed-in person
/// (the startup plan seed, module self-registration). Null: a person must be named or the change is refused.
/// </param>
/// <param name="AuditMetadata">Extra facts for the audit record's metadata (names and codes — never secrets).</param>
public sealed record GlobalApplicabilityMutationDescriptor(string RequestType,
    AuditOperation AuditOperation, string EntityType, Guid EntityId, string? SystemActor = null,
    IReadOnlyDictionary<string, object?>? AuditMetadata = null);

/// <param name="AuditChange">
/// WP-PLATFORM-AUDIT-INTX-01 FIX1 — what an update changed (field names, scalar values before and after), known only
/// once the body has read the record; written to the audit record's before / after state.
/// </param>
public sealed record GlobalApplicabilityMutation<T>(T Result, bool EffectiveStateChanged,
    Func<IPlatformTransactionSession, ulong, CancellationToken, Task>? WriteProjectionAsync = null,
    GlobalApplicabilityAuditChange? AuditChange = null);

public sealed record GlobalApplicabilityBatchItem(GlobalApplicabilityMutationDescriptor Descriptor,
    Func<IPlatformTransactionSession, ulong, CancellationToken, Task> WriteProjectionAsync,
    GlobalApplicabilityAuditChange? AuditChange = null);

public sealed record GlobalApplicabilityBatchMutation<T>(T Result,
    IReadOnlyList<GlobalApplicabilityBatchItem> EffectiveChanges);

public sealed class GlobalApplicabilityTransactionCoordinator : IGlobalApplicabilityTransactionCoordinator
{
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly IEntitlementStateVersionRepository _versions;
    private readonly ITransactionalIntegrationEventWriter _events;
    private readonly ITransactionalAuditOutboxWriter _audit;
    private readonly Diten.Platform.Common.Observability.ICorrelationContext? _correlation;

    public GlobalApplicabilityTransactionCoordinator(IPlatformTransactionExecutor transactions,
        IEntitlementStateVersionRepository versions, ITransactionalIntegrationEventWriter events,
        ITransactionalAuditOutboxWriter audit, Diten.Platform.Common.Observability.ICorrelationContext? correlation = null)
    {
        _correlation = correlation;
        _transactions = transactions;
        _versions = versions;
        _events = events;
        _audit = audit;
    }

    public Task<T> ExecuteAsync<T>(GlobalApplicabilityMutationDescriptor descriptor,
        Func<IPlatformTransactionSession, CancellationToken, Task<GlobalApplicabilityMutation<T>>> body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        return _transactions.ExecuteAsync(async (session, transactionCt) =>
        {
            var mutation = await body(session, transactionCt);
            if (!mutation.EffectiveStateChanged)
            {
                return mutation.Result;
            }

            if (mutation.WriteProjectionAsync is null)
            {
                throw new InvalidOperationException("An effective global-applicability mutation requires a projection write.");
            }
            await WriteChangeAsync(session, descriptor, mutation.WriteProjectionAsync, mutation.AuditChange, transactionCt);

            return mutation.Result;
        }, cancellationToken);
    }

    public Task<T> ExecuteBatchAsync<T>(
        Func<IPlatformTransactionSession, CancellationToken, Task<GlobalApplicabilityBatchMutation<T>>> body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        return _transactions.ExecuteAsync(async (session, transactionCt) =>
        {
            var mutation = await body(session, transactionCt);
            foreach (var change in mutation.EffectiveChanges)
            {
                await WriteChangeAsync(session, change.Descriptor, change.WriteProjectionAsync, change.AuditChange, transactionCt);
            }
            return mutation.Result;
        }, cancellationToken);
    }

    // The two kinds of global record this coordinator writes. A third kind must be given its category here — an
    // unknown one is refused at the writer's entry (AuditCategory.Unknown) rather than filed under a guess.
    private static AuditCategory CategoryOf(string entityType) => entityType switch
    {
        "ModuleCatalogItem" => AuditCategory.ModuleCatalog,
        "SubscriptionPlan" => AuditCategory.SubscriptionBilling,
        _ => AuditCategory.Unknown
    };

    private static string? SourceModuleOf(string entityType) => entityType switch
    {
        "ModuleCatalogItem" => "module-catalog",
        "SubscriptionPlan" => "subscription-billing",
        _ => null
    };

    private static Dictionary<string, object?> MetadataOf(
        GlobalApplicabilityMutationDescriptor descriptor, ulong version, GlobalApplicabilityAuditChange? auditChange)
    {
        var metadata = new Dictionary<string, object?>(descriptor.AuditMetadata ?? new Dictionary<string, object?>())
        {
            ["GlobalApplicabilityVersion"] = version
        };
        if (auditChange is not null)
        {
            metadata["ChangedFields"] = auditChange.ChangedFields.ToArray();
        }

        return metadata;
    }

    private async Task WriteChangeAsync(IPlatformTransactionSession session,
        GlobalApplicabilityMutationDescriptor descriptor,
        Func<IPlatformTransactionSession, ulong, CancellationToken, Task> writeProjectionAsync,
        GlobalApplicabilityAuditChange? auditChange,
        CancellationToken transactionCt)
    {
            var version = await _versions.IncrementGlobalApplicabilityVersionAsync(session, transactionCt);
            await writeProjectionAsync(session, version, transactionCt);
            var eventId = Guid.NewGuid();
            // INTX FIX2 — one correlation for the event and the audit record (the request's, else one fresh id for both).
            var correlationId = AuditCorrelation.Resolve(_correlation?.CorrelationId, Guid.NewGuid());
            var occurredAtUtc = DateTimeOffset.UtcNow;
            await _events.EnqueueAsync(session,
                new GlobalApplicabilityChangedV1(eventId, occurredAtUtc, correlationId,
                    descriptor.EntityType, descriptor.EntityId, descriptor.AuditOperation.ToString(), version),
                new EventPublishOptions { EventId = eventId, CorrelationId = correlationId,
                    Producer = "Diten.Platform", OccurredAtUtc = occurredAtUtc }, transactionCt);

            var inserted = await _audit.TryEnqueueAsync(session, new AuditOutboxWriteRequest
            {
                TenantId = AuditTenantIds.PlatformSystemTenantId,
                CorrelationId = correlationId,
                IdempotencyKey = $"global-applicability:{descriptor.RequestType}:{eventId:N}",
                RequestType = descriptor.RequestType,
                Operation = descriptor.AuditOperation,
                EntityType = descriptor.EntityType,
                EntityId = descriptor.EntityId,
                // WP-PLATFORM-AUDIT-INTX-01 — an INTENT; the canonical payload is built at the writer's entry.
                Intent = new TransactionOwnedAuditIntent
                {
                    Category = CategoryOf(descriptor.EntityType),
                    TargetTenantId = null, // platform-global: owned by the platform-system tenant
                    BeforeState = auditChange?.Before,
                    AfterState = auditChange?.After,
                    Metadata = MetadataOf(descriptor, version, auditChange),
                    SourceModule = SourceModuleOf(descriptor.EntityType),
                    SystemActor = descriptor.SystemActor
                }
            }, transactionCt);
            if (!inserted)
            {
                throw new InvalidOperationException("Transactional global-applicability audit intent was not inserted.");
            }

    }
}
