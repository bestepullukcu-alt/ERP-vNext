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
        var moduleCatalogCollection = database.GetCollection<ModuleCatalogItem>("platform_module_catalog");
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

        await moduleCatalogCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.ModuleCode),
                new CreateIndexOptions { Unique = true, Name = "ux_platform_module_catalog_module_code" }),
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_platform_module_catalog_status" }),
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.Domain),
                new CreateIndexOptions { Name = "ix_platform_module_catalog_domain" }),
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.Service),
                new CreateIndexOptions { Name = "ix_platform_module_catalog_service" }),
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.Category),
                new CreateIndexOptions { Name = "ix_platform_module_catalog_category" }),
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.IsTenantAssignable),
                new CreateIndexOptions { Name = "ix_platform_module_catalog_assignable" }),
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.SortOrder),
                new CreateIndexOptions { Name = "ix_platform_module_catalog_sort_order" })
        });
    }
}
