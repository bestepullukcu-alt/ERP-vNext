using Diten.Platform.Domain.Entities;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class MongoDbIndexConfigurations
{
    public static async Task EnsureIndexesAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<SavedView>("saved_views");
        var tenantCollection = database.GetCollection<Tenant>("tenants");
        var tenantDomainCollection = database.GetCollection<TenantDomain>("tenant_domains");
        var tenantLoginSettingsCollection = database.GetCollection<TenantLoginSettings>("tenant_login_settings");
        var domainLandscapeCollection = database.GetCollection<DomainLandscape>("domain_landscapes");
        var suitePlatformCollection = database.GetCollection<SuitePlatform>("suite_platforms");
        var capabilityGroupCollection = database.GetCollection<CapabilityGroup>("capability_groups");
        var moduleDefinitionCollection = database.GetCollection<ModuleDefinition>("module_definitions");
        var modulePageDefinitionCollection = database.GetCollection<ModulePageDefinition>("module_page_definitions");

        await collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<SavedView>(
                Builders<SavedView>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.ModuleKey)
                    .Ascending(x => x.PageKey)
                    .Ascending(x => x.Status)),
            new CreateIndexModel<SavedView>(
                Builders<SavedView>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.ModuleKey)
                    .Ascending(x => x.PageKey)
                    .Ascending(x => x.IsDefault)
                    .Ascending(x => x.Status)),
            new CreateIndexModel<SavedView>(
                Builders<SavedView>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.ModuleKey)
                    .Ascending(x => x.PageKey)
                    .Ascending(x => x.ViewName)
                    .Ascending(x => x.Status))
        });

        await tenantCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys.Ascending(x => x.Code),
                new CreateIndexOptions { Unique = true, Name = "ux_tenants_code" }),
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys.Ascending(x => x.Slug),
                new CreateIndexOptions { Unique = true, Name = "ux_tenants_slug" }),
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys.Ascending(x => x.Domain),
                new CreateIndexOptions { Unique = true, Name = "ux_tenants_domain" }),
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.Region)
                    .Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_tenants_status_region_createdat" })
        });

        await tenantDomainCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TenantDomain>(
                Builders<TenantDomain>.IndexKeys.Ascending(x => x.DomainName),
                new CreateIndexOptions { Unique = true, Name = "ux_tenant_domains_domain_name" }),
            new CreateIndexModel<TenantDomain>(
                Builders<TenantDomain>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IsPrimary),
                new CreateIndexOptions { Name = "ix_tenant_domains_tenantid_primary" }),
            new CreateIndexModel<TenantDomain>(
                Builders<TenantDomain>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_tenant_domains_tenantid_status" })
        });

        await tenantLoginSettingsCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TenantLoginSettings>(
                Builders<TenantLoginSettings>.IndexKeys.Ascending(x => x.TenantRefId),
                new CreateIndexOptions { Unique = true, Name = "ux_tenant_login_settings_tenant_ref_id" })
        });

        await domainLandscapeCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DomainLandscape>(
                Builders<DomainLandscape>.IndexKeys.Ascending(x => x.Code),
                new CreateIndexOptions { Unique = true, Name = "ux_domain_landscapes_code" })
        });

        await suitePlatformCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<SuitePlatform>(
                Builders<SuitePlatform>.IndexKeys
                    .Ascending(x => x.DomainLandscapeId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions { Unique = true, Name = "ux_suite_platforms_domain_code" })
        });

        await capabilityGroupCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<CapabilityGroup>(
                Builders<CapabilityGroup>.IndexKeys
                    .Ascending(x => x.SuitePlatformId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions { Unique = true, Name = "ux_capability_groups_suite_code" })
        });

        await moduleDefinitionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ModuleDefinition>(
                Builders<ModuleDefinition>.IndexKeys.Ascending(x => x.ModuleId),
                new CreateIndexOptions { Unique = true, Name = "ux_module_definitions_module_id" }),
            new CreateIndexModel<ModuleDefinition>(
                Builders<ModuleDefinition>.IndexKeys.Ascending(x => x.ModuleName),
                new CreateIndexOptions { Name = "ix_module_definitions_module_name" }),
            new CreateIndexModel<ModuleDefinition>(
                Builders<ModuleDefinition>.IndexKeys.Ascending(x => x.DomainLandscapeId),
                new CreateIndexOptions { Name = "ix_module_definitions_domain_landscape_id" }),
            new CreateIndexModel<ModuleDefinition>(
                Builders<ModuleDefinition>.IndexKeys.Ascending(x => x.SuitePlatformId),
                new CreateIndexOptions { Name = "ix_module_definitions_suite_platform_id" }),
            new CreateIndexModel<ModuleDefinition>(
                Builders<ModuleDefinition>.IndexKeys.Ascending(x => x.CapabilityGroupId),
                new CreateIndexOptions { Name = "ix_module_definitions_capability_group_id" })
        });

        await modulePageDefinitionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ModulePageDefinition>(
                Builders<ModulePageDefinition>.IndexKeys
                    .Ascending(x => x.ModuleId)
                    .Ascending(x => x.PageCode),
                new CreateIndexOptions { Unique = true, Name = "ux_module_page_definitions_module_page_code" }),
            new CreateIndexModel<ModulePageDefinition>(
                Builders<ModulePageDefinition>.IndexKeys.Ascending(x => x.ModuleId),
                new CreateIndexOptions { Name = "ix_module_page_definitions_module_id" }),
            new CreateIndexModel<ModulePageDefinition>(
                Builders<ModulePageDefinition>.IndexKeys.Ascending(x => x.RoutePath),
                new CreateIndexOptions { Name = "ix_module_page_definitions_route_path" })
        });
    }
}
