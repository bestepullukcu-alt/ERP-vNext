using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU17 — tenant-scoped Mongo repositories for controlled copies / withdrawal plans / obsolete findings.
// No hard delete.

public sealed class DocumentControlledCopyRepository
    : TenantRepository<DocumentControlledCopy>, IDocumentControlledCopyRepository
{
    public DocumentControlledCopyRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_controlled_copies") { }

    public new Task<DocumentControlledCopy> CreateAsync(DocumentControlledCopy copy, CancellationToken ct = default) =>
        base.CreateAsync(copy, ct);

    public async Task<IReadOnlyList<DocumentControlledCopy>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<DocumentControlledCopy>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortBy(x => x.CopyNumber).ToListAsync(ct);

    public Task<DocumentControlledCopy?> GetByCopyNumberAsync(Guid registerEntryId, int copyNumber, CancellationToken ct = default) =>
        Collection.Find(And(Builders<DocumentControlledCopy>.Filter.And(
                Builders<DocumentControlledCopy>.Filter.Eq(x => x.RegisterEntryId, registerEntryId),
                Builders<DocumentControlledCopy>.Filter.Eq(x => x.CopyNumber, copyNumber)))).FirstOrDefaultAsync(ct)!;

    public async Task<bool> UpdateAsync(DocumentControlledCopy copy, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(And(Builders<DocumentControlledCopy>.Filter.Eq(x => x.Id, copy.Id)), copy, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    private FilterDefinition<DocumentControlledCopy> And(FilterDefinition<DocumentControlledCopy> extra) =>
        Builders<DocumentControlledCopy>.Filter.And(ExecutionFilter, extra);
}

public sealed class DocumentCopyWithdrawalPlanRepository
    : TenantRepository<DocumentCopyWithdrawalPlan>, IDocumentCopyWithdrawalPlanRepository
{
    public DocumentCopyWithdrawalPlanRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_copy_withdrawal_plans") { }

    public new Task<DocumentCopyWithdrawalPlan> CreateAsync(DocumentCopyWithdrawalPlan plan, CancellationToken ct = default) =>
        base.CreateAsync(plan, ct);

    public async Task<IReadOnlyList<DocumentCopyWithdrawalPlan>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<DocumentCopyWithdrawalPlan>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public Task<DocumentCopyWithdrawalPlan?> GetOpenAsync(Guid registerEntryId, CancellationToken ct = default) =>
        Collection.Find(And(Builders<DocumentCopyWithdrawalPlan>.Filter.And(
                Builders<DocumentCopyWithdrawalPlan>.Filter.Eq(x => x.RegisterEntryId, registerEntryId),
                Builders<DocumentCopyWithdrawalPlan>.Filter.Nin(x => x.PlanStatus,
                    new[] { CopyWithdrawalPlanStatus.Completed, CopyWithdrawalPlanStatus.Cancelled }))))
            .SortByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct)!;

    public async Task<bool> UpdateAsync(DocumentCopyWithdrawalPlan plan, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(And(Builders<DocumentCopyWithdrawalPlan>.Filter.Eq(x => x.Id, plan.Id)), plan, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    private FilterDefinition<DocumentCopyWithdrawalPlan> And(FilterDefinition<DocumentCopyWithdrawalPlan> extra) =>
        Builders<DocumentCopyWithdrawalPlan>.Filter.And(ExecutionFilter, extra);
}

public sealed class DocumentObsoleteCopyFindingRepository
    : TenantRepository<DocumentObsoleteCopyFinding>, IDocumentObsoleteCopyFindingRepository
{
    public DocumentObsoleteCopyFindingRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_obsolete_copy_findings") { }

    public new Task<DocumentObsoleteCopyFinding> CreateAsync(DocumentObsoleteCopyFinding finding, CancellationToken ct = default) =>
        base.CreateAsync(finding, ct);

    public async Task<IReadOnlyList<DocumentObsoleteCopyFinding>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<DocumentObsoleteCopyFinding>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.DetectedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentObsoleteCopyFinding finding, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(And(Builders<DocumentObsoleteCopyFinding>.Filter.Eq(x => x.Id, finding.Id)), finding, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    private FilterDefinition<DocumentObsoleteCopyFinding> And(FilterDefinition<DocumentObsoleteCopyFinding> extra) =>
        Builders<DocumentObsoleteCopyFinding>.Filter.And(ExecutionFilter, extra);
}
