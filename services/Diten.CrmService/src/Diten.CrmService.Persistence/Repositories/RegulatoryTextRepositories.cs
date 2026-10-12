using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// WP-KP-5a — shared persistence of the two regulatory texts: one collection per kind, tenant scoped, soft-delete aware,
/// no delete, single-document version-checked writes. Nothing sorts or indexes on a DateTimeOffset (parallel-arrays
/// trap). The "one active per key" rule is enforced in the database too (<see cref="SafetyTextRepository.ActiveKeyIndex"/>
/// / <see cref="CountryLegalProfileRepository.ActiveKeyIndex"/>; equality partial filters, never <c>$ne</c>).
/// </summary>
public abstract class RegulatoryTextRepository<T> : IRegulatoryTextRepository<T> where T : RegulatoryText
{
    private readonly IMongoCollection<T> _collection;

    protected RegulatoryTextRepository(IMongoDatabase database, string collectionName)
        => _collection = database.GetCollection<T>(collectionName);

    public static FilterDefinition<T> TenantFilter(Guid tenantId)
        => Builders<T>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    /// <summary>The partial filter of the "one active per key" unique index: active, not deleted.</summary>
    public static FilterDefinition<T> ActiveFilter()
        => Builders<T>.Filter.And(
            Builders<T>.Filter.Eq(x => x.Status, RegulatoryTextStatuses.Active),
            Builders<T>.Filter.Eq(x => x.IsDeleted, false));

    /// <summary>At most one OPEN (draft / in-review) version per key: unique (tenant, OpenKey) over the rows whose derived
    /// <see cref="RegulatoryText.OpenKey"/> is a string — a <c>$type</c> partial filter, never <c>$ne</c>.</summary>
    public static CreateIndexModel<T> OpenKeyIndex(string name)
        => new(
            Builders<T>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OpenKey),
            new CreateIndexOptions<T>
            {
                Unique = true,
                Name = name,
                PartialFilterExpression = Builders<T>.Filter.And(
                    Builders<T>.Filter.Type(x => x.OpenKey, BsonType.String),
                    Builders<T>.Filter.Eq(x => x.IsDeleted, false))
            });

    public async Task<T?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(TenantFilter(tenantId) & Builders<T>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _collection.Find(TenantFilter(tenantId)).ToListAsync(cancellationToken);

    public async Task InsertAsync(T entity, CancellationToken cancellationToken)
    {
        try
        {
            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // WP-KP-5a-FIX-1 — a second open version of the key (ux_*_open_key) raced the pre-check: say so, not 500.
            throw new RegulatoryTextKeyConflictException("The key already has an open version (concurrent write).", ex);
        }
    }

    public async Task<bool> ReplaceAsync(T entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        try
        {
            var result = await _collection.ReplaceOneAsync(
                Builders<T>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
                entity, cancellationToken: cancellationToken);
            if (result.IsAcknowledged && result.MatchedCount == 1)
            {
                return true;
            }
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // A second active of the same key (the unique index) — a concurrent approval won.
        }

        entity.Version = expectedVersion;
        return false;
    }
}

public sealed class SafetyTextRepository : RegulatoryTextRepository<SafetyText>, ISafetyTextRepository
{
    public const string CollectionName = "safety_texts";
    public const string OpenKeyIndexName = "ux_safety_texts_open_key";

    public SafetyTextRepository(IMongoDatabase database) : base(database, CollectionName)
    {
    }

    /// <summary>At most one active safety text per (tenant, product, country, language).</summary>
    public static CreateIndexModel<SafetyText> ActiveKeyIndex()
        => new(
            Builders<SafetyText>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.GlobalProductId)
                .Ascending(x => x.CountryCode).Ascending(x => x.LanguageCode),
            new CreateIndexOptions<SafetyText>
            {
                Unique = true, Name = "ux_safety_texts_active_key", PartialFilterExpression = ActiveFilter()
            });

    public static CreateIndexModel<SafetyText> LookupIndex()
        => new(
            Builders<SafetyText>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CountryCode).Ascending(x => x.Code),
            new CreateIndexOptions { Name = "ix_safety_texts_tenant_country_code" });
}

public sealed class CountryLegalProfileRepository : RegulatoryTextRepository<CountryLegalProfile>, ICountryLegalProfileRepository
{
    public const string CollectionName = "country_legal_profiles";
    public const string OpenKeyIndexName = "ux_country_legal_profiles_open_key";

    public CountryLegalProfileRepository(IMongoDatabase database) : base(database, CollectionName)
    {
    }

    /// <summary>At most one active legal profile per (tenant, country, language).</summary>
    public static CreateIndexModel<CountryLegalProfile> ActiveKeyIndex()
        => new(
            Builders<CountryLegalProfile>.IndexKeys.Ascending(x => x.TenantId)
                .Ascending(x => x.CountryCode).Ascending(x => x.LanguageCode),
            new CreateIndexOptions<CountryLegalProfile>
            {
                Unique = true, Name = "ux_country_legal_profiles_active_key", PartialFilterExpression = ActiveFilter()
            });
}
