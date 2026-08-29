using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

/// <summary>
/// The part of schema startup that is NOT the schema: dropping superseded indexes, and two jobs that change
/// DATA.
///
/// ⚠ WHY THIS IS A SEPARATE FILE, AND WHY IT IS NOT IN THE MANIFEST.
/// <c>MongoDbIndexConfigurations.EnsureIndexesAsync</c> was named "ensure indexes" but did three different
/// things, and two of them wrote rows:
///
///   • <see cref="SoftDeleteDomainsForDeletedTenantsAsync"/> — soft-deletes tenant_domains rows belonging to
///     deleted tenants. A DATA repair, running on every startup.
///   • <see cref="UnsetRetiredModuleCatalogCategoryAsync"/> — unsets the retired <c>Category</c> field on
///     every platform_module_catalog document. A DATA migration, running on every startup.
///
/// A test profile must NEVER run either. A profile exists so a test builds the four collections it uses;
/// if asking for a profile also ran a data migration, the "cheap" path would carry the most expensive and
/// least reversible behaviour in the file, into a database the test believes it owns. So the manifest is
/// declarative only, and everything imperative lives here.
///
/// ⚠ AND PRODUCTION MUST NOT LOSE THEM. Splitting them out is exactly how a startup job gets dropped:
/// it moves to a new file, nothing calls the new file, and nothing fails — the repair simply stops
/// happening, quietly, forever. <c>PlatformSchemaMigrationsTests</c> pins that the production path still
/// runs both, by name.
///
/// ⚠ ORDERING. Every drop here runs BEFORE the manifest builds anything. That is not the original
/// interleaving, but it preserves the property the interleaving existed for: an index whose definition
/// changed (unique → partial-unique, renamed keys) must be dropped before it is rebuilt, or Mongo answers
/// with IndexOptionsConflict. All-drops-then-all-creates satisfies that for every case here.
/// The one true ordering dependency — the unique <c>CodeKey</c> index needs
/// <c>ModuleDomainDeduplicationMigration</c> to have run first — is unaffected: that migration runs in DI
/// startup, before this method is called at all.
/// </summary>
public static class PlatformSchemaMigrations
{
    /// <summary>The names of the jobs that touch DATA, so a test can assert they are still wired up.</summary>
    public static IReadOnlyList<string> DataJobs { get; } = new[]
    {
        nameof(SoftDeleteDomainsForDeletedTenantsAsync),
        nameof(UnsetRetiredModuleCatalogCategoryAsync)
    };

    /// <summary>
    /// Runs every pre-manifest step, in the order the original method ran them.
    /// PRODUCTION ONLY — no schema profile calls this.
    /// </summary>
    public static async Task RunAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var tenants = database.GetCollection<Tenant>(PlatformCollections.Tenants);
        var tenantDomains = database.GetCollection<TenantDomain>(PlatformCollections.TenantDomains);
        var moduleCatalog = database.GetCollection<ModuleCatalogItem>(PlatformCollections.ModuleCatalog);
        var moduleCatalogDocuments = database.GetCollection<BsonDocument>(PlatformCollections.ModuleCatalog);

        await DropIndexIfExistsAsync(
            database.GetCollection<Domain.Entities.Workflow.WorkflowTransitionLog>(
                PlatformCollections.WorkflowTransitionLogs).Indexes,
            "ix_workflow_transition_logs_tenant_instance_sequence");

        await SoftDeleteDomainsForDeletedTenantsAsync(tenants, tenantDomains);

        await DropIndexIfExistsAsync(tenants.Indexes, "ux_tenants_code");
        await DropIndexIfExistsAsync(tenants.Indexes, "ux_tenants_slug");
        await DropIndexIfExistsAsync(tenants.Indexes, "ux_tenants_domain");
        await DropIndexIfExistsAsync(tenantDomains.Indexes, "ux_tenant_domains_domain_name");

        // Eski non-partial unique index'i düşür; aksi halde aynı isimle partial yeniden oluşturmak
        // IndexOptionsConflict verir.
        await DropIndexIfExistsAsync(moduleCatalog.Indexes, "ux_platform_module_catalog_module_code");
        await DropIndexIfExistsAsync(moduleCatalog.Indexes, "ix_platform_module_catalog_category");

        await UnsetRetiredModuleCatalogCategoryAsync(moduleCatalogDocuments);

        // FIX-DOMAIN-DEDUP — uniqueness moves from the raw Code to the NORMALIZED CodeKey (UPPERCASE, no
        // separators) so two live rows can never share a domain that differs only by separators/case
        // (e.g. "MASTER-DATA-MANAGEMENT" vs "MASTERDATAMANAGEMENT"). ModuleDomainDeduplicationMigration runs
        // BEFORE this (in DI startup) to collapse existing duplicates and backfill CodeKey, so the index
        // builds cleanly. The old Code-based unique index is dropped.
        var moduleDomains = database.GetCollection<ModuleDomain>(PlatformCollections.ModuleDomains);
        await DropIndexIfExistsAsync(moduleDomains.Indexes, "ux_platform_module_domains_code");
        await DropIndexIfExistsAsync(moduleDomains.Indexes, "ux_platform_module_domains_code_key");

        await DropIndexIfExistsAsync(
            database.GetCollection<ModuleService>(PlatformCollections.ModuleServices).Indexes,
            "ux_platform_module_services_code");

        // FIX C: page-code + route-path uniqueness must be PARTIAL (live-only), mirroring the catalog C3e
        // pattern, so a soft-deleted page frees its route/pagecode → operator/manifest can re-open the same
        // route (reclaim works).
        var pageDescriptors = database.GetCollection<ModulePageDescriptor>(
            PlatformCollections.ModulePageDescriptors);
        await DropIndexIfExistsAsync(pageDescriptors.Indexes, "ux_platform_module_pages_tenant_module_page_code");
        await DropIndexIfExistsAsync(pageDescriptors.Indexes, "ux_platform_module_pages_tenant_module_route_path");

        // FIX C: action-code uniqueness PARTIAL (live-only) too — a soft-deleted action frees its
        // (page, actionCode).
        await DropIndexIfExistsAsync(
            database.GetCollection<ModulePageActionDescriptor>(
                PlatformCollections.ModulePageActionDescriptors).Indexes,
            "ux_platform_module_page_actions_tenant_page_action");

        // F-WC-DOC-SCHEMA-PORT (2026-08-28 main-sync) — the ux_working_calendars unique index gained a
        // CalendarStatus clause in its partial filter (archived rows release their code). Mongo refuses to
        // recreate an existing index name with different options (IndexOptionsConflict, code 85), which would
        // crash-loop startup on a DB that already carries the old-options index. Drop first; a fresh DB is
        // fine (DropIndexIfExistsAsync swallows IndexNotFound/NamespaceNotFound). The literal name matches the
        // WorkingCalendar manifest collection (PlatformCollections.WorkingCalendars, added in this same port).
        await DropIndexIfExistsAsync(
            database.GetCollection<WorkingCalendar>(PlatformCollections.WorkingCalendars).Indexes,
            "ux_working_calendars_scope_country_year_code");
    }

    private static async Task DropIndexIfExistsAsync<TDocument>(
        IMongoIndexManager<TDocument> indexes,
        string indexName)
    {
        try
        {
            await indexes.DropOneAsync(indexName);
        }
        catch (MongoCommandException ex) when (ex.CodeName is "IndexNotFound" or "NamespaceNotFound")
        {
        }
    }

    /// <summary>DATA REPAIR — soft-deletes the domains of tenants that were themselves soft-deleted.</summary>
    internal static async Task SoftDeleteDomainsForDeletedTenantsAsync(
        IMongoCollection<Tenant> tenantCollection,
        IMongoCollection<TenantDomain> tenantDomainCollection)
    {
        var deletedTenantIds = await tenantCollection
            .Find(Builders<Tenant>.Filter.Eq(x => x.IsDeleted, true))
            .Project(x => x.Id)
            .ToListAsync();

        if (deletedTenantIds.Count == 0)
        {
            return;
        }

        var filter = Builders<TenantDomain>.Filter.And(
            Builders<TenantDomain>.Filter.In(x => x.TenantId, deletedTenantIds),
            Builders<TenantDomain>.Filter.Eq(x => x.IsDeleted, false));
        var update = Builders<TenantDomain>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.Status, TenantDomainStatus.Inactive)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        await tenantDomainCollection.UpdateManyAsync(filter, update);
    }

    /// <summary>DATA MIGRATION — removes the retired <c>Category</c> field from the module catalog.</summary>
    internal static Task UnsetRetiredModuleCatalogCategoryAsync(IMongoCollection<BsonDocument> moduleCatalogDocuments)
        => moduleCatalogDocuments.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Exists("Category"),
            Builders<BsonDocument>.Update.Unset("Category"));
}
