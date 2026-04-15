using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ItemVariantModelRepository : RepositoryBase<ItemVariantModel>, IItemVariantModelRepository
{
    private readonly IMongoCollection<AttributeDefinition> _attributeDefinitions;
    private readonly IMongoCollection<AttributeTemplate> _attributeTemplates;

    public ItemVariantModelRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "item_variant_models")
    {
        _attributeDefinitions = database.GetCollection<AttributeDefinition>("attribute_definitions");
        _attributeTemplates = database.GetCollection<AttributeTemplate>("attribute_templates");

        var indexKeys = Builders<ItemVariantModel>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.Code)
            .Ascending(x => x.IsDeleted);
        Collection.Indexes.CreateOne(new CreateIndexModel<ItemVariantModel>(indexKeys, new CreateIndexOptions { Unique = true }));
    }

    // Override GetAllAsync to apply default sort by Name
    public override async Task<IReadOnlyList<ItemVariantModel>> GetAllAsync(CancellationToken ct = default)
    {
        return await Collection.Find(TenantFilter).SortBy(x => x.Name).ToListAsync(ct);
    }

    public async Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return 0;

        var filter = Builders<ItemVariantModel>.Filter.And(
            TenantFilter,
            Builders<ItemVariantModel>.Filter.In(x => x.Id, idList));

        var update = Builders<ItemVariantModel>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return (int)result.ModifiedCount;
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filter = Builders<ItemVariantModel>.Filter.And(
            TenantFilter,
            Builders<ItemVariantModel>.Filter.Eq(x => x.Code, code));

        if (excludeId.HasValue)
        {
            filter &= Builders<ItemVariantModel>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task ReplaceTemplatesAsync(
        Guid variantModelId,
        IEnumerable<AttributeDefinition> definitions,
        IEnumerable<AttributeTemplate> templates,
        CancellationToken ct = default)
    {
        var definitionList = definitions.ToList();
        foreach (var definition in definitionList)
        {
            definition.TenantId = TenantContext.TenantId;
            definition.IsDeleted = false;

            var filter = Builders<AttributeDefinition>.Filter.And(
                Builders<AttributeDefinition>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
                Builders<AttributeDefinition>.Filter.Eq(x => x.Code, definition.Code));

            await _attributeDefinitions.ReplaceOneAsync(filter, definition, new ReplaceOptions { IsUpsert = true }, ct);
        }

        var existingTemplateFilter = Builders<AttributeTemplate>.Filter.And(
            TenantFilterFor<AttributeTemplate>(),
            Builders<AttributeTemplate>.Filter.Eq(x => x.VariantModelId, variantModelId));
        await _attributeTemplates.DeleteManyAsync(existingTemplateFilter, ct);

        var templateList = templates.ToList();
        if (templateList.Count == 0) return;

        foreach (var template in templateList)
        {
            template.Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id;
            template.TenantId = TenantContext.TenantId;
            template.VariantModelId = variantModelId;
        }

        await _attributeTemplates.InsertManyAsync(templateList, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<AttributeTemplate>> GetTemplatesAsync(Guid variantModelId, CancellationToken ct = default)
    {
        var filter = Builders<AttributeTemplate>.Filter.And(
            TenantFilterFor<AttributeTemplate>(),
            Builders<AttributeTemplate>.Filter.Eq(x => x.VariantModelId, variantModelId));
        return await _attributeTemplates.Find(filter).SortBy(x => x.SortOrder).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AttributeDefinition>> GetAttributeDefinitionsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return [];

        var filter = Builders<AttributeDefinition>.Filter.And(
            TenantFilterFor<AttributeDefinition>(),
            Builders<AttributeDefinition>.Filter.In(x => x.Id, idList));
        return await _attributeDefinitions.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AttributeDefinition>> GetAllAttributeDefinitionsAsync(CancellationToken ct = default)
    {
        return await _attributeDefinitions.Find(TenantFilterFor<AttributeDefinition>()).SortBy(x => x.Name).ToListAsync(ct);
    }

    private FilterDefinition<TEntity> TenantFilterFor<TEntity>() where TEntity : EntityBase
    {
        return Builders<TEntity>.Filter.And(
            Builders<TEntity>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<TEntity>.Filter.Eq(x => x.IsDeleted, false));
    }
}
