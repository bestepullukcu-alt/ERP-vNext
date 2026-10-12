using Diten.SupplyChainService.Domain.Features.Claims;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Diten.SupplyChainService.Persistence.Features.Claims;

// Inspection only. No dispatch, worker, delivery state mutation or financial posting.
public sealed class ClaimOutboxStore(IMongoDatabase database)
{
    public async Task<IReadOnlyList<BsonDocument>> ReadPendingAsync(ClaimScope scope, CancellationToken cancellationToken)
    {
        scope.EnsureTrusted();
        return await database.GetCollection<BsonDocument>("claims_outbox").Find(new BsonDocument
        {
            { "TenantId", scope.TenantId.ToString() }, { "LegalEntityId", scope.LegalEntityId.ToString() }, { "Status", "Pending" }
        }).ToListAsync(cancellationToken);
    }
}
