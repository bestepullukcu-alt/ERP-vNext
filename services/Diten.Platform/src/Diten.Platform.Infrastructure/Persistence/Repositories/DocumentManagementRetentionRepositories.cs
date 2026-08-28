using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU15 — tenant-scoped Mongo repositories for retention policies, retention subjects, legal holds, hold
// membership and disposition requests. NO delete operation exists on any of them: retiring a policy, releasing a
// hold and executing a disposition are all status changes. Only governance metadata and reference strings are
// persisted — no regulated document content ever reaches these collections.

public sealed class DocumentRetentionPolicyRepository
    : TenantRepository<DocumentRetentionPolicy>, IDocumentRetentionPolicyRepository
{
    public DocumentRetentionPolicyRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_retention_policies") { }

    public new Task<DocumentRetentionPolicy> CreateAsync(DocumentRetentionPolicy policy, CancellationToken ct = default) =>
        base.CreateAsync(policy, ct);

    public async Task<DocumentRetentionPolicy?> GetByKeyAsync(string policyKey, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentRetentionPolicy>.Filter.And(
                ExecutionFilter, Builders<DocumentRetentionPolicy>.Filter.Eq(x => x.PolicyKey, policyKey)))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<DocumentRetentionPolicy>> GetActiveBySubjectTypeAsync(RetentionSubjectType subjectType, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentRetentionPolicy>.Filter.And(
                ExecutionFilter,
                Builders<DocumentRetentionPolicy>.Filter.Eq(x => x.SubjectType, subjectType),
                Builders<DocumentRetentionPolicy>.Filter.Eq(x => x.PolicyStatus, RetentionPolicyStatus.Active)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentRetentionPolicy>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentRetentionPolicy policy, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentRetentionPolicy>.Filter.And(ExecutionFilter,
                Builders<DocumentRetentionPolicy>.Filter.Eq(x => x.Id, policy.Id)),
            policy, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentRetentionSubjectRepository
    : TenantRepository<DocumentRetentionSubject>, IDocumentRetentionSubjectRepository
{
    public DocumentRetentionSubjectRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_retention_subjects") { }

    public new Task<DocumentRetentionSubject> CreateAsync(DocumentRetentionSubject subject, CancellationToken ct = default) =>
        base.CreateAsync(subject, ct);

    public async Task<DocumentRetentionSubject?> GetBySubjectAsync(RetentionSubjectType subjectType, Guid subjectId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentRetentionSubject>.Filter.And(
                ExecutionFilter,
                Builders<DocumentRetentionSubject>.Filter.Eq(x => x.SubjectType, subjectType),
                Builders<DocumentRetentionSubject>.Filter.Eq(x => x.SubjectId, subjectId)))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<DocumentRetentionSubject>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentRetentionSubject>.Filter.And(
                ExecutionFilter, Builders<DocumentRetentionSubject>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentRetentionSubject>> GetEligibleAsync(CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentRetentionSubject>.Filter.And(
                ExecutionFilter,
                Builders<DocumentRetentionSubject>.Filter.Eq(x => x.IsDispositionEligible, true),
                Builders<DocumentRetentionSubject>.Filter.Eq(x => x.IsBlockedByLegalHold, false)))
            .SortBy(x => x.RetentionDueDate).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentRetentionSubject>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentRetentionSubject subject, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentRetentionSubject>.Filter.And(ExecutionFilter,
                Builders<DocumentRetentionSubject>.Filter.Eq(x => x.Id, subject.Id)),
            subject, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentLegalHoldRepository
    : TenantRepository<DocumentLegalHold>, IDocumentLegalHoldRepository
{
    public DocumentLegalHoldRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_legal_holds") { }

    public new Task<DocumentLegalHold> CreateAsync(DocumentLegalHold hold, CancellationToken ct = default) =>
        base.CreateAsync(hold, ct);

    public async Task<IReadOnlyList<DocumentLegalHold>> GetActiveAsync(CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentLegalHold>.Filter.And(
                ExecutionFilter, Builders<DocumentLegalHold>.Filter.Eq(x => x.HoldStatus, LegalHoldStatus.Active)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentLegalHold>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentLegalHold hold, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentLegalHold>.Filter.And(ExecutionFilter,
                Builders<DocumentLegalHold>.Filter.Eq(x => x.Id, hold.Id)),
            hold, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentLegalHoldSubjectRepository
    : TenantRepository<DocumentLegalHoldSubject>, IDocumentLegalHoldSubjectRepository
{
    public DocumentLegalHoldSubjectRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_legal_hold_subjects") { }

    public new Task<DocumentLegalHoldSubject> CreateAsync(DocumentLegalHoldSubject subject, CancellationToken ct = default) =>
        base.CreateAsync(subject, ct);

    public async Task<IReadOnlyList<DocumentLegalHoldSubject>> GetByHoldAsync(Guid legalHoldId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentLegalHoldSubject>.Filter.And(
                ExecutionFilter, Builders<DocumentLegalHoldSubject>.Filter.Eq(x => x.LegalHoldId, legalHoldId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentLegalHoldSubject>> GetBySubjectAsync(RetentionSubjectType subjectType, Guid subjectId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentLegalHoldSubject>.Filter.And(
                ExecutionFilter,
                Builders<DocumentLegalHoldSubject>.Filter.Eq(x => x.SubjectType, subjectType),
                Builders<DocumentLegalHoldSubject>.Filter.Eq(x => x.SubjectId, subjectId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentLegalHoldSubject subject, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentLegalHoldSubject>.Filter.And(ExecutionFilter,
                Builders<DocumentLegalHoldSubject>.Filter.Eq(x => x.Id, subject.Id)),
            subject, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentDispositionRequestRepository
    : TenantRepository<DocumentDispositionRequest>, IDocumentDispositionRequestRepository
{
    public DocumentDispositionRequestRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_disposition_requests") { }

    public new Task<DocumentDispositionRequest> CreateAsync(DocumentDispositionRequest request, CancellationToken ct = default) =>
        base.CreateAsync(request, ct);

    public async Task<IReadOnlyList<DocumentDispositionRequest>> GetBySubjectAsync(RetentionSubjectType subjectType, Guid subjectId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentDispositionRequest>.Filter.And(
                ExecutionFilter,
                Builders<DocumentDispositionRequest>.Filter.Eq(x => x.SubjectType, subjectType),
                Builders<DocumentDispositionRequest>.Filter.Eq(x => x.SubjectId, subjectId)))
            .SortByDescending(x => x.RequestedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentDispositionRequest>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentDispositionRequest request, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentDispositionRequest>.Filter.And(ExecutionFilter,
                Builders<DocumentDispositionRequest>.Filter.Eq(x => x.Id, request.Id)),
            request, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}
