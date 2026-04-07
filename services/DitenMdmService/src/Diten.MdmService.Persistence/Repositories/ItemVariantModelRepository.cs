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

    public async Task<ItemVariantModel> CreateAsync(ItemVariantModel entity, CancellationToken cancellationToken = default)
    {
        return await InsertAsync(entity, cancellationToken);
    }

    public async Task<bool> UpdateAsync(ItemVariantModel entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ItemVariantModel>.Filter.And(
            TenantFilter,
            Builders<ItemVariantModel>.Filter.Eq(x => x.Id, entity.Id));

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.TenantId = TenantContext.TenantId;
        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<ItemVariantModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<ItemVariantModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Collection.Find(TenantFilter).SortBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var update = Builders<ItemVariantModel>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);
        await Collection.UpdateOneAsync(
            Builders<ItemVariantModel>.Filter.And(TenantFilter, Builders<ItemVariantModel>.Filter.Eq(x => x.Id, id)),
            update,
            cancellationToken: cancellationToken);
    }

    public async Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return 0;
        }

        var filter = Builders<ItemVariantModel>.Filter.And(
            TenantFilter,
            Builders<ItemVariantModel>.Filter.In(x => x.Id, idList));

        var update = Builders<ItemVariantModel>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ItemVariantModel>.Filter.And(
            TenantFilter,
            Builders<ItemVariantModel>.Filter.Eq(x => x.Code, code));

        if (excludeId.HasValue)
        {
            filter &= Builders<ItemVariantModel>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task ReplaceTemplatesAsync(
        Guid variantModelId,
        IEnumerable<AttributeDefinition> definitions,
        IEnumerable<AttributeTemplate> templates,
        CancellationToken cancellationToken = default)
    {
        var definitionList = definitions.ToList();
        foreach (var definition in definitionList)
        {
            definition.TenantId = TenantContext.TenantId;
            definition.IsDeleted = false;

            var filter = Builders<AttributeDefinition>.Filter.And(
                Builders<AttributeDefinition>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
                Builders<AttributeDefinition>.Filter.Eq(x => x.Code, definition.Code));

            await _attributeDefinitions.ReplaceOneAsync(filter, definition, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        }

        var existingTemplateFilter = Builders<AttributeTemplate>.Filter.And(
            TenantFilterFor<AttributeTemplate>(),
            Builders<AttributeTemplate>.Filter.Eq(x => x.VariantModelId, variantModelId));
        await _attributeTemplates.DeleteManyAsync(existingTemplateFilter, cancellationToken);

        var templateList = templates.ToList();
        if (templateList.Count == 0)
        {
            return;
        }

        foreach (var template in templateList)
        {
            template.Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id;
            template.TenantId = TenantContext.TenantId;
            template.VariantModelId = variantModelId;
        }

        await _attributeTemplates.InsertManyAsync(templateList, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeTemplate>> GetTemplatesAsync(Guid variantModelId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AttributeTemplate>.Filter.And(
            TenantFilterFor<AttributeTemplate>(),
            Builders<AttributeTemplate>.Filter.Eq(x => x.VariantModelId, variantModelId));
        return await _attributeTemplates.Find(filter).SortBy(x => x.SortOrder).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeDefinition>> GetAttributeDefinitionsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return [];
        }

        var filter = Builders<AttributeDefinition>.Filter.And(
            TenantFilterFor<AttributeDefinition>(),
            Builders<AttributeDefinition>.Filter.In(x => x.Id, idList));
        return await _attributeDefinitions.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeDefinition>> GetAllAttributeDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        return await _attributeDefinitions.Find(TenantFilterFor<AttributeDefinition>()).SortBy(x => x.Name).ToListAsync(cancellationToken);
    }

    private FilterDefinition<TEntity> TenantFilterFor<TEntity>() where TEntity : EntityBase
    {
        return Builders<TEntity>.Filter.And(
            Builders<TEntity>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<TEntity>.Filter.Eq(x => x.IsDeleted, false));
    }
}
