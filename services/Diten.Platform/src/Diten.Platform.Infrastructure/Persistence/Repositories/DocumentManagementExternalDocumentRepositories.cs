using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU14 — tenant-scoped Mongo repositories for the External Document Register, its monitoring checks,
// impact assessments and internal links. No hard delete on any of them. Only governance metadata and reference
// strings are persisted — external document content is never stored.

public sealed class ExternalDocumentRegisterRepository
    : TenantRepository<ExternalDocumentRegisterEntry>, IExternalDocumentRegisterRepository
{
    public ExternalDocumentRegisterRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_external_documents") { }

    public new Task<ExternalDocumentRegisterEntry> CreateAsync(ExternalDocumentRegisterEntry entry, CancellationToken ct = default) =>
        base.CreateAsync(entry, ct);

    public async Task<IReadOnlyList<ExternalDocumentRegisterEntry>> ListAsync(ExternalDocumentListFilter filter, CancellationToken ct = default)
    {
        var b = Builders<ExternalDocumentRegisterEntry>.Filter;
        var conditions = new List<FilterDefinition<ExternalDocumentRegisterEntry>> { ExecutionFilter };

        if (filter.ExternalDocumentStatus is { } status)
        {
            conditions.Add(b.Eq(x => x.ExternalDocumentStatus, status));
        }

        if (filter.SourceStatus is { } sourceStatus)
        {
            conditions.Add(b.Eq(x => x.SourceStatus, sourceStatus));
        }

        if (filter.ExternalDocumentType is { } type)
        {
            conditions.Add(b.Eq(x => x.ExternalDocumentType, type));
        }

        if (filter.ImpactAssessmentStatus is { } impactStatus)
        {
            conditions.Add(b.Eq(x => x.ImpactAssessmentStatus, impactStatus));
        }

        if (filter.MonitoringOwnerUserId is { } owner)
        {
            conditions.Add(b.Eq(x => x.MonitoringOwnerUserId, owner));
        }

        return await Collection.Find(b.And(conditions)).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ExternalDocumentRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(ExternalDocumentRegisterEntry entry, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<ExternalDocumentRegisterEntry>.Filter.And(ExecutionFilter,
                Builders<ExternalDocumentRegisterEntry>.Filter.Eq(x => x.Id, entry.Id)),
            entry, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class ExternalDocumentMonitoringCheckRepository
    : TenantRepository<ExternalDocumentMonitoringCheck>, IExternalDocumentMonitoringCheckRepository
{
    public ExternalDocumentMonitoringCheckRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_external_document_monitoring_checks") { }

    public new Task<ExternalDocumentMonitoringCheck> CreateAsync(ExternalDocumentMonitoringCheck check, CancellationToken ct = default) =>
        base.CreateAsync(check, ct);

    public async Task<IReadOnlyList<ExternalDocumentMonitoringCheck>> GetByExternalDocumentAsync(Guid externalDocumentRegisterEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<ExternalDocumentMonitoringCheck>.Filter.And(
                ExecutionFilter,
                Builders<ExternalDocumentMonitoringCheck>.Filter.Eq(x => x.ExternalDocumentRegisterEntryId, externalDocumentRegisterEntryId)))
            .SortByDescending(x => x.CheckDate).ToListAsync(ct);
}

public sealed class ExternalDocumentImpactAssessmentRepository
    : TenantRepository<ExternalDocumentImpactAssessment>, IExternalDocumentImpactAssessmentRepository
{
    public ExternalDocumentImpactAssessmentRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_external_document_impact_assessments") { }

    public new Task<ExternalDocumentImpactAssessment> CreateAsync(ExternalDocumentImpactAssessment assessment, CancellationToken ct = default) =>
        base.CreateAsync(assessment, ct);

    public async Task<IReadOnlyList<ExternalDocumentImpactAssessment>> GetByExternalDocumentAsync(Guid externalDocumentRegisterEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<ExternalDocumentImpactAssessment>.Filter.And(
                ExecutionFilter,
                Builders<ExternalDocumentImpactAssessment>.Filter.Eq(x => x.ExternalDocumentRegisterEntryId, externalDocumentRegisterEntryId)))
            .SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<ExternalDocumentImpactAssessment>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(ExternalDocumentImpactAssessment assessment, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<ExternalDocumentImpactAssessment>.Filter.And(ExecutionFilter,
                Builders<ExternalDocumentImpactAssessment>.Filter.Eq(x => x.Id, assessment.Id)),
            assessment, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class ExternalDocumentInternalLinkRepository
    : TenantRepository<ExternalDocumentInternalLink>, IExternalDocumentInternalLinkRepository
{
    public ExternalDocumentInternalLinkRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_external_document_internal_links") { }

    public new Task<ExternalDocumentInternalLink> CreateAsync(ExternalDocumentInternalLink link, CancellationToken ct = default) =>
        base.CreateAsync(link, ct);

    public async Task<IReadOnlyList<ExternalDocumentInternalLink>> GetByExternalDocumentAsync(Guid externalDocumentRegisterEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<ExternalDocumentInternalLink>.Filter.And(
                ExecutionFilter,
                Builders<ExternalDocumentInternalLink>.Filter.Eq(x => x.ExternalDocumentRegisterEntryId, externalDocumentRegisterEntryId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<ExternalDocumentInternalLink>> GetByInternalRegisterEntryAsync(Guid internalRegisterEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<ExternalDocumentInternalLink>.Filter.And(
                ExecutionFilter,
                Builders<ExternalDocumentInternalLink>.Filter.Eq(x => x.InternalRegisterEntryId, internalRegisterEntryId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(ExternalDocumentInternalLink link, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<ExternalDocumentInternalLink>.Filter.And(ExecutionFilter,
                Builders<ExternalDocumentInternalLink>.Filter.Eq(x => x.Id, link.Id)),
            link, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}
