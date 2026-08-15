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

public sealed record GlobalApplicabilityMutationDescriptor(string RequestType,
    AuditOperation AuditOperation, string EntityType, Guid EntityId);

public sealed record GlobalApplicabilityMutation<T>(T Result, bool EffectiveStateChanged,
    Func<IPlatformTransactionSession, ulong, CancellationToken, Task>? WriteProjectionAsync = null);

public sealed record GlobalApplicabilityBatchItem(GlobalApplicabilityMutationDescriptor Descriptor,
    Func<IPlatformTransactionSession, ulong, CancellationToken, Task> WriteProjectionAsync);

public sealed record GlobalApplicabilityBatchMutation<T>(T Result,
    IReadOnlyList<GlobalApplicabilityBatchItem> EffectiveChanges);

public sealed class GlobalApplicabilityTransactionCoordinator : IGlobalApplicabilityTransactionCoordinator
{
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly IEntitlementStateVersionRepository _versions;
    private readonly ITransactionalIntegrationEventWriter _events;
    private readonly ITransactionalAuditOutboxWriter _audit;

    public GlobalApplicabilityTransactionCoordinator(IPlatformTransactionExecutor transactions,
        IEntitlementStateVersionRepository versions, ITransactionalIntegrationEventWriter events,
        ITransactionalAuditOutboxWriter audit)
    {
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
            await WriteChangeAsync(session, descriptor, mutation.WriteProjectionAsync, transactionCt);

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
                await WriteChangeAsync(session, change.Descriptor, change.WriteProjectionAsync, transactionCt);
            }
            return mutation.Result;
        }, cancellationToken);
    }

    private async Task WriteChangeAsync(IPlatformTransactionSession session,
        GlobalApplicabilityMutationDescriptor descriptor,
        Func<IPlatformTransactionSession, ulong, CancellationToken, Task> writeProjectionAsync,
        CancellationToken transactionCt)
    {
            var version = await _versions.IncrementGlobalApplicabilityVersionAsync(session, transactionCt);
            await writeProjectionAsync(session, version, transactionCt);
            var eventId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
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
                Payload = new Dictionary<string, object?>
                {
                    ["Outcome"] = "Succeeded",
                    ["GlobalApplicabilityVersion"] = version
                }
            }, transactionCt);
            if (!inserted)
            {
                throw new InvalidOperationException("Transactional global-applicability audit intent was not inserted.");
            }

    }
}
