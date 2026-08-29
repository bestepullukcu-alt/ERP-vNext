using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029 (Faz 2a) — tenant-scoped Mongo repository for document-centric variant links. No hard delete.
public sealed class DocumentVariantRepository
    : TenantRepository<DocumentVariant>, IDocumentVariantRepository
{
    public DocumentVariantRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementDocumentVariants) { }

    public new Task<DocumentVariant> CreateAsync(DocumentVariant variant, CancellationToken ct = default) =>
        base.CreateAsync(variant, ct);

    public async Task<DocumentVariant?> GetByVariantRegisterEntryAsync(Guid variantRegisterEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentVariant>.Filter.And(
                ExecutionFilter, Builders<DocumentVariant>.Filter.Eq(x => x.VariantRegisterEntryId, variantRegisterEntryId)))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<DocumentVariant>> GetByParentRegisterEntryAsync(Guid parentRegisterEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentVariant>.Filter.And(
                ExecutionFilter, Builders<DocumentVariant>.Filter.Eq(x => x.ParentRegisterEntryId, parentRegisterEntryId)))
            .SortByDescending(x => x.CreatedAt).ToListAsync(ct);
}
