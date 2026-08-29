using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0167 FU04 StrategyTemplate persistence — ONE collection (<c>strategy_templates</c>) with all four binding lists
/// embedded, so a play and its bindings share one document and one optimistic token. Tenant scoped, soft-delete aware,
/// and with <b>no delete method</b>: closing a template is the soft archive lifecycle, because deleting one would take
/// every past explanation of "why did we run this play?" with it.
/// <para>EffectiveFrom / EffectiveTo / BindingsFrozenAt / ActivatedAt / ArchivedAt are DateTimeOffset and therefore
/// stored as BSON arrays: they are never index keys and never server-side sort keys (the parallel-array trap). Ordering
/// happens in memory. Code uniqueness is enforced in the handler, so no partial index needs a <c>$ne</c> filter — which
/// crash-loops the service at startup.</para>
/// <para>Every write is a single-document operation guarded by the optimistic <see cref="EntityBase.Version"/> token,
/// so no multi-document transaction and no compensation is needed on a standalone dev Mongo.</para>
/// </summary>
public sealed class StrategyTemplateRepository : IStrategyTemplateRepository
{
    public const string CollectionName = "strategy_templates";

    private readonly IMongoCollection<TemplateEntity> _collection;

    public StrategyTemplateRepository(IMongoDatabase database)
        => _collection = database.GetCollection<TemplateEntity>(CollectionName);

    private static FilterDefinition<TemplateEntity> Tenant(Guid tenantId)
        => Builders<TemplateEntity>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<TemplateEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<TemplateEntity>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TemplateEntity>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.TemplateCode).ThenBy(x => x.TemplateVersion).ToList();
    }

    public async Task<IReadOnlyList<TemplateEntity>> ListByLineageAsync(
        Guid tenantId, Guid versionLineageId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<TemplateEntity>.Filter.Eq(x => x.VersionLineageId, versionLineageId))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.TemplateVersion).ToList();
    }

    public async Task<IReadOnlyList<TemplateEntity>> ListByCodeAsync(
        Guid tenantId, string templateCode, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<TemplateEntity>.Filter.Eq(x => x.TemplateCode, templateCode))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.TemplateVersion).ToList();
    }

    public async Task InsertAsync(TemplateEntity entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(
        TemplateEntity entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<TemplateEntity>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }
}
