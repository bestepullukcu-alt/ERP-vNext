using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleRegistration;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleRegistration;

// SELF-REG Faz 1 — idempotent reconcile + ownership rules over a pushed module manifest.
public sealed class RegisterModuleManifestCommandHandlerTests
{
    [Fact]
    public async Task First_register_creates_catalog_pages_actions_and_syncs_permissions()
    {
        var (handler, catalog, pages, actions, sync) = Build();

        var result = await handler.Handle(new RegisterModuleManifestCommand(GoldenSlimManifest()), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal("created", result.Data!.CatalogAction);
        Assert.Equal(1, result.Data.PagesUpserted);
        Assert.Equal(3, result.Data.ActionsUpserted);
        Assert.Equal(4, result.Data.PermissionsSynced); // read (page) + create/update/delete (actions)

        var item = Assert.Single(catalog.Items);
        Assert.Equal("GOLDENSLIM", item.ModuleCode);
        Assert.Equal("DevEnablement", item.Domain);
        Assert.Equal(ModuleCatalogStatus.Active, item.Status);
        Assert.Single(pages.Items);
        Assert.Equal(3, actions.Items.Count);
        Assert.Equal(
            new[] { "goldenslim.records.read", "goldenslim.records.create", "goldenslim.records.update", "goldenslim.records.delete" }.OrderBy(k => k),
            sync.SyncedKeys.OrderBy(k => k));
    }

    [Fact]
    public async Task Re_register_same_manifest_is_idempotent_no_duplicates()
    {
        var (handler, catalog, pages, actions, _) = Build();

        await handler.Handle(new RegisterModuleManifestCommand(GoldenSlimManifest()), CancellationToken.None);
        var second = await handler.Handle(new RegisterModuleManifestCommand(GoldenSlimManifest()), CancellationToken.None);

        Assert.Equal("updated", second.Data!.CatalogAction);
        Assert.Single(catalog.Items);   // no duplicate catalog row
        Assert.Single(pages.Items);     // no duplicate page
        Assert.Equal(3, actions.Items.Count); // no duplicate actions
    }

    [Fact]
    public async Task Re_register_preserves_operator_soft_fields_and_refreshes_hard_fields()
    {
        var (handler, catalog, _, _, _) = Build();
        // Operator-curated catalog item: changed Domain/Service/DisplayName/SortOrder/IsTenantAssignable.
        catalog.Items.Add(new ModuleCatalogItem
        {
            ModuleCode = "GOLDENSLIM",
            ModuleName = "Stale Name",
            ModuleVersion = "0.0.1",
            DisplayName = "Operator Display",
            Domain = "OPERATOR-DOMAIN",
            Service = "OPERATOR-SERVICE",
            IsTenantAssignable = false,
            SortOrder = 5,
            Status = ModuleCatalogStatus.Inactive
        });

        await handler.Handle(new RegisterModuleManifestCommand(GoldenSlimManifest()), CancellationToken.None);

        var item = Assert.Single(catalog.Items);
        // HARD refreshed:
        Assert.Equal("Golden Reference Slim", item.ModuleName);
        Assert.Equal("1.0.0", item.ModuleVersion);
        // SOFT (operator-owned) preserved:
        Assert.Equal("OPERATOR-DOMAIN", item.Domain);
        Assert.Equal("OPERATOR-SERVICE", item.Service);
        Assert.Equal("Operator Display", item.DisplayName);
        Assert.False(item.IsTenantAssignable);
        Assert.Equal(5, item.SortOrder);
        Assert.Equal(ModuleCatalogStatus.Inactive, item.Status);
    }

    [Fact]
    public async Task Page_upsert_refreshes_route_and_prunes_pages_not_in_the_manifest()
    {
        var (handler, _, pages, _, _) = Build();

        await handler.Handle(new RegisterModuleManifestCommand(GoldenSlimManifest("/GoldenReferenceSlim")), CancellationToken.None);
        // A page on this (code-owned) module that the manifest does NOT declare → an orphan.
        pages.Items.Add(new ModulePageDescriptor
        {
            TenantId = Guid.Empty,
            ModuleCode = "GOLDENSLIM",
            PageCode = "OPERATOR_PAGE",
            DisplayName = "Operator Page",
            RoutePath = "/GoldenSlim/Operator"
        });

        // Re-register with a CHANGED route for the code-owned RECORDS page.
        var second = await handler.Handle(new RegisterModuleManifestCommand(GoldenSlimManifest("/GoldenReferenceSlim/V2")), CancellationToken.None);

        Assert.True(second.IsSuccessful);
        var records = Assert.Single(pages.Items, p => p.PageCode == "RECORDS" && !p.IsDeleted);
        Assert.Equal("/GoldenReferenceSlim/V2", records.RoutePath); // HARD route refreshed
        // MC-6 — authoritative: the orphan page is pruned (soft-deleted), not preserved.
        Assert.Contains(pages.Items, p => p.PageCode == "OPERATOR_PAGE" && p.IsDeleted);
        Assert.Equal(1, second.Data!.PagesPruned);
        Assert.Single(pages.Items, p => !p.IsDeleted); // only RECORDS is live
    }

    // ── manifest ──
    private static ModuleManifestDocument GoldenSlimManifest(string recordsRoute = "/GoldenReferenceSlim") =>
        new(
            "GOLDENSLIM", "Golden Reference Slim", "Golden Slim", "DevEnablement", "DevEnablement", "1.0.0", true, 100,
            [
                new ModuleManifestPage("RECORDS", "Records", recordsRoute, "goldenslim.records.read", null, true, "List", 10,
                [
                    new ModuleManifestAction("CREATE", "Create", "goldenslim.records.create", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("UPDATE", "Edit", "goldenslim.records.update", "RowAction", 20, false, false, true),
                    new ModuleManifestAction("DELETE", "Delete", "goldenslim.records.delete", "RowAction", 30, true, false, true)
                ])
            ]);

    // ── harness ──
    private static (RegisterModuleManifestCommandHandler handler, FakeCatalogRepository catalog, FakePageRepository pages, FakeActionRepository actions, FakeSyncService sync) Build()
    {
        var catalog = new FakeCatalogRepository();
        var pages = new FakePageRepository();
        var actions = new FakeActionRepository();
        var sync = new FakeSyncService();
        var handler = new RegisterModuleManifestCommandHandler(catalog, pages, actions, sync,
            new PassthroughTaxonomyResolver(), NullLogger<RegisterModuleManifestCommandHandler>.Instance);
        return (handler, catalog, pages, actions, sync);
    }

    [Fact]
    public async Task Orphan_page_holding_a_manifest_route_is_pruned_then_the_route_is_reclaimed()
    {
        var (handler, _, pages, _, _) = Build();
        // An orphan page (not in the manifest) currently holds the route the manifest's RECORDS page wants.
        pages.Items.Add(new ModulePageDescriptor
        {
            TenantId = Guid.Empty,
            ModuleCode = "GOLDENSLIM",
            PageCode = "OPERATOR_RECORDS",
            DisplayName = "Operator Records",
            RoutePath = "/GoldenReferenceSlim"
        });

        var result = await handler.Handle(new RegisterModuleManifestCommand(GoldenSlimManifest("/GoldenReferenceSlim")), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        // MC-6 — prune-first frees the route: the orphan is soft-deleted and RECORDS is created (no skip).
        Assert.Equal(1, result.Data!.PagesUpserted);
        Assert.Equal(1, result.Data!.PagesPruned);
        Assert.Empty(result.Data.PagesSkipped);
        Assert.Contains(pages.Items, p => p.PageCode == "OPERATOR_RECORDS" && p.IsDeleted);
        Assert.Contains(pages.Items, p => p.PageCode == "RECORDS" && !p.IsDeleted);
        Assert.Single(pages.Items, p => !p.IsDeleted);
    }

    [Fact]
    public async Task Soft_deleted_page_does_not_block_reclaiming_its_route()
    {
        var (handler, _, pages, _, _) = Build();
        // FIX C: a soft-deleted page that previously held the route must NOT be treated as a live holder.
        pages.Items.Add(new ModulePageDescriptor
        {
            TenantId = Guid.Empty,
            ModuleCode = "GOLDENSLIM",
            PageCode = "OLD_RECORDS",
            DisplayName = "Old Records",
            RoutePath = "/GoldenReferenceSlim",
            IsDeleted = true
        });

        var result = await handler.Handle(new RegisterModuleManifestCommand(GoldenSlimManifest("/GoldenReferenceSlim")), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.PagesUpserted);   // RECORDS reclaimed the freed route
        Assert.Empty(result.Data.PagesSkipped);         // soft-deleted holder did not cause a skip
        Assert.Contains(pages.Items, p => p.PageCode == "RECORDS" && !p.IsDeleted);
    }

    // NOTE: the E11000 duplicate-key backstop in the handler is defense-in-depth for races / cross-push collisions.
    // MongoDB.Driver 3.x makes MongoWriteException/WriteError impractical to construct in a unit test, so it is not
    // exercised here directly; the realistic same-push route collision is covered by the test above (skip, no 500).

    private sealed class FakeCatalogRepository : IModuleCatalogRepository
    {
        public List<ModuleCatalogItem> Items { get; } = [];

        public Task<ModuleCatalogItem> CreateAsync(ModuleCatalogItem item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.FromResult(item);
        }

        public Task<ModuleCatalogItem?> GetByCodeAsync(string moduleCode, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.ModuleCode == moduleCode && !x.IsDeleted));

        public Task UpdateAsync(ModuleCatalogItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task<ModuleCatalogItem?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByCodeAsync(string moduleCode, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<ModuleCatalogItem> Items, long TotalCount)> QueryAsync(ModuleCatalogQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ModuleCatalogItem>> GetAssignableAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<ModuleCatalogStatus, long>> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakePageRepository : IModulePageDescriptorRepository
    {
        public List<ModulePageDescriptor> Items { get; } = [];

        public Task<ModulePageDescriptor> CreateAsync(ModulePageDescriptor descriptor, CancellationToken ct = default)
        {
            Items.Add(descriptor);
            return Task.FromResult(descriptor);
        }

        public Task<IReadOnlyList<ModulePageDescriptor>> GetByModuleAsync(string moduleCode, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ModulePageDescriptor>>(Items.Where(p => p.ModuleCode == moduleCode && !p.IsDeleted).ToList());

        public Task UpdateAsync(ModulePageDescriptor descriptor, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default) // MC-6 — soft-delete
        {
            var p = Items.FirstOrDefault(x => x.Id == id);
            if (p is not null) p.IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<ModulePageDescriptor?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ModuleExistsAsync(string moduleCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByPageCodeAsync(string moduleCode, string pageCode, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByRoutePathAsync(string moduleCode, string routePath, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<ModulePageDescriptor> Items, long TotalCount)> SearchAsync(ModulePageDescriptorQuery query, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeActionRepository : IModulePageActionDescriptorRepository
    {
        public List<ModulePageActionDescriptor> Items { get; } = [];

        public Task<ModulePageActionDescriptor> CreateAsync(ModulePageActionDescriptor descriptor, CancellationToken ct = default)
        {
            Items.Add(descriptor);
            return Task.FromResult(descriptor);
        }

        public Task<IReadOnlyList<ModulePageActionDescriptor>> GetByPageAsync(Guid pageDescriptorId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ModulePageActionDescriptor>>(Items.Where(a => a.PageDescriptorId == pageDescriptorId && !a.IsDeleted).ToList());

        public Task UpdateAsync(ModulePageActionDescriptor descriptor, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default) // MC-6 — soft-delete
        {
            var a = Items.FirstOrDefault(x => x.Id == id);
            if (a is not null) a.IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<ModulePageActionDescriptor?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByActionCodeAsync(Guid pageDescriptorId, string actionCode, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    // Identity resolver — preserves manifest Domain/Service so the seed-once assertions hold (resolution is
    // exercised separately in ModuleTaxonomyCanonicalizerTests).
    private sealed class PassthroughTaxonomyResolver : Diten.Platform.Application.Features.ModuleCatalog.Services.IModuleTaxonomyResolver
    {
        public Task<string> ResolveDomainCodeAsync(string? rawDomain, CancellationToken ct = default)
            => Task.FromResult(rawDomain?.Trim() ?? string.Empty);

        public Task<string> ResolveServiceCodeAsync(string? rawService, CancellationToken ct = default)
            => Task.FromResult(rawService?.Trim() ?? string.Empty);
    }

    private sealed class FakeSyncService : ICatalogPermissionSyncService
    {
        public List<string> SyncedKeys { get; } = [];

        public Task<CatalogPermissionSyncStatus> SyncPermissionAsync(string? permissionKey, string? displayName, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(permissionKey))
            {
                SyncedKeys.Add(permissionKey);
            }

            return Task.FromResult(CatalogPermissionSyncStatus.Synced);
        }
    }
}
