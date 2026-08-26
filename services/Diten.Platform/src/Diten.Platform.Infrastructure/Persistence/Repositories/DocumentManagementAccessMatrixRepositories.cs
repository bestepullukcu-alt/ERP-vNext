using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU04 — tenant-scoped Mongo repository for the generalized document access matrix policy collection.

public sealed class DocumentAccessPolicyRepository : TenantRepository<DocumentAccessPolicyEntry>, IDocumentAccessPolicyRepository
{
    public DocumentAccessPolicyRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementAccessPolicies) { }

    public new Task<DocumentAccessPolicyEntry> CreateAsync(DocumentAccessPolicyEntry entry, CancellationToken ct = default) =>
        base.CreateAsync(entry, ct);

    public async Task<IReadOnlyList<DocumentAccessPolicyEntry>> ListAsync(
        string? targetType,
        string? targetId,
        string? principalType,
        string? principalId,
        string? effect,
        string? action,
        string? status,
        CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<DocumentAccessPolicyEntry>> { ExecutionFilter };

        if (Enum.TryParse<DocumentAccessTargetType>(targetType, true, out var tt))
        {
            filters.Add(Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.TargetType, tt));
        }

        if (!string.IsNullOrWhiteSpace(targetId))
        {
            filters.Add(Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.TargetId, targetId.Trim()));
        }

        if (Enum.TryParse<DocumentAccessPrincipalType>(principalType, true, out var pt))
        {
            filters.Add(Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.PrincipalType, pt));
        }

        if (!string.IsNullOrWhiteSpace(principalId))
        {
            filters.Add(Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.PrincipalId, principalId.Trim()));
        }

        if (Enum.TryParse<DocumentAccessEffect>(effect, true, out var ef))
        {
            filters.Add(Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.Effect, ef));
        }

        if (Enum.TryParse<DocumentAccessMatrixAction>(action, true, out var ac))
        {
            filters.Add(Builders<DocumentAccessPolicyEntry>.Filter.AnyEq(x => x.Actions, ac));
        }

        if (Enum.TryParse<DocumentAccessPolicyStatus>(status, true, out var st))
        {
            filters.Add(Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.Status, st));
        }

        // Sort in memory: DateTimeOffset has no scalar serializer registered, so the driver stores it as an array
        // ([ticks, offset]). A server-side sort by TWO such fields (UpdatedAt + CreatedAt) makes MongoDB fail with
        // "cannot sort with keys that are parallel arrays" once any row has a non-null UpdatedAt. The policy list is
        // tenant-bounded, so an in-memory sort is safe and avoids the limitation.
        var rows = await Collection.Find(Builders<DocumentAccessPolicyEntry>.Filter.And(filters)).ToListAsync(ct);
        return rows
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<DocumentAccessPolicyEntry>> GetByTargetsAsync(
        IReadOnlyList<(DocumentAccessTargetType TargetType, string TargetId)> targets,
        CancellationToken ct = default)
    {
        if (targets is null || targets.Count == 0)
        {
            return [];
        }

        var orFilters = targets
            .Where(t => !string.IsNullOrWhiteSpace(t.TargetId))
            .Select(t => Builders<DocumentAccessPolicyEntry>.Filter.And(
                Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.TargetType, t.TargetType),
                Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.TargetId, t.TargetId.Trim())))
            .ToList();

        if (orFilters.Count == 0)
        {
            return [];
        }

        var filter = And(Builders<DocumentAccessPolicyEntry>.Filter.Or(orFilters));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public Task<DocumentAccessPolicyEntry?> FindDuplicateAsync(
        DocumentAccessTargetType targetType,
        string targetId,
        DocumentAccessPrincipalType principalType,
        string principalId,
        DocumentAccessEffect effect,
        CancellationToken ct = default) =>
        Collection.Find(And(Builders<DocumentAccessPolicyEntry>.Filter.And(
            Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.TargetType, targetType),
            Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.TargetId, targetId.Trim()),
            Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.PrincipalType, principalType),
            Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.PrincipalId, principalId.Trim()),
            Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.Effect, effect)))).FirstOrDefaultAsync(ct)!;

    public async Task<bool> UpdateAsync(DocumentAccessPolicyEntry entry, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            And(Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.Id, entry.Id)), entry, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => base.DeleteAsync(id, ct);

    public async Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return 0;
        }

        var filter = And(Builders<DocumentAccessPolicyEntry>.Filter.In(x => x.Id, ids));
        var update = Builders<DocumentAccessPolicyEntry>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return (int)result.ModifiedCount;
    }

    private FilterDefinition<DocumentAccessPolicyEntry> And(FilterDefinition<DocumentAccessPolicyEntry> extra) =>
        Builders<DocumentAccessPolicyEntry>.Filter.And(ExecutionFilter, extra);
}
