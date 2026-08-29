using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0151 FU08 import run history. Insert + read only — there is no replace/delete path here, which is what makes
/// the "append-only, never rewritten" pack rule (§7.5b) a property of the code rather than a convention.
///
/// <para>Sorting is done in memory: <c>UploadedAt</c> is a <c>DateTimeOffset</c> and this codebase serializes those as
/// a BSON array, which a Mongo sort cannot combine with another array field ("cannot sort with keys that are parallel
/// arrays"). Run counts per model are small, so an in-memory sort is the safe option.</para>
/// </summary>
public sealed class TerritoryImportRunRepository : ITerritoryImportRunRepository
{
    public const string CollectionName = "territory_import_runs";

    private readonly IMongoCollection<TerritoryImportRun> _collection;

    public TerritoryImportRunRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TerritoryImportRun>(CollectionName);
    }

    private static FilterDefinition<TerritoryImportRun> Scope(Guid tenantId, Guid modelId)
        => Builders<TerritoryImportRun>.Filter.Where(r =>
            r.TenantId == tenantId && r.TerritoryModelId == modelId && !r.IsDeleted);

    public Task InsertAsync(TerritoryImportRun run, CancellationToken cancellationToken)
        => _collection.InsertOneAsync(run, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<TerritoryImportRun>> ListByModelAsync(
        Guid tenantId, Guid modelId, CancellationToken cancellationToken)
    {
        var runs = await _collection.Find(Scope(tenantId, modelId)).ToListAsync(cancellationToken);
        return runs.OrderByDescending(r => r.UploadedAt).ToList();
    }

    public async Task<IReadOnlyList<TerritoryImportRun>> ListByFileHashAsync(
        Guid tenantId, Guid modelId, string fileHash, CancellationToken cancellationToken)
    {
        var filter = Scope(tenantId, modelId)
                     & Builders<TerritoryImportRun>.Filter.Eq(r => r.FileHash, fileHash);
        var runs = await _collection.Find(filter).ToListAsync(cancellationToken);
        return runs.OrderByDescending(r => r.UploadedAt).ToList();
    }
}
