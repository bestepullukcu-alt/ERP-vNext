using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU10 — tenant-scoped Mongo repositories for release gate evaluations / results / manual evidence. No hard delete.

public sealed class DocumentReleaseGateEvaluationRepository
    : TenantRepository<DocumentReleaseGateEvaluation>, IDocumentReleaseGateEvaluationRepository
{
    public DocumentReleaseGateEvaluationRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementReleaseGateEvaluations) { }

    public new Task<DocumentReleaseGateEvaluation> CreateAsync(DocumentReleaseGateEvaluation evaluation, CancellationToken ct = default) =>
        base.CreateAsync(evaluation, ct);

    public Task<DocumentReleaseGateEvaluation?> GetLatestAsync(Guid registerEntryId, CancellationToken ct = default) =>
        Collection.Find(And(Builders<DocumentReleaseGateEvaluation>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.EvaluatedAt).FirstOrDefaultAsync(ct)!;

    public async Task<IReadOnlyList<DocumentReleaseGateEvaluation>> GetHistoryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<DocumentReleaseGateEvaluation>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.EvaluatedAt).ToListAsync(ct);

    private FilterDefinition<DocumentReleaseGateEvaluation> And(FilterDefinition<DocumentReleaseGateEvaluation> extra) =>
        Builders<DocumentReleaseGateEvaluation>.Filter.And(ExecutionFilter, extra);
}

public sealed class DocumentReleaseGateResultRepository
    : TenantRepository<DocumentReleaseGateResult>, IDocumentReleaseGateResultRepository
{
    public DocumentReleaseGateResultRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementReleaseGateResults) { }

    public new Task<DocumentReleaseGateResult> CreateAsync(DocumentReleaseGateResult result, CancellationToken ct = default) =>
        base.CreateAsync(result, ct);

    public async Task<IReadOnlyList<DocumentReleaseGateResult>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentReleaseGateResult>.Filter.And(
                ExecutionFilter, Builders<DocumentReleaseGateResult>.Filter.Eq(x => x.EvaluationId, evaluationId)))
            .SortBy(x => x.GateNumber).ToListAsync(ct);
}

public sealed class DocumentReleaseGateEvidenceRepository
    : TenantRepository<DocumentReleaseGateEvidence>, IDocumentReleaseGateEvidenceRepository
{
    public DocumentReleaseGateEvidenceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementReleaseGateEvidence) { }

    public new Task<DocumentReleaseGateEvidence> CreateAsync(DocumentReleaseGateEvidence evidence, CancellationToken ct = default) =>
        base.CreateAsync(evidence, ct);

    public async Task<IReadOnlyList<DocumentReleaseGateEvidence>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<DocumentReleaseGateEvidence>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.VerificationDate).ToListAsync(ct);

    public Task<DocumentReleaseGateEvidence?> GetLatestForGateAsync(Guid registerEntryId, ReleaseGateKey gateKey, CancellationToken ct = default) =>
        Collection.Find(And(Builders<DocumentReleaseGateEvidence>.Filter.And(
                Builders<DocumentReleaseGateEvidence>.Filter.Eq(x => x.RegisterEntryId, registerEntryId),
                Builders<DocumentReleaseGateEvidence>.Filter.Eq(x => x.GateKey, gateKey))))
            .SortByDescending(x => x.VerificationDate).FirstOrDefaultAsync(ct)!;

    private FilterDefinition<DocumentReleaseGateEvidence> And(FilterDefinition<DocumentReleaseGateEvidence> extra) =>
        Builders<DocumentReleaseGateEvidence>.Filter.And(ExecutionFilter, extra);
}
