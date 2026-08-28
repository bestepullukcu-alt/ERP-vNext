using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU11 — tenant-scoped Mongo repositories for document training matrix requirements + assignments. No hard delete.

public sealed class DocumentTrainingMatrixRequirementRepository
    : TenantRepository<DocumentTrainingMatrixRequirement>, IDocumentTrainingMatrixRequirementRepository
{
    public DocumentTrainingMatrixRequirementRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_training_requirements") { }

    public new Task<DocumentTrainingMatrixRequirement> CreateAsync(DocumentTrainingMatrixRequirement requirement, CancellationToken ct = default) =>
        base.CreateAsync(requirement, ct);

    public async Task<IReadOnlyList<DocumentTrainingMatrixRequirement>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentTrainingMatrixRequirement>.Filter.And(
                ExecutionFilter, Builders<DocumentTrainingMatrixRequirement>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentTrainingMatrixRequirement requirement, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentTrainingMatrixRequirement>.Filter.And(ExecutionFilter,
                Builders<DocumentTrainingMatrixRequirement>.Filter.Eq(x => x.Id, requirement.Id)),
            requirement, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentTrainingAssignmentRepository
    : TenantRepository<DocumentTrainingAssignment>, IDocumentTrainingAssignmentRepository
{
    public DocumentTrainingAssignmentRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_training_assignments") { }

    public new Task<DocumentTrainingAssignment> CreateAsync(DocumentTrainingAssignment assignment, CancellationToken ct = default) =>
        base.CreateAsync(assignment, ct);

    public async Task<IReadOnlyList<DocumentTrainingAssignment>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentTrainingAssignment>.Filter.And(
                ExecutionFilter, Builders<DocumentTrainingAssignment>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.AssignedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentTrainingAssignment>> GetByRequirementAsync(Guid requirementId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentTrainingAssignment>.Filter.And(
                ExecutionFilter, Builders<DocumentTrainingAssignment>.Filter.Eq(x => x.RequirementId, requirementId)))
            .SortByDescending(x => x.AssignedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentTrainingAssignment assignment, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentTrainingAssignment>.Filter.And(ExecutionFilter,
                Builders<DocumentTrainingAssignment>.Filter.Eq(x => x.Id, assignment.Id)),
            assignment, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}
