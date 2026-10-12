using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Persistence.Features.Returns;
// Read-only inspection of Pending local events. No publisher or worker registration.
public sealed class ReturnOutboxStore(IMongoDatabase database)
{
    public async Task<IReadOnlyList<BsonDocument>> PendingAsync(ReturnScope scope, CancellationToken ct)
    {
        scope.EnsureTrusted();
        return await database.GetCollection<BsonDocument>("returns_outbox").Find(new BsonDocument
        {
            { "TenantId", scope.TenantId.ToString() },
            { "LegalEntityId", scope.LegalEntityId.ToString() },
            { "Status", "Pending" }
        }).ToListAsync(ct);
    }
}
