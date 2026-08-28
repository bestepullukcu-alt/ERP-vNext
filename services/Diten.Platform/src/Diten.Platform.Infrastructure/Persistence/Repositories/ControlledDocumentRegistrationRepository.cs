using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class ControlledDocumentRegistrationRepository
    : TenantRepository<ControlledDocumentRegistrationOperation>, IControlledDocumentRegistrationRepository
{
    public const string CollectionName = "document_management_controlled_document_registration_operations";

    public ControlledDocumentRegistrationRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, CollectionName) { }

    public Task<ControlledDocumentRegistrationOperation> AddAsync(ControlledDocumentRegistrationOperation operation, CancellationToken ct = default) =>
        CreateAsync(operation, ct);

    public Task<ControlledDocumentRegistrationOperation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
        Collection.Find(And(Builders<ControlledDocumentRegistrationOperation>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey))).FirstOrDefaultAsync(ct)!;

    public Task<ControlledDocumentRegistrationOperation?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default) =>
        Collection.Find(And(Builders<ControlledDocumentRegistrationOperation>.Filter.Eq(x => x.ControlledDocumentId, controlledDocumentId))).FirstOrDefaultAsync(ct)!;

    public Task<ControlledDocumentRegistrationOperation?> GetByMasterRegisterEntryIdAsync(Guid masterRegisterEntryId, CancellationToken ct = default) =>
        Collection.Find(And(Builders<ControlledDocumentRegistrationOperation>.Filter.Eq(x => x.MasterRegisterEntryId, masterRegisterEntryId))).FirstOrDefaultAsync(ct)!;

    public async Task<IReadOnlyList<ControlledDocumentRegistrationOperation>> ListByStatusAsync(
        ControlledDocumentRegistrationStatus status, CancellationToken ct = default) =>
        await Collection.Find(And(Builders<ControlledDocumentRegistrationOperation>.Filter.Eq(x => x.Status, status)))
            .SortByDescending(x => x.UpdatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(ControlledDocumentRegistrationOperation operation, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            And(Builders<ControlledDocumentRegistrationOperation>.Filter.Eq(x => x.Id, operation.Id)),
            operation,
            cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    private FilterDefinition<ControlledDocumentRegistrationOperation> And(FilterDefinition<ControlledDocumentRegistrationOperation> extra) =>
        Builders<ControlledDocumentRegistrationOperation>.Filter.And(ExecutionFilter, extra);
}
