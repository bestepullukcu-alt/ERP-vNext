using MongoDB.Driver;
using MongoDB.Bson;
using Diten.SupplyChainService.Domain.Features.Loads;
namespace Diten.SupplyChainService.Persistence.Features.Loads;
public sealed class LoadOutboxStore(IMongoDatabase db)
{ public async Task<IReadOnlyList<BsonDocument>> PendingAsync(LoadScope scope,CancellationToken ct) {scope.EnsureTrusted();return await db.GetCollection<BsonDocument>("loads_outbox").Find(new BsonDocument{{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},{"Status","Pending"}}).ToListAsync(ct);} }
