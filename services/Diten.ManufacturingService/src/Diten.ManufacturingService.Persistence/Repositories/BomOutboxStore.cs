using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.ManufacturingService.Application.Features.Boms;
using Diten.ManufacturingService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.ManufacturingService.Persistence.Repositories;

/// <summary>
/// The BOM-version outbox as the shared <see cref="IEventOutboxStore"/>, so the shared
/// <see cref="EventOutboxPublisherProcessor"/> moves it to the broker (the same pattern as the SupplyChain shipment
/// outbox). Rows are inserted ONLY by <see cref="BomHistoryJournal"/>, inside the BOM transaction —
/// <see cref="EnqueueAsync"/> refuses, because an event written outside that transaction could announce a change that
/// was never committed. The claim is a trusted background queue scan across tenants; it is not a user data path.
/// </summary>
public sealed class BomOutboxStore(IMongoDatabase database) : IEventOutboxStore
{
    private readonly IMongoCollection<BomOutboxEvent> _outbox = database.GetCollection<BomOutboxEvent>(BomCollections.Outbox);

    public Task<EventOutboxWriteResult> EnqueueAsync(EventOutboxWriteRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("BOM-version events are written by IBomHistoryJournal.CommitAsync inside the BOM transaction.");

    public async Task<EventOutboxPublishItem?> ClaimForPublishAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset stalePublishingCutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var f = Builders<BomOutboxEvent>.Filter;
        var eligible = (f.Eq(e => e.Status, BomOutboxStatus.Pending) & f.Lte(e => e.NextAttemptAtUtc, nowUtc))
                       | (f.Eq(e => e.Status, BomOutboxStatus.Publishing) & f.Lte(e => e.ClaimedAtUtc, stalePublishingCutoffUtc));
        var claimed = await _outbox.FindOneAndUpdateAsync(
            eligible,
            Builders<BomOutboxEvent>.Update.Set(e => e.Status, BomOutboxStatus.Publishing).Set(e => e.ClaimedAtUtc, nowUtc),
            new FindOneAndUpdateOptions<BomOutboxEvent>
            {
                ReturnDocument = ReturnDocument.After,
                Sort = Builders<BomOutboxEvent>.Sort.Ascending(e => e.OccurredAtUtc).Ascending(e => e.Id)
            },
            cancellationToken);
        if (claimed is null)
        {
            return null;
        }

        return new EventOutboxPublishItem(
            new EventMetadata(
                claimed.Id,
                claimed.EventName,
                claimed.EventVersion,
                claimed.CorrelationId,
                null,
                claimed.TenantId,
                BomVersionEvents.Producer,
                claimed.OccurredAtUtc),
            Encoding.UTF8.GetBytes(claimed.PayloadJson),
            TrustedTransportMetadata.Empty,
            EventOutboxDeliveryStatus.Publishing,
            claimed.AttemptCount,
            claimed.LastError);
    }

    public Task CompletePublishAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        _outbox.UpdateOneAsync(
            Publishing(eventId),
            Builders<BomOutboxEvent>.Update
                .Set(e => e.Status, BomOutboxStatus.Published)
                .Set(e => e.PublishedAtUtc, DateTimeOffset.UtcNow)
                .Set(e => e.LastError, null),
            cancellationToken: cancellationToken);

    public async Task FailPublishAsync(Guid eventId, string error, DateTimeOffset nextAttemptAtUtc, int maxAttempts, CancellationToken cancellationToken = default)
    {
        var row = await _outbox.Find(Publishing(eventId)).FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return;
        }

        var attempts = row.AttemptCount + 1;
        await _outbox.UpdateOneAsync(
            Publishing(eventId) & Builders<BomOutboxEvent>.Filter.Eq(e => e.AttemptCount, row.AttemptCount),
            Builders<BomOutboxEvent>.Update
                .Set(e => e.AttemptCount, attempts)
                .Set(e => e.Status, attempts >= maxAttempts ? BomOutboxStatus.DeadLettered : BomOutboxStatus.Pending)
                .Set(e => e.LastError, error)
                .Set(e => e.NextAttemptAtUtc, nextAttemptAtUtc),
            cancellationToken: cancellationToken);
    }

    public Task DeadLetterPublishAsync(Guid eventId, EventOutboxTerminalFailure failure, CancellationToken cancellationToken = default) =>
        _outbox.UpdateOneAsync(
            Publishing(eventId),
            Builders<BomOutboxEvent>.Update
                .Set(e => e.Status, BomOutboxStatus.DeadLettered)
                .Set(e => e.LastError, failure.ReasonCode)
                .Inc(e => e.AttemptCount, 1),
            cancellationToken: cancellationToken);

    private static FilterDefinition<BomOutboxEvent> Publishing(Guid eventId) =>
        Builders<BomOutboxEvent>.Filter.Eq(e => e.Id, eventId) & Builders<BomOutboxEvent>.Filter.Eq(e => e.Status, BomOutboxStatus.Publishing);
}
