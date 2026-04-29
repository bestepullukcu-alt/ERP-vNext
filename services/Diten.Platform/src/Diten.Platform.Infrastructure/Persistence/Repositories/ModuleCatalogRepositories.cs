using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class DomainLandscapeRepository : GlobalRepository<DomainLandscape>, IDomainLandscapeRepository
{
    public DomainLandscapeRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "domain_landscapes")
    {
    }

    public Task<DomainLandscape?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var filter = Builders<DomainLandscape>.Filter.And(
            ExecutionFilter,
            Builders<DomainLandscape>.Filter.Eq(x => x.Code, code));
        return Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public Task UpdateAsync(DomainLandscape entity, CancellationToken ct = default)
    {
        var filter = Builders<DomainLandscape>.Filter.And(
            ExecutionFilter,
            Builders<DomainLandscape>.Filter.Eq(x => x.Id, entity.Id));
        return Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
    }
}

public sealed class SuitePlatformRepository : GlobalRepository<SuitePlatform>, ISuitePlatformRepository
{
    public SuitePlatformRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "suite_platforms")
    {
    }

    public Task<SuitePlatform?> GetByCodeAsync(Guid domainLandscapeId, string code, CancellationToken ct = default)
    {
        var filter = Builders<SuitePlatform>.Filter.And(
            ExecutionFilter,
            Builders<SuitePlatform>.Filter.Eq(x => x.DomainLandscapeId, domainLandscapeId),
            Builders<SuitePlatform>.Filter.Eq(x => x.Code, code));
        return Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public Task UpdateAsync(SuitePlatform entity, CancellationToken ct = default)
    {
        var filter = Builders<SuitePlatform>.Filter.And(
            ExecutionFilter,
            Builders<SuitePlatform>.Filter.Eq(x => x.Id, entity.Id));
        return Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
    }
}

public sealed class CapabilityGroupRepository : GlobalRepository<CapabilityGroup>, ICapabilityGroupRepository
{
    public CapabilityGroupRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "capability_groups")
    {
    }

    public Task<CapabilityGroup?> GetByCodeAsync(Guid suitePlatformId, string code, CancellationToken ct = default)
    {
        var filter = Builders<CapabilityGroup>.Filter.And(
            ExecutionFilter,
            Builders<CapabilityGroup>.Filter.Eq(x => x.SuitePlatformId, suitePlatformId),
            Builders<CapabilityGroup>.Filter.Eq(x => x.Code, code));
        return Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public Task UpdateAsync(CapabilityGroup entity, CancellationToken ct = default)
    {
        var filter = Builders<CapabilityGroup>.Filter.And(
            ExecutionFilter,
            Builders<CapabilityGroup>.Filter.Eq(x => x.Id, entity.Id));
        return Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
    }
}

public sealed class ModuleDefinitionRepository : GlobalRepository<ModuleDefinition>, IModuleDefinitionRepository
{
    public ModuleDefinitionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "module_definitions")
    {
    }

    public Task<ModuleDefinition?> GetByModuleIdAsync(string moduleId, CancellationToken ct = default)
    {
        var filter = Builders<ModuleDefinition>.Filter.And(
            ExecutionFilter,
            Builders<ModuleDefinition>.Filter.Eq(x => x.ModuleId, moduleId));
        return Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public Task UpdateAsync(ModuleDefinition entity, CancellationToken ct = default)
    {
        var filter = Builders<ModuleDefinition>.Filter.And(
            ExecutionFilter,
            Builders<ModuleDefinition>.Filter.Eq(x => x.Id, entity.Id));
        return Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
    }

    public async Task<(IReadOnlyList<ModuleDefinition> Items, long TotalCount)> QueryAsync(ModuleDefinitionQuery query, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<ModuleDefinition>> { ExecutionFilter };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var regex = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query.Search.Trim()), "i");
            filters.Add(Builders<ModuleDefinition>.Filter.Or(
                Builders<ModuleDefinition>.Filter.Regex(x => x.ModuleId, regex),
                Builders<ModuleDefinition>.Filter.Regex(x => x.ModuleName, regex),
                Builders<ModuleDefinition>.Filter.Regex(x => x.SupportModel, regex)));
        }

        if (query.DomainLandscapeId != null)
        {
            filters.Add(Builders<ModuleDefinition>.Filter.Eq(x => x.DomainLandscapeId, query.DomainLandscapeId.Value));
        }

        if (query.SuitePlatformId != null)
        {
            filters.Add(Builders<ModuleDefinition>.Filter.Eq(x => x.SuitePlatformId, query.SuitePlatformId.Value));
        }

        if (query.CapabilityGroupId != null)
        {
            filters.Add(Builders<ModuleDefinition>.Filter.Eq(x => x.CapabilityGroupId, query.CapabilityGroupId.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ModuleLifecycleStatus>(query.Status.Trim(), true, out var status))
        {
            filters.Add(Builders<ModuleDefinition>.Filter.Eq(x => x.Status, status));
        }

        if (query.IsTenantAssignable != null)
        {
            filters.Add(Builders<ModuleDefinition>.Filter.Eq(x => x.IsTenantAssignable, query.IsTenantAssignable.Value));
        }

        if (query.IsPlatformCore != null)
        {
            filters.Add(Builders<ModuleDefinition>.Filter.Eq(x => x.IsPlatformCore, query.IsPlatformCore.Value));
        }

        var filter = Builders<ModuleDefinition>.Filter.And(filters);
        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await Collection.Find(filter)
            .SortBy(x => x.ModuleId)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}

public sealed class ModulePageDefinitionRepository : GlobalRepository<ModulePageDefinition>, IModulePageDefinitionRepository
{
    public ModulePageDefinitionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "module_page_definitions")
    {
    }

    public async Task<IReadOnlyList<ModulePageDefinition>> GetByModuleIdAsync(string moduleId, CancellationToken ct = default)
    {
        var filter = Builders<ModulePageDefinition>.Filter.And(
            ExecutionFilter,
            Builders<ModulePageDefinition>.Filter.Eq(x => x.ModuleId, moduleId));

        return await Collection.Find(filter)
            .SortBy(x => x.PageCode)
            .ToListAsync(ct);
    }

    public Task<ModulePageDefinition?> GetByCodeAsync(string moduleId, string pageCode, CancellationToken ct = default)
    {
        var filter = Builders<ModulePageDefinition>.Filter.And(
            ExecutionFilter,
            Builders<ModulePageDefinition>.Filter.Eq(x => x.ModuleId, moduleId),
            Builders<ModulePageDefinition>.Filter.Eq(x => x.PageCode, pageCode));

        return Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public Task<bool> ExistsByCodeAsync(string moduleId, string pageCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<ModulePageDefinition>>
        {
            ExecutionFilter,
            Builders<ModulePageDefinition>.Filter.Eq(x => x.ModuleId, moduleId),
            Builders<ModulePageDefinition>.Filter.Eq(x => x.PageCode, pageCode)
        };

        if (excludeId != null)
        {
            filters.Add(Builders<ModulePageDefinition>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.Find(Builders<ModulePageDefinition>.Filter.And(filters)).AnyAsync(ct);
    }

    public Task UpdateAsync(ModulePageDefinition entity, CancellationToken ct = default)
    {
        var filter = Builders<ModulePageDefinition>.Filter.And(
            ExecutionFilter,
            Builders<ModulePageDefinition>.Filter.Eq(x => x.Id, entity.Id));
        return Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
    }
}
