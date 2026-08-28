using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU20 — tenant-scoped Mongo repositories for downtime events, temporary controlled issues and downtime
// escalations. No delete operation exists on any of them; only governance metadata and reference strings are
// persisted — no document content ever reaches these collections.

public sealed class DocumentRepositoryDowntimeEventRepository
    : TenantRepository<DocumentRepositoryDowntimeEvent>, IDocumentRepositoryDowntimeEventRepository
{
    public DocumentRepositoryDowntimeEventRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_repository_downtime_events") { }

    public new Task<DocumentRepositoryDowntimeEvent> CreateAsync(DocumentRepositoryDowntimeEvent e, CancellationToken ct = default) =>
        base.CreateAsync(e, ct);

    public async Task<IReadOnlyList<DocumentRepositoryDowntimeEvent>> GetByStatusAsync(DowntimeStatus status, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentRepositoryDowntimeEvent>.Filter.And(
                ExecutionFilter, Builders<DocumentRepositoryDowntimeEvent>.Filter.Eq(x => x.DowntimeStatus, status)))
            .SortByDescending(x => x.StartedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentRepositoryDowntimeEvent>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.StartedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentRepositoryDowntimeEvent e, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentRepositoryDowntimeEvent>.Filter.And(ExecutionFilter,
                Builders<DocumentRepositoryDowntimeEvent>.Filter.Eq(x => x.Id, e.Id)),
            e, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentTemporaryControlledIssueRepository
    : TenantRepository<DocumentTemporaryControlledIssue>, IDocumentTemporaryControlledIssueRepository
{
    public DocumentTemporaryControlledIssueRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_temporary_controlled_issues") { }

    public new Task<DocumentTemporaryControlledIssue> CreateAsync(DocumentTemporaryControlledIssue issue, CancellationToken ct = default) =>
        base.CreateAsync(issue, ct);

    public async Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetByDowntimeEventAsync(Guid downtimeEventId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentTemporaryControlledIssue>.Filter.And(
                ExecutionFilter, Builders<DocumentTemporaryControlledIssue>.Filter.Eq(x => x.DowntimeEventId, downtimeEventId)))
            .SortBy(x => x.RequestedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentTemporaryControlledIssue>.Filter.And(
                ExecutionFilter, Builders<DocumentTemporaryControlledIssue>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.RequestedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetOutstandingAsync(CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentTemporaryControlledIssue>.Filter.And(
                ExecutionFilter,
                Builders<DocumentTemporaryControlledIssue>.Filter.In(x => x.IssueStatus,
                    new[] { TemporaryIssueStatus.Issued, TemporaryIssueStatus.ReconciliationDue, TemporaryIssueStatus.Overdue })))
            .SortBy(x => x.ReconciliationDueDate).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentTemporaryControlledIssue issue, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentTemporaryControlledIssue>.Filter.And(ExecutionFilter,
                Builders<DocumentTemporaryControlledIssue>.Filter.Eq(x => x.Id, issue.Id)),
            issue, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentDowntimeEscalationRepository
    : TenantRepository<DocumentDowntimeEscalation>, IDocumentDowntimeEscalationRepository
{
    public DocumentDowntimeEscalationRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_downtime_escalations") { }

    public new Task<DocumentDowntimeEscalation> CreateAsync(DocumentDowntimeEscalation escalation, CancellationToken ct = default) =>
        base.CreateAsync(escalation, ct);

    public async Task<IReadOnlyList<DocumentDowntimeEscalation>> GetByDowntimeEventAsync(Guid downtimeEventId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentDowntimeEscalation>.Filter.And(
                ExecutionFilter, Builders<DocumentDowntimeEscalation>.Filter.Eq(x => x.DowntimeEventId, downtimeEventId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentDowntimeEscalation escalation, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentDowntimeEscalation>.Filter.And(ExecutionFilter,
                Builders<DocumentDowntimeEscalation>.Filter.Eq(x => x.Id, escalation.Id)),
            escalation, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}
