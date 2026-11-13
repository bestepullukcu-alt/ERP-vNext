using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// Publication and invalidation insert these records inside their own Mongo transaction.
// This adapter only claims and delivers already committed records.
public sealed class DemandRevisionOutboxStore(DemandPlanningMongoContext context) : IEventOutboxStore
{
    private const string ClaimIndexName = "ix_mod0188_outbox_undispatched";
    private readonly IMongoCollection<DemandOutboxMessage> _outbox = context.Outbox;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private volatile bool _claimIndexReady;

    public Task<EventOutboxWriteResult> EnqueueAsync(EventOutboxWriteRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Demand revision events must be inserted in the revision transaction.");

    public async Task<EventOutboxPublishItem?> ClaimForPublishAsync(DateTimeOffset nowUtc,
        DateTimeOffset stalePublishingCutoffUtc, CancellationToken cancellationToken = default)
    {
        // Trusted background worker: this is a global queue scan, never a user data query.
        await EnsureClaimIndexAsync(cancellationToken);
        var f = Builders<DemandOutboxMessage>.Filter;
        var pending = (f.Eq(x => x.DeliveryState, "Pending") |
                       f.Exists(nameof(DemandOutboxMessage.DeliveryState), false)) &
                      (f.Eq(x => x.NextAttemptAt, null) |
                       f.Lte(x => x.NextAttemptAt, nowUtc));
        var stale = f.Eq(x => x.DeliveryState, "Publishing") &
                    f.Lte(x => x.ClaimedAt, stalePublishingCutoffUtc);
        var message = await _outbox.FindOneAndUpdateAsync(
            f.Eq(x => x.DispatchedAt, null) & (pending | stale),
            Builders<DemandOutboxMessage>.Update
                .Set(x => x.DeliveryState, "Publishing")
                .Set(x => x.ClaimedAt, nowUtc),
            new FindOneAndUpdateOptions<DemandOutboxMessage>
            {
                ReturnDocument = ReturnDocument.After,
                Hint = new BsonString(ClaimIndexName),
                Sort = Builders<DemandOutboxMessage>.Sort.Ascending(x => x.OccurredAt)
            }, cancellationToken);
        if (message is null) return null;
        return new EventOutboxPublishItem(
            new EventMetadata(message.EventId, message.EventType, 2,
                message.CorrelationId == Guid.Empty ? message.EventId : message.CorrelationId,
                null, message.TenantId, "Diten.PlanningService",
                message.OccurredAt.ToUniversalTime()),
            Encoding.UTF8.GetBytes(message.Payload), TrustedTransportMetadata.Empty,
            EventOutboxDeliveryStatus.Publishing, message.AttemptCount, null);
    }

    private async Task EnsureClaimIndexAsync(CancellationToken cancellationToken)
    {
        if (_claimIndexReady) return;
        await _indexGate.WaitAsync(cancellationToken);
        try
        {
            if (_claimIndexReady) return;
            // The worker must see all tenants. The index narrows to unshipped records;
            // OccurredAt remains the sort. Mongo stores DateTimeOffset as an array, so
            // indexing both dates would reject a dispatched record (parallel arrays).
            var keys = Builders<DemandOutboxMessage>.IndexKeys;
            await _outbox.Indexes.CreateOneAsync(new CreateIndexModel<DemandOutboxMessage>(
                keys.Ascending(x => x.DispatchedAt),
                new CreateIndexOptions { Name = ClaimIndexName }),
                cancellationToken: cancellationToken);
            _claimIndexReady = true;
        }
        finally { _indexGate.Release(); }
    }

    public async Task CompletePublishAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await _outbox.UpdateOneAsync(Publishing(eventId),
            Builders<DemandOutboxMessage>.Update
                .Set(x => x.DeliveryState, "Published")
                .Set(x => x.DispatchedAt, DateTimeOffset.UtcNow)
                .Set(x => x.ClaimedAt, null)
                .Set(x => x.LastError, null), cancellationToken: cancellationToken);
    }

    public async Task FailPublishAsync(Guid eventId, string error,
        DateTimeOffset nextAttemptAtUtc, int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        var current = await _outbox.Find(Publishing(eventId))
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null) return;
        await _outbox.UpdateOneAsync(Publishing(eventId) &
                                     Builders<DemandOutboxMessage>.Filter.Eq(
                                         x => x.AttemptCount, current.AttemptCount),
            Builders<DemandOutboxMessage>.Update
                .Set(x => x.DeliveryState,
                    current.AttemptCount + 1 >= maxAttempts ? "DeadLettered" : "Pending")
                .Set(x => x.NextAttemptAt, nextAttemptAtUtc)
                .Set(x => x.ClaimedAt, null)
                .Set(x => x.LastError, error)
                .Inc(x => x.AttemptCount, 1), cancellationToken: cancellationToken);
    }

    public async Task DeadLetterPublishAsync(Guid eventId,
        EventOutboxTerminalFailure failure, CancellationToken cancellationToken = default)
    {
        await _outbox.UpdateOneAsync(Publishing(eventId),
            Builders<DemandOutboxMessage>.Update
                .Set(x => x.DeliveryState, "DeadLettered")
                .Set(x => x.ClaimedAt, null)
                .Set(x => x.LastError, failure.ReasonCode)
                .Inc(x => x.AttemptCount, 1), cancellationToken: cancellationToken);
    }

    private static FilterDefinition<DemandOutboxMessage> Publishing(Guid eventId) =>
        Builders<DemandOutboxMessage>.Filter.Where(x =>
            x.EventId == eventId && x.DeliveryState == "Publishing" &&
            x.DispatchedAt == null);
}
