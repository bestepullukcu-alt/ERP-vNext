using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU06 — tenant-scoped Mongo repository for the Document Master Register (LOG-0001). No hard delete.

public sealed class DocumentMasterRegisterRepository : TenantRepository<DocumentMasterRegisterEntry>, IDocumentMasterRegisterRepository
{
    public DocumentMasterRegisterRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementMasterRegister) { }

    public new Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) =>
        base.CreateAsync(entry, ct);

    public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string permanentUid, CancellationToken ct = default) =>
        Collection.Find(And(Builders<DocumentMasterRegisterEntry>.Filter.Eq(x => x.PermanentUid, permanentUid))).FirstOrDefaultAsync(ct)!;

    public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string documentCode, CancellationToken ct = default) =>
        Collection.Find(And(Builders<DocumentMasterRegisterEntry>.Filter.Eq(x => x.DocumentCode, documentCode))).FirstOrDefaultAsync(ct)!;

    public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default) =>
        Collection.Find(And(Builders<DocumentMasterRegisterEntry>.Filter.Eq(x => x.ControlledDocumentId, controlledDocumentId))).FirstOrDefaultAsync(ct)!;

    // DCP-005 Phase 2 — indexed $in pushdown for the effectiveness resolver's batch, replacing the interface default's
    // full tenant scan. Tenant scope stays enforced by ExecutionFilter (via And). Behaviour matches the default: only
    // rows whose PermanentUid/DocumentCode is one of the (trimmed, de-duplicated) requested identifiers are returned.
    public async Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetByPermanentUidsAsync(IReadOnlyCollection<string> permanentUids, CancellationToken ct = default)
    {
        var wanted = Normalize(permanentUids);
        if (wanted.Count == 0)
        {
            return [];
        }

        return await Collection.Find(And(Builders<DocumentMasterRegisterEntry>.Filter.In(x => x.PermanentUid, wanted))).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetByDocumentCodesAsync(IReadOnlyCollection<string> documentCodes, CancellationToken ct = default)
    {
        var wanted = Normalize(documentCodes);
        if (wanted.Count == 0)
        {
            return [];
        }

        return await Collection.Find(And(Builders<DocumentMasterRegisterEntry>.Filter.In(x => x.DocumentCode, wanted))).ToListAsync(ct);
    }

    private static List<string> Normalize(IReadOnlyCollection<string>? keys) =>
        keys is null
            ? []
            : keys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct(StringComparer.Ordinal).ToList();

    public async Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default)
    {
        var f = Builders<DocumentMasterRegisterEntry>.Filter;
        var conditions = new List<FilterDefinition<DocumentMasterRegisterEntry>> { ExecutionFilter };

        if (filter.RegisterStatus is { } rs) conditions.Add(f.Eq(x => x.RegisterStatus, rs));
        if (filter.LifecycleStatus is { } ls) conditions.Add(f.Eq(x => x.LifecycleStatus, ls));
        if (filter.Criticality is { } c) conditions.Add(f.Eq(x => x.Criticality, c));
        if (filter.DocumentClass is { } dc) conditions.Add(f.Eq(x => x.DocumentClass, dc));
        if (filter.OwnerCompanyId is { } oc) conditions.Add(f.Eq(x => x.OwnerCompanyId, oc));

        return await Collection.Find(f.And(conditions)).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            And(Builders<DocumentMasterRegisterEntry>.Filter.Eq(x => x.Id, entry.Id)), entry, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    private FilterDefinition<DocumentMasterRegisterEntry> And(FilterDefinition<DocumentMasterRegisterEntry> extra) =>
        Builders<DocumentMasterRegisterEntry>.Filter.And(ExecutionFilter, extra);
}
