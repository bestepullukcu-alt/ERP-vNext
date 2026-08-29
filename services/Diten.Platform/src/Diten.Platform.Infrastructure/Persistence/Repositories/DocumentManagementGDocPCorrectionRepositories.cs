using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU21 — tenant-scoped Mongo repositories for the GDocP correction trail. No delete operation exists on
// any of them, and the correction record exposes only the narrow review-update path so the recorded values,
// reason and correction timestamp can never be rewritten. Only field-value text is persisted — never bytes.

public sealed class DocumentGDocPCorrectionRecordRepository
    : TenantRepository<DocumentGDocPCorrectionRecord>, IDocumentGDocPCorrectionRecordRepository
{
    public DocumentGDocPCorrectionRecordRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementGdocpCorrectionRecords) { }

    public new Task<DocumentGDocPCorrectionRecord> CreateAsync(DocumentGDocPCorrectionRecord record, CancellationToken ct = default) =>
        base.CreateAsync(record, ct);

    public async Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetBySubjectAsync(
        GDocPSubjectType subjectType, Guid subjectId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentGDocPCorrectionRecord>.Filter.And(
                ExecutionFilter,
                Builders<DocumentGDocPCorrectionRecord>.Filter.Eq(x => x.SubjectType, subjectType),
                Builders<DocumentGDocPCorrectionRecord>.Filter.Eq(x => x.SubjectId, subjectId)))
            .SortBy(x => x.CorrectedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetPendingReviewAsync(CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentGDocPCorrectionRecord>.Filter.And(
                ExecutionFilter,
                Builders<DocumentGDocPCorrectionRecord>.Filter.Eq(x => x.ReviewStatus, GDocPReviewStatus.PendingReview)))
            .SortBy(x => x.CorrectedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CorrectedAt).ToListAsync(ct);

    /// <summary>
    /// Review-only mutation. Deliberately a targeted $set of the review fields rather than a whole-document
    /// replace, so a caller holding a mutated in-memory record cannot overwrite the recorded correction values.
    /// </summary>
    public async Task<bool> UpdateReviewAsync(DocumentGDocPCorrectionRecord record, CancellationToken ct = default)
    {
        var update = Builders<DocumentGDocPCorrectionRecord>.Update
            .Set(x => x.ReviewStatus, record.ReviewStatus)
            .Set(x => x.ReviewedBy, record.ReviewedBy)
            .Set(x => x.ReviewedByUserId, record.ReviewedByUserId)
            .Set(x => x.ReviewedAt, record.ReviewedAt)
            .Set(x => x.ReviewEvidenceReference, record.ReviewEvidenceReference)
            .Set(x => x.ReviewComment, record.ReviewComment)
            .Set(x => x.UpdatedAt, record.UpdatedAt)
            .Set(x => x.UpdatedBy, record.UpdatedBy);

        var result = await Collection.UpdateOneAsync(
            Builders<DocumentGDocPCorrectionRecord>.Filter.And(ExecutionFilter,
                Builders<DocumentGDocPCorrectionRecord>.Filter.Eq(x => x.Id, record.Id)),
            update, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentGDocPCorrectionPolicyRepository
    : TenantRepository<DocumentGDocPCorrectionPolicy>, IDocumentGDocPCorrectionPolicyRepository
{
    public DocumentGDocPCorrectionPolicyRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementGdocpCorrectionPolicies) { }

    public new Task<DocumentGDocPCorrectionPolicy> CreateAsync(DocumentGDocPCorrectionPolicy policy, CancellationToken ct = default) =>
        base.CreateAsync(policy, ct);

    public async Task<DocumentGDocPCorrectionPolicy?> GetByKeyAsync(string policyKey, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentGDocPCorrectionPolicy>.Filter.And(
                ExecutionFilter, Builders<DocumentGDocPCorrectionPolicy>.Filter.Eq(x => x.PolicyKey, policyKey)))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<DocumentGDocPCorrectionPolicy>> GetActiveBySubjectTypeAsync(
        GDocPSubjectType subjectType, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentGDocPCorrectionPolicy>.Filter.And(
                ExecutionFilter,
                Builders<DocumentGDocPCorrectionPolicy>.Filter.Eq(x => x.SubjectType, subjectType),
                Builders<DocumentGDocPCorrectionPolicy>.Filter.Eq(x => x.PolicyStatus, GDocPCorrectionPolicyStatus.Active)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentGDocPCorrectionPolicy>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentGDocPCorrectionPolicy policy, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentGDocPCorrectionPolicy>.Filter.And(ExecutionFilter,
                Builders<DocumentGDocPCorrectionPolicy>.Filter.Eq(x => x.Id, policy.Id)),
            policy, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentGDocPCorrectionReviewRepository
    : TenantRepository<DocumentGDocPCorrectionReview>, IDocumentGDocPCorrectionReviewRepository
{
    public DocumentGDocPCorrectionReviewRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementGdocpCorrectionReviews) { }

    public new Task<DocumentGDocPCorrectionReview> CreateAsync(DocumentGDocPCorrectionReview review, CancellationToken ct = default) =>
        base.CreateAsync(review, ct);

    public async Task<IReadOnlyList<DocumentGDocPCorrectionReview>> GetByCorrectionAsync(Guid correctionRecordId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentGDocPCorrectionReview>.Filter.And(
                ExecutionFilter,
                Builders<DocumentGDocPCorrectionReview>.Filter.Eq(x => x.CorrectionRecordId, correctionRecordId)))
            .SortBy(x => x.ReviewedAt).ToListAsync(ct);
}
