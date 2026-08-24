using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductDefinitionRevisionRepository : IProductDefinitionRevisionRepository
{
    private const string CollectionName = "mdm_product_definition_revisions";
    private const string AllocatorCollectionName = "mdm_product_definition_revision_allocators";
    private readonly IMongoCollection<ProductDefinitionRevision> _revisions;
    private readonly IMongoCollection<BsonDocument> _allocators;
    private readonly Guid _tenantId;

    public ProductDefinitionRevisionRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _revisions = database.GetCollection<ProductDefinitionRevision>(CollectionName);
        _allocators = database.GetCollection<BsonDocument>(AllocatorCollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public Task<ProductDefinitionRevision?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _revisions.Find(ActiveFilter & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductDefinitionRevision>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _revisions.Find(ActiveFilter & Builders<ProductDefinitionRevision>.Filter.In(x => x.Id, ids))
            .ToListAsync(cancellationToken);
    }

    public Task<ProductDefinitionRevision?> GetByCreationCommandIdAsync(
        string creationCommandId,
        CancellationToken cancellationToken = default)
        => _revisions.Find(
                TenantFilter & Builders<ProductDefinitionRevision>.Filter.Eq(
                    x => x.CreationCommandId,
                    creationCommandId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FirstGskuPairAllocationResult> AllocateForFirstGskuAsync(
        Guid globalProductId,
        string creationCommandId,
        CancellationToken cancellationToken = default)
    {
        var allocatorId = $"{_tenantId:N}:{globalProductId:N}";
        var nextOrdinal = new BsonDocument("$add", new BsonArray
        {
            new BsonDocument("$ifNull", new BsonArray { "$LastOrdinal", 0 }),
            1
        });
        var allocation = new BsonDocument
        {
            { "CreationCommandId", creationCommandId },
            { "RevisionId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
            { "GskuId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
            { "Ordinal", nextOrdinal },
            { "AllocatedAt", new BsonDateTime(DateTime.UtcNow) }
        };
        var stage = new BsonDocument("$set", new BsonDocument
        {
            { "TenantId", new BsonBinaryData(_tenantId, GuidRepresentation.Standard) },
            { "GlobalProductId", new BsonBinaryData(globalProductId, GuidRepresentation.Standard) },
            { "LastOrdinal", nextOrdinal },
            { "Allocations", new BsonDocument("$concatArrays", new BsonArray
                {
                    new BsonDocument("$ifNull", new BsonArray { "$Allocations", new BsonArray() }),
                    new BsonArray { allocation }
                }) }
        });
        var filter = Builders<BsonDocument>.Filter.Eq("_id", allocatorId)
                     & Builders<BsonDocument>.Filter.Ne("Allocations.CreationCommandId", creationCommandId);

        try
        {
            await _allocators.FindOneAndUpdateAsync(
                filter,
                new PipelineUpdateDefinition<BsonDocument>(new[] { stage }),
                new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
                cancellationToken);
        }
        catch (MongoCommandException exception) when (exception.Code == 11000)
        {
            // A same-parent or same-command contender won. The durable command allocation below is authoritative.
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Same recovery path as the command-level duplicate above.
        }

        var document = await _allocators.Find(Builders<BsonDocument>.Filter.Eq("_id", allocatorId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("REVISION_ORDINAL_CONFLICT");
        var entry = document["Allocations"].AsBsonArray
            .Select(value => value.AsBsonDocument)
            .SingleOrDefault(value => value["CreationCommandId"].AsString == creationCommandId)
            ?? throw new InvalidOperationException("REVISION_ORDINAL_CONFLICT");
        var ordinal = entry["Ordinal"].ToInt32();
        return new FirstGskuPairAllocationResult(
            entry["RevisionId"].AsGuid,
            entry["GskuId"].AsGuid,
            ordinal,
            $"REV-{ordinal:D3}");
    }

    public async Task<ProductDefinitionRevisionCreateResult> CreateForFirstGskuAsync(
        ProductDefinitionRevision revision,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByCreationCommandIdAsync(revision.CreationCommandId, cancellationToken);
        if (existing is not null)
        {
            return SameFacts(existing, revision)
                ? new(true, existing)
                : new(false, existing, "CREATION_COMMAND_PAIR_CONFLICT");
        }

        if (revision.AuditIntents.Count is 0 or > AuditIntentLimits.MaxPerAggregate
            || revision.AuditIntents.Any(x => x.TenantId != _tenantId))
        {
            return new(false, null, "AUDIT_INTENT_CONTRACT_INVALID");
        }

        revision.TenantId = _tenantId;
        revision.CreatedAt = DateTimeOffset.UtcNow;
        revision.UpdatedAt = revision.CreatedAt;
        revision.IsDeleted = false;
        revision.Version = 0;
        try
        {
            await _revisions.InsertOneAsync(revision, cancellationToken: cancellationToken);
            return new(true, revision);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByCreationCommandIdAsync(revision.CreationCommandId, cancellationToken);
            return existing is not null && SameFacts(existing, revision)
                ? new(true, existing)
                : new(false, existing, "CREATION_COMMAND_PAIR_CONFLICT");
        }
    }

    private static bool SameFacts(ProductDefinitionRevision left, ProductDefinitionRevision right)
        => left.Id == right.Id
           && left.GlobalProductId == right.GlobalProductId
           && left.RevisionIdentifier == right.RevisionIdentifier
           && left.CreationCommandId == right.CreationCommandId;

    private void EnsureIndexes()
    {
        _revisions.Indexes.CreateMany([
            new CreateIndexModel<ProductDefinitionRevision>(
                Builders<ProductDefinitionRevision>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.GlobalProductId).Ascending(x => x.RevisionIdentifier),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_product_definition_revisions_tenant_parent_identifier" }),
            new CreateIndexModel<ProductDefinitionRevision>(
                Builders<ProductDefinitionRevision>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.CreationCommandId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_product_definition_revisions_tenant_command" }),
            new CreateIndexModel<ProductDefinitionRevision>(
                Builders<ProductDefinitionRevision>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.GlobalProductId),
                new CreateIndexOptions { Name = "ix_mdm_product_definition_revisions_tenant_parent" })
        ]);
    }

    private FilterDefinition<ProductDefinitionRevision> TenantFilter =>
        Builders<ProductDefinitionRevision>.Filter.Eq(x => x.TenantId, _tenantId);
    private FilterDefinition<ProductDefinitionRevision> ActiveFilter =>
        TenantFilter & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.IsDeleted, false);
}
