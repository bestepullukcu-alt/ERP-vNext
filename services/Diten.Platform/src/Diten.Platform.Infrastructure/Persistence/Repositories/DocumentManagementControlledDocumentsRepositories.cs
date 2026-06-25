using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU01 — tenant-scoped Mongo repositories for controlled documents / templates / versions / shares.

public sealed class ControlledDocumentRepository : TenantRepository<ControlledDocument>, IControlledDocumentRepository
{
    public ControlledDocumentRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_controlled_documents") { }

    public new Task<ControlledDocument> CreateAsync(ControlledDocument document, CancellationToken ct = default) =>
        base.CreateAsync(document, ct);

    public Task<ControlledDocument?> GetByDocumentKeyAsync(string documentKey, CancellationToken ct = default) =>
        Collection.Find(And(Builders<ControlledDocument>.Filter.Eq(x => x.DocumentKey, documentKey))).FirstOrDefaultAsync(ct)!;

    public async Task<IReadOnlyList<ControlledDocument>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<ControlledDocument>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<ControlledDocument>.Filter.Eq(x => x.OwnerCompanyId, companyId))).ToListAsync(ct);

    public async Task<IReadOnlyList<ControlledDocument>> GetByCollectionInstanceAsync(Guid collectionInstanceId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<ControlledDocument>.Filter.Eq(x => x.CollectionInstanceId, collectionInstanceId))).ToListAsync(ct);

    public async Task<bool> UpdateAsync(ControlledDocument document, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            And(Builders<ControlledDocument>.Filter.Eq(x => x.Id, document.Id)), document, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    private FilterDefinition<ControlledDocument> And(FilterDefinition<ControlledDocument> extra) =>
        Builders<ControlledDocument>.Filter.And(ExecutionFilter, extra);
}

public sealed class ControlledDocumentVersionRepository : TenantRepository<ControlledDocumentVersion>, IControlledDocumentVersionRepository
{
    public ControlledDocumentVersionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_controlled_document_versions") { }

    public new Task<ControlledDocumentVersion> CreateAsync(ControlledDocumentVersion version, CancellationToken ct = default) =>
        base.CreateAsync(version, ct);

    public async Task<IReadOnlyList<ControlledDocumentVersion>> GetByDocumentAsync(Guid documentId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<ControlledDocumentVersion>.Filter.Eq(x => x.DocumentId, documentId)))
            .SortByDescending(x => x.VersionNumber).ToListAsync(ct);

    public Task<ControlledDocumentVersion?> GetByDocumentAndNumberAsync(Guid documentId, int versionNumber, CancellationToken ct = default) =>
        Collection.Find(And(Builders<ControlledDocumentVersion>.Filter.And(
            Builders<ControlledDocumentVersion>.Filter.Eq(x => x.DocumentId, documentId),
            Builders<ControlledDocumentVersion>.Filter.Eq(x => x.VersionNumber, versionNumber)))).FirstOrDefaultAsync(ct)!;

    public async Task<int> GetMaxVersionNumberAsync(Guid documentId, CancellationToken ct = default)
    {
        var top = await Collection.Find(And(Builders<ControlledDocumentVersion>.Filter.Eq(x => x.DocumentId, documentId)))
            .SortByDescending(x => x.VersionNumber).Limit(1).FirstOrDefaultAsync(ct);
        return top?.VersionNumber ?? 0;
    }

    public async Task SupersedeActiveVersionsAsync(Guid documentId, Guid exceptVersionId, CancellationToken ct = default)
    {
        var filter = And(Builders<ControlledDocumentVersion>.Filter.And(
            Builders<ControlledDocumentVersion>.Filter.Eq(x => x.DocumentId, documentId),
            Builders<ControlledDocumentVersion>.Filter.Ne(x => x.Id, exceptVersionId),
            Builders<ControlledDocumentVersion>.Filter.Eq(x => x.VersionStatus, DocumentVersionStatus.Active)));
        var update = Builders<ControlledDocumentVersion>.Update.Set(x => x.VersionStatus, DocumentVersionStatus.Superseded);
        await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
    }

    private FilterDefinition<ControlledDocumentVersion> And(FilterDefinition<ControlledDocumentVersion> extra) =>
        Builders<ControlledDocumentVersion>.Filter.And(ExecutionFilter, extra);
}

public sealed class TemplateDocumentRepository : TenantRepository<TemplateDocument>, ITemplateDocumentRepository
{
    public TemplateDocumentRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_template_documents") { }

    public new Task<TemplateDocument> CreateAsync(TemplateDocument template, CancellationToken ct = default) =>
        base.CreateAsync(template, ct);

    public Task<TemplateDocument?> GetByTemplateKeyAsync(string templateKey, CancellationToken ct = default) =>
        Collection.Find(And(Builders<TemplateDocument>.Filter.Eq(x => x.TemplateKey, templateKey))).FirstOrDefaultAsync(ct)!;

    public async Task<IReadOnlyList<TemplateDocument>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<TemplateDocument>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<TemplateDocument>.Filter.Eq(x => x.OwnerCompanyId, companyId))).ToListAsync(ct);

    public async Task<IReadOnlyList<TemplateDocument>> GetByCollectionInstanceAsync(Guid collectionInstanceId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<TemplateDocument>.Filter.Eq(x => x.CollectionInstanceId, collectionInstanceId))).ToListAsync(ct);

    public async Task<bool> UpdateAsync(TemplateDocument template, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            And(Builders<TemplateDocument>.Filter.Eq(x => x.Id, template.Id)), template, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    private FilterDefinition<TemplateDocument> And(FilterDefinition<TemplateDocument> extra) =>
        Builders<TemplateDocument>.Filter.And(ExecutionFilter, extra);
}

public sealed class TemplateVersionRepository : TenantRepository<TemplateVersion>, ITemplateVersionRepository
{
    public TemplateVersionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_template_versions") { }

    public new Task<TemplateVersion> CreateAsync(TemplateVersion version, CancellationToken ct = default) =>
        base.CreateAsync(version, ct);

    public async Task<IReadOnlyList<TemplateVersion>> GetByTemplateAsync(Guid templateId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<TemplateVersion>.Filter.Eq(x => x.TemplateId, templateId)))
            .SortByDescending(x => x.VersionNumber).ToListAsync(ct);

    public Task<TemplateVersion?> GetByTemplateAndNumberAsync(Guid templateId, int versionNumber, CancellationToken ct = default) =>
        Collection.Find(And(Builders<TemplateVersion>.Filter.And(
            Builders<TemplateVersion>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<TemplateVersion>.Filter.Eq(x => x.VersionNumber, versionNumber)))).FirstOrDefaultAsync(ct)!;

    public async Task<int> GetMaxVersionNumberAsync(Guid templateId, CancellationToken ct = default)
    {
        var top = await Collection.Find(And(Builders<TemplateVersion>.Filter.Eq(x => x.TemplateId, templateId)))
            .SortByDescending(x => x.VersionNumber).Limit(1).FirstOrDefaultAsync(ct);
        return top?.VersionNumber ?? 0;
    }

    public async Task SupersedeActiveVersionsAsync(Guid templateId, Guid exceptVersionId, CancellationToken ct = default)
    {
        var filter = And(Builders<TemplateVersion>.Filter.And(
            Builders<TemplateVersion>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<TemplateVersion>.Filter.Ne(x => x.Id, exceptVersionId),
            Builders<TemplateVersion>.Filter.Eq(x => x.VersionStatus, DocumentVersionStatus.Active)));
        var update = Builders<TemplateVersion>.Update.Set(x => x.VersionStatus, DocumentVersionStatus.Superseded);
        await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
    }

    private FilterDefinition<TemplateVersion> And(FilterDefinition<TemplateVersion> extra) =>
        Builders<TemplateVersion>.Filter.And(ExecutionFilter, extra);
}

public sealed class FolderDocumentAccessPolicyRepository : TenantRepository<FolderDocumentAccessPolicy>, IFolderDocumentAccessPolicyRepository
{
    public FolderDocumentAccessPolicyRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_folder_document_access_policies") { }

    public async Task<FolderDocumentAccessPolicy> UpsertAsync(FolderDocumentAccessPolicy policy, CancellationToken ct = default)
    {
        var filter = And(Builders<FolderDocumentAccessPolicy>.Filter.And(
            Builders<FolderDocumentAccessPolicy>.Filter.Eq(x => x.CollectionInstanceId, policy.CollectionInstanceId),
            Builders<FolderDocumentAccessPolicy>.Filter.Eq(x => x.TargetType, policy.TargetType),
            Builders<FolderDocumentAccessPolicy>.Filter.Eq(x => x.TargetId, policy.TargetId)));
        await Collection.ReplaceOneAsync(filter, policy, new ReplaceOptions { IsUpsert = true }, ct);
        return policy;
    }

    public async Task<IReadOnlyList<FolderDocumentAccessPolicy>> GetByCollectionInstanceAsync(Guid collectionInstanceId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<FolderDocumentAccessPolicy>.Filter.Eq(x => x.CollectionInstanceId, collectionInstanceId))).ToListAsync(ct);

    public async Task<IReadOnlyList<FolderDocumentAccessPolicy>> GetByCollectionInstanceAndTargetsAsync(
        Guid collectionInstanceId,
        IReadOnlyList<(AccessTargetType TargetType, string TargetId)> targets,
        CancellationToken ct = default)
    {
        var all = await GetByCollectionInstanceAsync(collectionInstanceId, ct);
        var set = targets.Select(t => (t.TargetType, t.TargetId)).ToHashSet();
        return all.Where(p => set.Contains((p.TargetType, p.TargetId))).ToList();
    }

    private FilterDefinition<FolderDocumentAccessPolicy> And(FilterDefinition<FolderDocumentAccessPolicy> extra) =>
        Builders<FolderDocumentAccessPolicy>.Filter.And(ExecutionFilter, extra);
}

public sealed class DocumentShareRecordRepository : TenantRepository<DocumentShareRecord>, IDocumentShareRecordRepository
{
    public DocumentShareRecordRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_document_shares") { }

    public new Task<DocumentShareRecord> CreateAsync(DocumentShareRecord share, CancellationToken ct = default) =>
        base.CreateAsync(share, ct);

    public async Task<IReadOnlyList<DocumentShareRecord>> CreateManyAsync(IReadOnlyList<DocumentShareRecord> shares, CancellationToken ct = default)
    {
        if (shares.Count == 0)
        {
            return [];
        }

        foreach (var share in shares)
        {
            typeof(DocumentShareRecord).GetProperty(nameof(DocumentShareRecord.TenantId))?.SetValue(share, TenantContext.TenantId);
        }

        await Collection.InsertManyAsync(shares, cancellationToken: ct);
        return shares;
    }

    public async Task<IReadOnlyList<DocumentShareRecord>> GetByItemAsync(SharedItemKind itemKind, Guid itemId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<DocumentShareRecord>.Filter.And(
            Builders<DocumentShareRecord>.Filter.Eq(x => x.ItemKind, itemKind),
            Builders<DocumentShareRecord>.Filter.Eq(x => x.ItemId, itemId)))).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentShareRecord>> GetSharesForTargetCompanyAsync(Guid targetCompanyId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<DocumentShareRecord>.Filter.Eq(x => x.TargetCompanyId, targetCompanyId))).ToListAsync(ct);

    public async Task<bool> ExistsAsync(SharedItemKind itemKind, Guid itemId, Guid targetCompanyId, CancellationToken ct = default)
    {
        var count = await Collection.CountDocumentsAsync(And(Builders<DocumentShareRecord>.Filter.And(
            Builders<DocumentShareRecord>.Filter.Eq(x => x.ItemKind, itemKind),
            Builders<DocumentShareRecord>.Filter.Eq(x => x.ItemId, itemId),
            Builders<DocumentShareRecord>.Filter.Eq(x => x.TargetCompanyId, targetCompanyId))), cancellationToken: ct);
        return count > 0;
    }

    private FilterDefinition<DocumentShareRecord> And(FilterDefinition<DocumentShareRecord> extra) =>
        Builders<DocumentShareRecord>.Filter.And(ExecutionFilter, extra);
}

public sealed class FolderShareOperationRepository : TenantRepository<FolderShareOperation>, IFolderShareOperationRepository
{
    public FolderShareOperationRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_folder_share_operations") { }

    public new Task<FolderShareOperation> CreateAsync(FolderShareOperation operation, CancellationToken ct = default) =>
        base.CreateAsync(operation, ct);

    public Task<FolderShareOperation?> GetByOperationIdAsync(Guid operationId, CancellationToken ct = default) =>
        Collection.Find(Builders<FolderShareOperation>.Filter.And(
            ExecutionFilter, Builders<FolderShareOperation>.Filter.Eq(x => x.OperationId, operationId))).FirstOrDefaultAsync(ct)!;
}

public sealed class FolderShareOutcomeRepository : TenantRepository<FolderShareOutcome>, IFolderShareOutcomeRepository
{
    public FolderShareOutcomeRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_folder_share_outcomes") { }

    public async Task<IReadOnlyList<FolderShareOutcome>> CreateManyAsync(IReadOnlyList<FolderShareOutcome> outcomes, CancellationToken ct = default)
    {
        if (outcomes.Count == 0)
        {
            return [];
        }

        foreach (var outcome in outcomes)
        {
            typeof(FolderShareOutcome).GetProperty(nameof(FolderShareOutcome.TenantId))?.SetValue(outcome, TenantContext.TenantId);
        }

        await Collection.InsertManyAsync(outcomes, cancellationToken: ct);
        return outcomes;
    }

    public async Task<IReadOnlyList<FolderShareOutcome>> GetByOperationIdAsync(Guid operationId, CancellationToken ct = default) =>
        await Collection.Find(Builders<FolderShareOutcome>.Filter.And(
            ExecutionFilter, Builders<FolderShareOutcome>.Filter.Eq(x => x.OperationId, operationId)))
            .SortBy(x => x.ItemType).ThenBy(x => x.ItemKey).ToListAsync(ct);
}
