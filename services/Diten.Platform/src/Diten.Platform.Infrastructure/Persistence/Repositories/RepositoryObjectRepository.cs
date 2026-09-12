using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// MOD-0262-FU01 — tenant-scoped Mongo repository for document repository objects.
/// <para>
/// Inherits <see cref="TenantRepository{TEntity}"/>, whose <c>ExecutionFilter</c> ANDs the ambient tenant with
/// <c>IsDeleted == false</c>. Every read below composes that filter, so a cross-tenant content id resolves to
/// <c>null</c> and the calling service turns it into a 404 (non-leakage) rather than a 403.
/// </para>
/// <para>⛔ No hard delete and no purge path (DCP-008 AD-6).</para>
/// </summary>
public sealed class RepositoryObjectRepository
    : TenantRepository<RepositoryObject>, IRepositoryObjectRepository
{
    public RepositoryObjectRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentRepositoryObjects) { }

    public new Task<RepositoryObject> CreateAsync(RepositoryObject repositoryObject, CancellationToken ct = default) =>
        base.CreateAsync(repositoryObject, ct);

    public async Task<RepositoryObject?> GetByContentIdAsync(Guid contentId, CancellationToken ct = default) =>
        await Collection
            .Find(Builders<RepositoryObject>.Filter.And(
                ExecutionFilter,
                Builders<RepositoryObject>.Filter.Eq(x => x.ContentId, contentId)))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<RepositoryObject>> GetByOwningItemAsync(Guid owningItemId, CancellationToken ct = default) =>
        await Collection
            .Find(Builders<RepositoryObject>.Filter.And(
                ExecutionFilter,
                Builders<RepositoryObject>.Filter.Eq(x => x.OwningItemId, owningItemId)))
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> MarkCompensatedAsync(Guid contentId, CancellationToken ct = default)
    {
        var result = await Collection.UpdateOneAsync(
            Builders<RepositoryObject>.Filter.And(
                ExecutionFilter,
                Builders<RepositoryObject>.Filter.Eq(x => x.ContentId, contentId)),
            Builders<RepositoryObject>.Update
                .Set(x => x.IsDeleted, true)
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow),
            cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}
