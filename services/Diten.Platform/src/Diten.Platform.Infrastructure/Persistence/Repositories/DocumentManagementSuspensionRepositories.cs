using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU13 — tenant-scoped Mongo repositories for suspension / retirement / temporary-instruction control.
// No hard delete.

public sealed class DocumentSuspensionCaseRepository
    : TenantRepository<DocumentSuspensionCase>, IDocumentSuspensionCaseRepository
{
    public DocumentSuspensionCaseRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_suspension_cases") { }

    public new Task<DocumentSuspensionCase> CreateAsync(DocumentSuspensionCase suspensionCase, CancellationToken ct = default) =>
        base.CreateAsync(suspensionCase, ct);

    public async Task<IReadOnlyList<DocumentSuspensionCase>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<DocumentSuspensionCase>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.CaseNumber).ToListAsync(ct);

    public Task<DocumentSuspensionCase?> GetOpenAsync(Guid registerEntryId, CancellationToken ct = default) =>
        Collection.Find(And(Builders<DocumentSuspensionCase>.Filter.And(
                Builders<DocumentSuspensionCase>.Filter.Eq(x => x.RegisterEntryId, registerEntryId),
                Builders<DocumentSuspensionCase>.Filter.Nin(x => x.CaseStatus,
                    new[] { SuspensionCaseStatus.Closed, SuspensionCaseStatus.Cancelled, SuspensionCaseStatus.Rejected }))))
            .SortByDescending(x => x.CaseNumber).FirstOrDefaultAsync(ct)!;

    public async Task<bool> UpdateAsync(DocumentSuspensionCase suspensionCase, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            And(Builders<DocumentSuspensionCase>.Filter.Eq(x => x.Id, suspensionCase.Id)), suspensionCase, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    private FilterDefinition<DocumentSuspensionCase> And(FilterDefinition<DocumentSuspensionCase> extra) =>
        Builders<DocumentSuspensionCase>.Filter.And(ExecutionFilter, extra);
}

public sealed class DocumentRetirementCaseRepository
    : TenantRepository<DocumentRetirementCase>, IDocumentRetirementCaseRepository
{
    public DocumentRetirementCaseRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_retirement_cases") { }

    public new Task<DocumentRetirementCase> CreateAsync(DocumentRetirementCase retirementCase, CancellationToken ct = default) =>
        base.CreateAsync(retirementCase, ct);

    public async Task<IReadOnlyList<DocumentRetirementCase>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentRetirementCase>.Filter.And(
                ExecutionFilter, Builders<DocumentRetirementCase>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.CaseNumber).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentRetirementCase retirementCase, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentRetirementCase>.Filter.And(ExecutionFilter,
                Builders<DocumentRetirementCase>.Filter.Eq(x => x.Id, retirementCase.Id)),
            retirementCase, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class TemporaryInstructionControlRepository
    : TenantRepository<TemporaryInstructionControl>, ITemporaryInstructionControlRepository
{
    public TemporaryInstructionControlRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_temporary_instruction_controls") { }

    public new Task<TemporaryInstructionControl> CreateAsync(TemporaryInstructionControl control, CancellationToken ct = default) =>
        base.CreateAsync(control, ct);

    public Task<TemporaryInstructionControl?> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        Collection.Find(Builders<TemporaryInstructionControl>.Filter.And(
                ExecutionFilter, Builders<TemporaryInstructionControl>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct)!;

    // MOD-0029-FU32 — read-only enumeration for the expiry sweep.
    public async Task<IReadOnlyList<TemporaryInstructionControl>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortBy(x => x.ValidUntil).ToListAsync(ct);

    public async Task<bool> UpdateAsync(TemporaryInstructionControl control, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<TemporaryInstructionControl>.Filter.And(ExecutionFilter,
                Builders<TemporaryInstructionControl>.Filter.Eq(x => x.Id, control.Id)),
            control, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}
