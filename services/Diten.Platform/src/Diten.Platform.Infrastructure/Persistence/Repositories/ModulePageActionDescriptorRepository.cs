using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class ModulePageActionDescriptorRepository
    : TenantRepository<ModulePageActionDescriptor>, IModulePageActionDescriptorRepository
{
    public ModulePageActionDescriptorRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "platform_module_page_action_descriptors")
    {
    }

    public async Task<bool> ExistsByActionCodeAsync(Guid pageDescriptorId, string actionCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<ModulePageActionDescriptor>>
        {
            ExecutionFilter,
            Builders<ModulePageActionDescriptor>.Filter.Eq(x => x.PageDescriptorId, pageDescriptorId),
            Builders<ModulePageActionDescriptor>.Filter.Eq(x => x.ActionCode, actionCode)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<ModulePageActionDescriptor>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return await Collection.Find(Builders<ModulePageActionDescriptor>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateAsync(ModulePageActionDescriptor descriptor, CancellationToken ct = default)
    {
        descriptor.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<ModulePageActionDescriptor>.Filter.And(
            ExecutionFilter,
            Builders<ModulePageActionDescriptor>.Filter.Eq(x => x.Id, descriptor.Id));
        await Collection.ReplaceOneAsync(filter, descriptor, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<ModulePageActionDescriptor>> GetByPageAsync(Guid pageDescriptorId, CancellationToken ct = default)
    {
        var filter = Builders<ModulePageActionDescriptor>.Filter.And(
            ExecutionFilter,
            Builders<ModulePageActionDescriptor>.Filter.Eq(x => x.PageDescriptorId, pageDescriptorId));

        return await Collection.Find(filter)
            .Sort(Builders<ModulePageActionDescriptor>.Sort.Ascending(x => x.SortOrder).Ascending(x => x.ActionCode))
            .ToListAsync(ct);
    }

    // FEAT-CATALOG-PERM-DELETE-SYNC — count of LIVE actions referencing the permission key (ExecutionFilter excludes
    // soft-deleted rows, so a count after the owning descriptor's delete excludes it).
    public async Task<long> CountByPermissionKeyAsync(string permissionKey, CancellationToken ct = default)
    {
        var filter = Builders<ModulePageActionDescriptor>.Filter.And(
            ExecutionFilter,
            Builders<ModulePageActionDescriptor>.Filter.Eq(x => x.PermissionKey, permissionKey));

        return await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
    }
}
