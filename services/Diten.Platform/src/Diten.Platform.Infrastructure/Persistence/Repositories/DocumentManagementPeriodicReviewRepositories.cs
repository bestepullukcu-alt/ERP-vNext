using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU12 — tenant-scoped Mongo repositories for periodic reviews / extensions / escalations. No hard delete.

public sealed class DocumentPeriodicReviewRepository
    : TenantRepository<DocumentPeriodicReview>, IDocumentPeriodicReviewRepository
{
    public DocumentPeriodicReviewRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementPeriodicReviews) { }

    public new Task<DocumentPeriodicReview> CreateAsync(DocumentPeriodicReview review, CancellationToken ct = default) =>
        base.CreateAsync(review, ct);

    public async Task<IReadOnlyList<DocumentPeriodicReview>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<DocumentPeriodicReview>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.ReviewNumber).ToListAsync(ct);

    public Task<DocumentPeriodicReview?> GetOpenAsync(Guid registerEntryId, CancellationToken ct = default) =>
        Collection.Find(And(Builders<DocumentPeriodicReview>.Filter.And(
                Builders<DocumentPeriodicReview>.Filter.Eq(x => x.RegisterEntryId, registerEntryId),
                Builders<DocumentPeriodicReview>.Filter.Nin(x => x.ReviewStatus,
                    new[] { PeriodicReviewStatus.Completed, PeriodicReviewStatus.Cancelled }))))
            .SortByDescending(x => x.ReviewNumber).FirstOrDefaultAsync(ct)!;

    public async Task<bool> UpdateAsync(DocumentPeriodicReview review, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            And(Builders<DocumentPeriodicReview>.Filter.Eq(x => x.Id, review.Id)), review, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    private FilterDefinition<DocumentPeriodicReview> And(FilterDefinition<DocumentPeriodicReview> extra) =>
        Builders<DocumentPeriodicReview>.Filter.And(ExecutionFilter, extra);
}

public sealed class DocumentPeriodicReviewExtensionRepository
    : TenantRepository<DocumentPeriodicReviewExtension>, IDocumentPeriodicReviewExtensionRepository
{
    public DocumentPeriodicReviewExtensionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementPeriodicReviewExtensions) { }

    public new Task<DocumentPeriodicReviewExtension> CreateAsync(DocumentPeriodicReviewExtension extension, CancellationToken ct = default) =>
        base.CreateAsync(extension, ct);

    public async Task<IReadOnlyList<DocumentPeriodicReviewExtension>> GetByReviewAsync(Guid periodicReviewId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentPeriodicReviewExtension>.Filter.And(
                ExecutionFilter, Builders<DocumentPeriodicReviewExtension>.Filter.Eq(x => x.PeriodicReviewId, periodicReviewId)))
            .SortBy(x => x.ExtensionNumber).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentPeriodicReviewExtension extension, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentPeriodicReviewExtension>.Filter.And(ExecutionFilter,
                Builders<DocumentPeriodicReviewExtension>.Filter.Eq(x => x.Id, extension.Id)),
            extension, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentPeriodicReviewEscalationRepository
    : TenantRepository<DocumentPeriodicReviewEscalation>, IDocumentPeriodicReviewEscalationRepository
{
    public DocumentPeriodicReviewEscalationRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementPeriodicReviewEscalations) { }

    public new Task<DocumentPeriodicReviewEscalation> CreateAsync(DocumentPeriodicReviewEscalation escalation, CancellationToken ct = default) =>
        base.CreateAsync(escalation, ct);

    public async Task<IReadOnlyList<DocumentPeriodicReviewEscalation>> GetByReviewAsync(Guid periodicReviewId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentPeriodicReviewEscalation>.Filter.And(
                ExecutionFilter, Builders<DocumentPeriodicReviewEscalation>.Filter.Eq(x => x.PeriodicReviewId, periodicReviewId)))
            .SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentPeriodicReviewEscalation>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentPeriodicReviewEscalation>.Filter.And(
                ExecutionFilter, Builders<DocumentPeriodicReviewEscalation>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.CreatedAt).ToListAsync(ct);
}
