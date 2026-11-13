using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// Publication and invalidation insert these records inside their own Mongo transaction.
// This adapter only claims and delivers already committed records.
public sealed class DemandRevisionOutboxStore(DemandPlanningMongoContext context) : IEventOutboxStore
{
    private readonly IMongoCollection<DemandOutboxMessage> _outbox = context.Outbox;

    public Task<EventOutboxWriteResult> EnqueueAsync(EventOutboxWriteRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Demand revision events must be inserted in the revision transaction.");

    public async Task<EventOutboxPublishItem?> ClaimForPublishAsync(DateTimeOffset nowUtc,
        DateTimeOffset stalePublishingCutoffUtc, CancellationToken cancellationToken = default)
    {
        // Trusted background worker: this is a global queue scan, never a user data query.
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
