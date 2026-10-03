using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductAbbreviationHistoryRepository : IProductAbbreviationHistoryRepository
{
    public const string CollectionName = "mdm_product_abbreviation_history";
    public const string CommandKeyCollectionName = "mdm_product_abbreviation_command_keys";

    private readonly IMongoCollection<ProductAbbreviationHistoryEntry> _collection;
    private readonly IMongoCollection<CommandKeyClaim> _commandKeys;
    private readonly Guid _tenantId;

    public ProductAbbreviationHistoryRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _collection = database.GetCollection<ProductAbbreviationHistoryEntry>(CollectionName);
        _commandKeys = database.GetCollection<CommandKeyClaim>(CommandKeyCollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<bool> AppendIfAbsentAsync(
        ProductAbbreviationHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        entry.TenantId = _tenantId;
        entry.IsDeleted = false;
        entry.CreatedAt = entry.OccurredAtUtc;
        entry.UpdatedAt = entry.OccurredAtUtc;
        entry.Version = 0;
        try
        {
            await _collection.InsertOneAsync(entry, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // FIX1 S3: the replay is THIS entry's row with this key — a row another entry wrote under the same key
            // is not this decision's evidence.
            var existing = await _collection.Find(
                    TenantFilter
                    & Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(x => x.RegisterEntryId, entry.RegisterEntryId)
                    & Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(x => x.IdempotencyKey, entry.IdempotencyKey)
                    & Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(x => x.EventType, entry.EventType))
                .FirstOrDefaultAsync(cancellationToken);
            return existing is not null && existing.EvidenceHash == entry.EvidenceHash;
        }
    }

    /// <summary>
    /// FIX1 S10 — in the order the events happened. <c>DateTimeOffset</c> is stored as a BSON array here (BL-030),
    /// and a server sort on an array orders by its SMALLEST element — the offset, 0 for every row — which leaves the
    /// order to chance. The rows of one entry are few; they are ordered by the instant's ticks, then by id.
    /// </summary>
    public async Task<IReadOnlyList<ProductAbbreviationHistoryEntry>> GetForRegisterEntryAsync(
        Guid registerEntryId,
        CancellationToken cancellationToken = default)
        => (await _collection.Find(
                    TenantFilter & Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(
                        x => x.RegisterEntryId,
                        registerEntryId))
                .Limit(1000)
                .ToListAsync(cancellationToken))
            .OrderBy(x => x.OccurredAtUtc.UtcTicks)
            .ThenBy(x => x.Id)
            .ToList();

    public async Task<string?> ClaimCommandKeyAsync(
        string idempotencyKey,
        Guid registerEntryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var claim = new CommandKeyClaim
        {
            Id = ClaimId(_tenantId, idempotencyKey),
            TenantId = _tenantId,
            IdempotencyKey = idempotencyKey,
            RegisterEntryId = registerEntryId
        };
        try
        {
            await _commandKeys.InsertOneAsync(claim, cancellationToken: cancellationToken);
            return null;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _commandKeys
                .Find(Builders<CommandKeyClaim>.Filter.Eq(x => x.Id, claim.Id))
                .FirstOrDefaultAsync(cancellationToken);
            return existing is not null
                   && existing.TenantId == _tenantId
                   && existing.IdempotencyKey == idempotencyKey
                   && existing.RegisterEntryId == registerEntryId
                ? null
                : "ABBREVIATION_IDEMPOTENCY_KEY_REUSED";
        }
    }

    private static string ClaimId(Guid tenantId, string idempotencyKey)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:N}|{idempotencyKey}")));

    private void EnsureIndexes()
    {
        _collection.Indexes.CreateMany(
        [
            new CreateIndexModel<ProductAbbreviationHistoryEntry>(
                Builders<ProductAbbreviationHistoryEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IdempotencyKey)
                    .Ascending(x => x.EventType),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_mdm_product_abbreviation_history_tenant_event"
                }),
            new CreateIndexModel<ProductAbbreviationHistoryEntry>(
                Builders<ProductAbbreviationHistoryEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.RegisterEntryId)
                    .Ascending(x => x.OccurredAtUtc),
                new CreateIndexOptions { Name = "ix_mdm_product_abbreviation_history_tenant_entry" })
        ]);
        // The claim's _id is the (tenant, key) pair, so the key's single owner is enforced by the primary key itself.
        _commandKeys.Indexes.CreateOne(new CreateIndexModel<CommandKeyClaim>(
            Builders<CommandKeyClaim>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId),
            new CreateIndexOptions { Name = "ix_mdm_product_abbreviation_command_keys_tenant_entry" }));
    }

    private FilterDefinition<ProductAbbreviationHistoryEntry> TenantFilter
        => Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(x => x.TenantId, _tenantId)
           & Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(x => x.IsDeleted, false);

    /// <summary>FIX1 S3 — which register entry an idempotency key belongs to, per tenant. First writer wins.</summary>
    internal sealed class CommandKeyClaim
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public Guid RegisterEntryId { get; set; }
    }
}
