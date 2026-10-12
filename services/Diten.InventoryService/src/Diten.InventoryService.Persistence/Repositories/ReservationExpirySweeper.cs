using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// System expiry of due reservations (pack ASSUMPTION-0173-16). Discovers the due (Tenant, LE, SKU) groups with one
/// cross-tenant read, then expires each group in its OWN transaction through <see cref="ReservationPositionOperations"/>
/// scoped to that group's Tenant + LE — the same code path, rules and audit/outbox as the in-transaction expiry.
/// Actor = <see cref="Guid.Empty"/> (system); one correlation id per sweep.
/// </summary>
public sealed class ReservationExpirySweeper : IReservationExpirySweeper
{
    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<InventoryReservation> _reservations;

    public ReservationExpirySweeper(IMongoClient client, IMongoDatabase database)
    {
        _client = client;
        _database = database;
        _reservations = database.GetCollection<InventoryReservation>(InventoryCollections.Reservations);
    }

    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryReservation>.Filter;
        var due = f.Eq(x => x.Status, ReservationStatus.Active) & f.Eq(x => x.IsDeleted, false)
                  & f.Ne(x => x.ExpiresAt, null) & f.Lte(x => x.ExpiresAt, (DateTimeOffset?)now);

        var groups = await _reservations.Aggregate()
            .Match(due)
            .Group(new BsonDocument("_id", new BsonDocument
            {
                { "t", "$" + nameof(InventoryReservation.TenantId) },
                { "le", "$" + nameof(InventoryReservation.LegalEntityId) },
                { "item", "$" + nameof(InventoryReservation.ItemId) },
                { "sku", "$" + nameof(InventoryReservation.SkuId) },
                { "level", "$" + nameof(InventoryReservation.SkuLevel) }
            }))
            .ToListAsync(cancellationToken);

        var correlation = Guid.NewGuid();
        var expired = 0;
        foreach (var group in groups)
        {
            var id = group["_id"].AsBsonDocument;
            var tenant = Guid.Parse(id["t"].AsString);
            var legalEntity = Guid.Parse(id["le"].AsString);
            var sku = new SkuKey(
                Guid.Parse(id["item"].AsString),
                Guid.Parse(id["sku"].AsString),
                Enum.Parse<SkuLevel>(id["level"].AsString));
            var operations = new ReservationPositionOperations(_database, tenant, legalEntity, leSource: null); // system actor: no request, no LE source

            using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
            expired += await session.WithTransactionAsync(
                (s, ct) => operations.ExpireDueAsync(s, sku, correlation, Guid.Empty, now, ct),
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }

        return expired;
    }
}
