using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Persistence.Mongo;

namespace Diten.PpmService.Persistence.Repositories;

public sealed class AuditIntentRepository(PpmMongoContext context) : IAuditIntentRepository
{
    public async Task AddAsync(AuditIntent intent, CancellationToken cancellationToken)
    {
        if (intent.Id == Guid.Empty ||
            intent.TenantId == Guid.Empty ||
            intent.ActorId == Guid.Empty ||
            intent.EntityId == Guid.Empty ||
            string.IsNullOrWhiteSpace(intent.EntityType) ||
            string.IsNullOrWhiteSpace(intent.Mutation) ||
            intent.OccurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("Audit intent contains invalid required data.");
        }

        var document = new AuditIntentDocument
        {
            Id = intent.Id,
            TenantId = intent.TenantId,
            ActorId = intent.ActorId,
            EntityType = intent.EntityType,
            EntityId = intent.EntityId,
            Mutation = intent.Mutation,
            OccurredAtUtc = intent.OccurredAtUtc
        };

        var session = context.RequireTransaction();
        await context.AuditIntents.InsertOneAsync(
            session,
            document,
            cancellationToken: cancellationToken);
    }
}
