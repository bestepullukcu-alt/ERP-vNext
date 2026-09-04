using Diten.Platform.Domain.Repositories;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class EntitlementStateVersionRepository : IEntitlementStateVersionRepository
{
    public const string CollectionName = "entitlement_state_versions_v1";
    private const string GlobalApplicabilityKey = "global:catalog-applicability";
    private readonly IPlatformDbContext _dbContext;
    private readonly IMongoCollection<VersionCounterDocument> _collection;

    public EntitlementStateVersionRepository(IPlatformDbContext dbContext)
    {
        _dbContext = dbContext;
        _collection = dbContext.GetCollection<VersionCounterDocument>(CollectionName);
    }

    public Task<ulong> IncrementPhysicalEntitlementVersionAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        string moduleCode,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        var canonicalModule = NormalizeModuleCode(moduleCode);
        return IncrementAsync(session, $"physical:{tenantId:D}:{canonicalModule}", cancellationToken);
    }

    public Task<ulong> IncrementSubscriptionSelectionVersionAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        return IncrementAsync(session, $"subscription:{tenantId:D}", cancellationToken);
    }

    public Task<ulong> IncrementGlobalApplicabilityVersionAsync(
        IPlatformTransactionSession session,
        CancellationToken cancellationToken = default) =>
        IncrementAsync(session, GlobalApplicabilityKey, cancellationToken);

    private async Task<ulong> IncrementAsync(
        IPlatformTransactionSession session,
        string key,
        CancellationToken cancellationToken)
    {
        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        var updated = await _collection.FindOneAndUpdateAsync(
            handle,
            Builders<VersionCounterDocument>.Filter.Eq(x => x.Id, key),
            Builders<VersionCounterDocument>.Update.Inc(x => x.Value, 1L),
            new FindOneAndUpdateOptions<VersionCounterDocument>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);

        if (updated.Value <= 0)
        {
            throw new PlatformTransactionUnavailableException("Entitlement state version wrapped or reset.");
        }

        return checked((ulong)updated.Value);
    }

    private static string NormalizeModuleCode(string moduleCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleCode);
        var normalized = moduleCode.Normalize().ToUpperInvariant();
        if (!string.Equals(moduleCode, moduleCode.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("ModuleCode must not contain leading or trailing whitespace.", nameof(moduleCode));
        }

        return normalized;
    }

    internal sealed class VersionCounterDocument
    {
        [BsonId]
        public required string Id { get; init; }

        public long Value { get; init; }
    }
}
