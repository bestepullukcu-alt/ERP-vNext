using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.RecordCodes;
using Diten.Platform.Domain.Enums.RecordCodes;
using Diten.Platform.Domain.Features.RecordCodes;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// MOD-0040-FU02 §4 — the tenant predicate shared by the three record-code stores. The tenant is an explicit parameter and
/// the filter is EXACT equality on it; no <c>HybridRepository</c>, no "TenantId null or current". The call is refused —
/// nothing read or written — for an empty tenant, from the platform context, or for a tenant other than the context's.
/// The platform system tenant (…0001) is an ordinary Active tenant here and is not refused.
/// </summary>
internal static class RecordCodeTenantPredicate
{
    public static void Ensure(ITenantContext tenantContext, Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new RecordCodeTenantPredicateViolationException("A record-code store was called with an empty tenant.");
        }

        if (tenantContext.IsPlatformContext)
        {
            throw new RecordCodeTenantPredicateViolationException("A record-code store was called from the platform context.");
        }

        if (tenantContext.TenantId != tenantId)
        {
            throw new RecordCodeTenantPredicateViolationException("A record-code store was called for a tenant other than the current one.");
        }
    }
}

public sealed class RecordCodeCounterRepository : IRecordCodeCounterRepository
{
    /// <summary>The first-creation race: an E11000 on the upsert is retried without upsert (MDM CodeReservationRepository).</summary>
    private const int MaximumAttempts = 3;

    private readonly IMongoCollection<RecordCodeCounter> _collection;
    private readonly ITenantContext _tenantContext;

    public RecordCodeCounterRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _collection = dbContext.GetCollection<RecordCodeCounter>(PlatformCollections.RecordCodeCounters);
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<long> NextAsync(Guid tenantId, string prefix, string recordType, CancellationToken cancellationToken = default)
    {
        RecordCodeTenantPredicate.Ensure(_tenantContext, tenantId);
        var filter = Builders<RecordCodeCounter>.Filter.Eq(x => x.TenantId, tenantId)
            & Builders<RecordCodeCounter>.Filter.Eq(x => x.Prefix, prefix)
            & Builders<RecordCodeCounter>.Filter.Eq(x => x.RecordType, recordType);
        var update = Builders<RecordCodeCounter>.Update
            .Inc(x => x.LastNumber, 1L)
            .SetOnInsert(x => x.Id, Guid.NewGuid())
            .SetOnInsert(x => x.CreatedAt, DateTimeOffset.UtcNow)
            .SetOnInsert(x => x.CreatedBy, "record-code-service")
            .SetOnInsert(x => x.IsDeleted, false)
            .SetOnInsert(x => x.Version, 1);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var counter = await _collection.FindOneAndUpdateAsync(
                    filter,
                    update,
                    new FindOneAndUpdateOptions<RecordCodeCounter>
                    {
                        IsUpsert = attempt == 1,
                        ReturnDocument = ReturnDocument.After
                    },
                    cancellationToken);
                if (counter is not null)
                {
                    return counter.LastNumber;
                }
            }
            catch (MongoCommandException exception) when (exception.Code == 11000 && attempt < MaximumAttempts)
            {
                // Two first allocations raced on the upsert; the loser takes the winner's document on the next attempt.
            }

            if (attempt >= MaximumAttempts)
            {
                throw new PlatformTransactionUnavailableException("The record-code counter could not be advanced.");
            }
        }
    }
}

public sealed class RecordCodeAllocationRepository : IRecordCodeAllocationRepository
{
    public const string IdempotencyIndexName = "ux_record_code_allocations_tenant_caller_key";

    private readonly IPlatformDbContext _dbContext;
    private readonly IMongoCollection<RecordCodeAllocation> _collection;
    private readonly ITenantContext _tenantContext;

    public RecordCodeAllocationRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _collection = dbContext.GetCollection<RecordCodeAllocation>(PlatformCollections.RecordCodeAllocations);
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task InsertAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        RecordCodeAllocation allocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        RecordCodeTenantPredicate.Ensure(_tenantContext, tenantId);
        if (allocation.TenantId != tenantId)
        {
            throw new RecordCodeTenantPredicateViolationException("A record-code allocation was written for a tenant other than its own.");
        }

        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        try
        {
            await _collection.InsertOneAsync(handle, allocation, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (
            exception.WriteError?.Category == ServerErrorCategory.DuplicateKey
            && exception.WriteError.Message.Contains(IdempotencyIndexName, StringComparison.Ordinal))
        {
            throw new RecordCodeIdempotencyConflictException(exception);
        }
    }

    public Task<RecordCodeAllocation?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        string callerService,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        RecordCodeTenantPredicate.Ensure(_tenantContext, tenantId);
        return _collection.Find(ByKey(tenantId, callerService, idempotencyKey)).FirstOrDefaultAsync(cancellationToken)!;
    }

    public Task<RecordCodeAllocation?> FindByIdempotencyKeyAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        string callerService,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        RecordCodeTenantPredicate.Ensure(_tenantContext, tenantId);
        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        return _collection.Find(handle, ByKey(tenantId, callerService, idempotencyKey)).FirstOrDefaultAsync(cancellationToken)!;
    }

    public async Task<string?> FindLatestPrefixForLegalEntityAsync(
        Guid tenantId,
        Guid legalEntityId,
        CancellationToken cancellationToken = default)
    {
        RecordCodeTenantPredicate.Ensure(_tenantContext, tenantId);
        var latest = await _collection
            .Find(Builders<RecordCodeAllocation>.Filter.Eq(x => x.TenantId, tenantId)
                & Builders<RecordCodeAllocation>.Filter.Eq(x => x.LegalEntityId, legalEntityId)
                & Builders<RecordCodeAllocation>.Filter.Eq(x => x.PrefixScope, RecordCodePrefixScope.LegalEntity))
            .SortByDescending(x => x.AllocatedAtUtc)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
        return latest?.Prefix;
    }

    public async Task<bool> ExistsPrefixForOtherLegalEntityAsync(
        Guid tenantId,
        string prefix,
        Guid legalEntityId,
        CancellationToken cancellationToken = default)
    {
        RecordCodeTenantPredicate.Ensure(_tenantContext, tenantId);
        var filter = Builders<RecordCodeAllocation>.Filter.Eq(x => x.TenantId, tenantId)
            & Builders<RecordCodeAllocation>.Filter.Eq(x => x.Prefix, prefix)
            & Builders<RecordCodeAllocation>.Filter.Eq(x => x.PrefixScope, RecordCodePrefixScope.LegalEntity)
            & Builders<RecordCodeAllocation>.Filter.Ne(x => x.LegalEntityId, null)
            & Builders<RecordCodeAllocation>.Filter.Ne(x => x.LegalEntityId, legalEntityId);
        return await _collection.Find(filter).Limit(1).AnyAsync(cancellationToken);
    }

    private static FilterDefinition<RecordCodeAllocation> ByKey(Guid tenantId, string callerService, string idempotencyKey) =>
        Builders<RecordCodeAllocation>.Filter.Eq(x => x.TenantId, tenantId)
        & Builders<RecordCodeAllocation>.Filter.Eq(x => x.CallerService, callerService)
        & Builders<RecordCodeAllocation>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey);
}

public sealed class RecordCodeGroupPrefixRepository : IRecordCodeGroupPrefixRepository
{
    private readonly IPlatformDbContext _dbContext;
    private readonly IMongoCollection<RecordCodeGroupPrefix> _collection;
    private readonly ITenantContext _tenantContext;

    public RecordCodeGroupPrefixRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _collection = dbContext.GetCollection<RecordCodeGroupPrefix>(PlatformCollections.RecordCodeGroupPrefixes);
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public Task<RecordCodeGroupPrefix?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        RecordCodeTenantPredicate.Ensure(_tenantContext, tenantId);
        return _collection.Find(ByTenant(tenantId)).FirstOrDefaultAsync(cancellationToken)!;
    }

    public Task<RecordCodeGroupPrefix?> GetAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        RecordCodeTenantPredicate.Ensure(_tenantContext, tenantId);
        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        return _collection.Find(handle, ByTenant(tenantId)).FirstOrDefaultAsync(cancellationToken)!;
    }

    public async Task<RecordCodeGroupPrefixAlignment> AlignIfUnlockedAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        string groupPrefix,
        string alignedBy,
        DateTime alignedAtUtc,
        CancellationToken cancellationToken = default)
    {
        RecordCodeTenantPredicate.Ensure(_tenantContext, tenantId);
        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        var current = await _collection.Find(handle, ByTenant(tenantId)).FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            await _collection.InsertOneAsync(handle, new RecordCodeGroupPrefix
            {
                TenantId = tenantId,
                GroupPrefix = groupPrefix,
                AlignedAtUtc = alignedAtUtc,
                AlignedBy = alignedBy,
                CreatedBy = alignedBy
            }, cancellationToken: cancellationToken);
            return RecordCodeGroupPrefixAlignment.Inserted;
        }

        if (string.Equals(current.GroupPrefix, groupPrefix, StringComparison.Ordinal))
        {
            return RecordCodeGroupPrefixAlignment.Unchanged;
        }

        if (current.LockedAtUtc is not null)
        {
            return RecordCodeGroupPrefixAlignment.Locked;
        }

        var result = await _collection.UpdateOneAsync(
            handle,
            ByTenant(tenantId) & Builders<RecordCodeGroupPrefix>.Filter.Eq(x => x.LockedAtUtc, null),
            Builders<RecordCodeGroupPrefix>.Update
                .Set(x => x.GroupPrefix, groupPrefix)
                .Set(x => x.AlignedAtUtc, alignedAtUtc)
                .Set(x => x.AlignedBy, alignedBy)
                .Set(x => x.UpdatedAt, alignedAtUtc)
                .Set(x => x.UpdatedBy, alignedBy),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1 ? RecordCodeGroupPrefixAlignment.Updated : RecordCodeGroupPrefixAlignment.Locked;
    }

    public async Task<RecordCodeGroupPrefixLockResult> LockAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        string prefix,
        DateTime lockedAtUtc,
        CancellationToken cancellationToken = default)
    {
        RecordCodeTenantPredicate.Ensure(_tenantContext, tenantId);
        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        var (field, value) = SerializedMember(nameof(RecordCodeGroupPrefix.LockedAtUtc), (DateTime?)lockedAtUtc);
        PipelineDefinition<RecordCodeGroupPrefix, RecordCodeGroupPrefix> pipeline = new BsonDocument[]
        {
            new("$set", new BsonDocument(field, new BsonDocument("$ifNull", new BsonArray
            {
                "$" + field,
                new BsonDocument("$literal", value)
            })))
        };
        var result = await _collection.UpdateOneAsync(
            handle,
            ByTenant(tenantId) & Builders<RecordCodeGroupPrefix>.Filter.Eq(x => x.GroupPrefix, prefix),
            Builders<RecordCodeGroupPrefix>.Update.Pipeline(pipeline),
            cancellationToken: cancellationToken);
        return new RecordCodeGroupPrefixLockResult(result.MatchedCount == 1, result.ModifiedCount == 1);
    }

    private static FilterDefinition<RecordCodeGroupPrefix> ByTenant(Guid tenantId) =>
        Builders<RecordCodeGroupPrefix>.Filter.Eq(x => x.TenantId, tenantId);

    /// <summary>The element name and BSON value the class map writes for <paramref name="memberName"/>.</summary>
    private static (string Field, BsonValue Value) SerializedMember(string memberName, object? value)
    {
        var memberMap = BsonClassMap.LookupClassMap(typeof(RecordCodeGroupPrefix)).GetMemberMap(memberName);
        var document = new BsonDocument();
        using (var writer = new BsonDocumentWriter(document))
        {
            writer.WriteStartDocument();
            writer.WriteName("v");
            memberMap.GetSerializer().Serialize(BsonSerializationContext.CreateRoot(writer), new BsonSerializationArgs(), value);
            writer.WriteEndDocument();
        }

        return (memberMap.ElementName, document["v"]);
    }
}
